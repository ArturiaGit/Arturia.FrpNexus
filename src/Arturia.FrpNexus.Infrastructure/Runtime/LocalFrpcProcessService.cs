using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Text;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Serilog;

namespace Arturia.FrpNexus.Infrastructure.Runtime;

public sealed class LocalFrpcProcessService(ILogger logger, ITomlConfigurationService tomlConfigurationService) : ILocalFrpcProcessService, IDisposable
{
    private const string FrpcPathEnvironmentVariable = "FRPNEXUS_FRPC_PATH";
    private const int StartupStabilityDelayMilliseconds = 800;
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private readonly Dictionary<string, LocalFrpcSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public async Task<LocalFrpcProcessResult> ApplyNodeTunnelsAsync(
        LocalFrpcProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.EnabledTunnels.Count == 0)
        {
            return await StopNodeAsync(request.Node.Name, cancellationToken);
        }

        var completedAt = DateTimeOffset.UtcNow;
        var frpcPath = ResolveFrpcPath(request.FrpcBinaryPath);
        if (string.IsNullOrWhiteSpace(frpcPath))
        {
            return new LocalFrpcProcessResult(
                request.Node.Name,
                FrpNexusStatus.Error,
                completedAt,
                $"未找到本地 frpc。请在隧道页选择 frpc 核心，或设置环境变量 {FrpcPathEnvironmentVariable} 指向 frpc.exe。");
        }

        var executableValidationError = ValidateFrpcExecutable(frpcPath);
        if (!string.IsNullOrWhiteSpace(executableValidationError))
        {
            return new LocalFrpcProcessResult(
                request.Node.Name,
                FrpNexusStatus.Error,
                completedAt,
                executableValidationError);
        }

        var configuredConfigPath = request.FrpcConfigPath.Trim();
        if (string.IsNullOrWhiteSpace(configuredConfigPath))
        {
            return new LocalFrpcProcessResult(
                request.Node.Name,
                FrpNexusStatus.Error,
                completedAt,
                "未选择本地 frpc.toml 路径，请先在隧道页选择配置文件路径。");
        }

        LocalFrpcSession? existingSession;
        lock (_gate)
        {
            if (_sessions.TryGetValue(request.Node.Name, out existingSession) && existingSession.Process.HasExited)
            {
                _sessions.Remove(request.Node.Name);
                TryDeleteTemporaryFile(existingSession.ConfigPath, existingSession.DeleteConfigOnStop);
                existingSession.Process.Dispose();
                existingSession = null;
            }
        }

        var configPath = string.Empty;
        try
        {
            if (existingSession is not null)
            {
                var tomlContent = tomlConfigurationService.GenerateClientToml(
                    request.Node,
                    request.EnabledTunnels);
                File.WriteAllText(existingSession.ConfigPath, tomlContent, Utf8NoBom);
                return await ReloadAsync(frpcPath, request.Node.Name, existingSession.ConfigPath, completedAt, cancellationToken);
            }

            var newTomlContent = tomlConfigurationService.GenerateClientToml(
                request.Node,
                request.EnabledTunnels);
            configPath = WriteConfigFile(configuredConfigPath, newTomlContent);
            var startInfo = new ProcessStartInfo
            {
                FileName = frpcPath,
                Arguments = $"-c \"{configPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardErrorEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8
            };

            var process = Process.Start(startInfo);
            if (process is null)
            {
                TryDeleteTemporaryFile(configPath, deleteConfigOnStop: false);
                return new LocalFrpcProcessResult(
                    request.Node.Name,
                    FrpNexusStatus.Error,
                    completedAt,
                    "本地 frpc 启动失败：进程未创建。");
            }

            try
            {
                await Task.Delay(StartupStabilityDelayMilliseconds, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                StopSession(new LocalFrpcSession(process, configPath, false));
                throw;
            }

            if (process.HasExited)
            {
                var output = await ReadProcessOutputSummaryAsync(process, cancellationToken);
                process.Dispose();
                TryDeleteTemporaryFile(configPath, deleteConfigOnStop: false);
                return new LocalFrpcProcessResult(
                    request.Node.Name,
                    FrpNexusStatus.Error,
                    completedAt,
                    $"本地 frpc 启动后已退出：{output}");
            }

            lock (_gate)
            {
                _sessions[request.Node.Name] = new LocalFrpcSession(process, configPath, false);
            }

            logger.Information(
                "Local frpc started for node {NodeName} with {TunnelCount} enabled tunnels, PID {ProcessId}",
                request.Node.Name,
                request.EnabledTunnels.Count,
                process.Id);

            return new LocalFrpcProcessResult(
                request.Node.Name,
                FrpNexusStatus.Running,
                completedAt,
                $"本地 frpc 已按节点启动，PID {process.Id}。关闭 FrpNexus 不会自动停止它。");
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(configPath))
            {
                TryDeleteTemporaryFile(configPath, deleteConfigOnStop: false);
            }

            logger.Warning(ex, "Failed to start local frpc for node {NodeName}", request.Node.Name);
            return new LocalFrpcProcessResult(
                request.Node.Name,
                FrpNexusStatus.Error,
                completedAt,
                $"本地 frpc 启动失败：{FormatProcessStartError(ex)}");
        }
    }

    public Task<LocalFrpcProcessResult> StopNodeAsync(
        string nodeName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        LocalFrpcSession? session = null;
        lock (_gate)
        {
            if (_sessions.Remove(nodeName, out var existing))
            {
                session = existing;
            }
        }

        if (session is null)
        {
            return Task.FromResult(new LocalFrpcProcessResult(
                nodeName,
                FrpNexusStatus.Stopped,
                DateTimeOffset.UtcNow,
                "本地 frpc 未运行。"));
        }

        StopSession(session);
        logger.Information("Local frpc stopped for node {NodeName}", nodeName);

        return Task.FromResult(new LocalFrpcProcessResult(
            nodeName,
            FrpNexusStatus.Stopped,
            DateTimeOffset.UtcNow,
            "该节点本地 frpc 已停止。"));
    }

    public LocalFrpcProcessSnapshot GetNodeStatus(string nodeName, string? expectedConfigPath = null)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(nodeName, out var session))
            {
                return FindUnmanagedFrpcProcess(nodeName, expectedConfigPath)
                    ?? new LocalFrpcProcessSnapshot(nodeName, FrpNexusStatus.Stopped, "本地 frpc 未运行。");
            }

            if (session.Process.HasExited)
            {
                var exitCode = TryGetExitCode(session.Process);
                _sessions.Remove(nodeName);
                TryDeleteTemporaryFile(session.ConfigPath, session.DeleteConfigOnStop);
                session.Process.Dispose();
                return new LocalFrpcProcessSnapshot(
                    nodeName,
                    FrpNexusStatus.Error,
                    exitCode is null
                        ? "本地 frpc 已退出。"
                        : $"本地 frpc 已退出，退出码 {exitCode.Value}。",
                    ConfigPath: session.ConfigPath,
                    ExitCode: exitCode);
            }

            return new LocalFrpcProcessSnapshot(
                nodeName,
                FrpNexusStatus.Running,
                "本地 frpc 正在运行。",
                session.Process.Id,
                session.ConfigPath);
        }
    }

    public IReadOnlyList<LocalFrpcProcessSnapshot> ListManagedSessions()
    {
        lock (_gate)
        {
            var snapshots = new List<LocalFrpcProcessSnapshot>();
            foreach (var item in _sessions.ToArray())
            {
                var nodeName = item.Key;
                var session = item.Value;
                if (session.Process.HasExited)
                {
                    var exitCode = TryGetExitCode(session.Process);
                    _sessions.Remove(nodeName);
                    TryDeleteTemporaryFile(session.ConfigPath, session.DeleteConfigOnStop);
                    session.Process.Dispose();
                    snapshots.Add(new LocalFrpcProcessSnapshot(
                        nodeName,
                        FrpNexusStatus.Error,
                        exitCode is null
                            ? "本地 frpc 已退出。"
                            : $"本地 frpc 已退出，退出码 {exitCode.Value}。",
                        ConfigPath: session.ConfigPath,
                        ExitCode: exitCode));
                    continue;
                }

                snapshots.Add(new LocalFrpcProcessSnapshot(
                    nodeName,
                    FrpNexusStatus.Running,
                    "本地 frpc 正在运行。",
                    session.Process.Id,
                    session.ConfigPath));
            }

            return snapshots;
        }
    }

    public void Dispose()
    {
        LocalFrpcSession[] sessions;
        lock (_gate)
        {
            sessions = _sessions.Values.ToArray();
            _sessions.Clear();
        }

        foreach (var session in sessions)
        {
            DetachSession(session);
        }
    }

    private static string? ResolveFrpcPath(string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        var configured = Environment.GetEnvironmentVariable(FrpcPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, "frpc.exe"),
            Path.Combine(baseDirectory, "frpc"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Arturia",
                "FrpNexus",
                "core",
                "frpc.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string ValidateFrpcExecutable(string frpcPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return string.Empty;
        }

        if (!frpcPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || !HasWindowsExecutableHeader(frpcPath))
        {
            return "当前选择的 frpc 不是 Windows 可执行文件，请选择 frpc.exe。";
        }

        return string.Empty;
    }

    private static bool HasWindowsExecutableHeader(string path)
    {
        try
        {
            Span<byte> header = stackalloc byte[2];
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return stream.Read(header) == 2 && header[0] == 0x4D && header[1] == 0x5A;
        }
        catch
        {
            return false;
        }
    }

    private async Task<LocalFrpcProcessResult> ReloadAsync(
        string frpcPath,
        string nodeName,
        string configPath,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = frpcPath,
                Arguments = $"reload -c \"{configPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardErrorEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new LocalFrpcProcessResult(
                    nodeName,
                    FrpNexusStatus.Error,
                    completedAt,
                    "本地 frpc 热重载失败：进程未创建。");
            }

            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode == 0)
            {
                logger.Information("Local frpc reloaded for node {NodeName}", nodeName);
                return new LocalFrpcProcessResult(
                    nodeName,
                    FrpNexusStatus.Running,
                    completedAt,
                    "本地 frpc 已热重载配置，其他同节点隧道不会重启。");
            }

            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            return new LocalFrpcProcessResult(
                nodeName,
                FrpNexusStatus.Error,
                completedAt,
                $"本地 frpc 热重载失败：{SanitizeMessage(error)}");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to reload local frpc for node {NodeName}", nodeName);
            return new LocalFrpcProcessResult(
                nodeName,
                FrpNexusStatus.Error,
                completedAt,
                $"本地 frpc 热重载失败：{FormatProcessStartError(ex)}");
        }
    }

    private static LocalFrpcProcessSnapshot? FindUnmanagedFrpcProcess(string nodeName, string? expectedConfigPath)
    {
        if (string.IsNullOrWhiteSpace(expectedConfigPath))
        {
            return null;
        }

        var normalizedExpectedPath = NormalizePathForCommandMatch(expectedConfigPath);
        if (string.IsNullOrWhiteSpace(normalizedExpectedPath))
        {
            return null;
        }

        try
        {
            foreach (var process in EnumerateFrpcProcessInfos())
            {
                if (CommandUsesConfigPath(process.CommandLine, expectedConfigPath))
                {
                    return new LocalFrpcProcessSnapshot(
                        nodeName,
                        FrpNexusStatus.Warning,
                        $"检测到外部 frpc 正在使用当前配置，PID {process.ProcessId}，但它不是 FrpNexus 启动的进程。",
                        process.ProcessId,
                        expectedConfigPath,
                        IsManaged: false);
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static IEnumerable<LocalProcessInfo> EnumerateFrpcProcessInfos()
    {
        if (OperatingSystem.IsWindows())
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, Name, ExecutablePath, CommandLine FROM Win32_Process WHERE Name = 'frpc.exe' OR Name = 'frpc'");
            foreach (var item in searcher.Get().OfType<ManagementObject>())
            {
                var processId = Convert.ToInt32(item["ProcessId"], System.Globalization.CultureInfo.InvariantCulture);
                var commandLine = item["CommandLine"] as string ?? item["ExecutablePath"] as string ?? item["Name"] as string ?? string.Empty;
                yield return new LocalProcessInfo(processId, commandLine);
            }

            yield break;
        }

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var name = process.ProcessName;
                if (!name.Equals("frpc", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return new LocalProcessInfo(process.Id, name);
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    internal static bool CommandUsesConfigPath(string commandLine, string expectedConfigPath)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return false;
        }

        var normalizedExpectedPath = NormalizePathForCommandMatch(expectedConfigPath);
        if (string.IsNullOrWhiteSpace(normalizedExpectedPath))
        {
            return false;
        }

        var normalizedCommand = NormalizePathForCommandMatch(commandLine);
        return normalizedCommand.Contains(normalizedExpectedPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePathForCommandMatch(string value)
    {
        return value.Trim().Trim('"').Replace('\\', '/');
    }

    private static int? TryGetExitCode(Process process)
    {
        try
        {
            return process.HasExited ? process.ExitCode : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> ReadProcessOutputSummaryAsync(
        Process process,
        CancellationToken cancellationToken)
    {
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var summary = string.Join(
            " ",
            new[] { stderr, stdout }
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(SanitizeMessage));

        return string.IsNullOrWhiteSpace(summary)
            ? "未返回详细错误。"
            : ShortenMessage(summary);
    }

    private static string WriteConfigFile(string configPath, string tomlContent)
    {
        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(configPath, tomlContent, Utf8NoBom);
        return configPath;
    }

    private static void StopSession(LocalFrpcSession session)
    {
        try
        {
            if (!session.Process.HasExited)
            {
                session.Process.Kill(entireProcessTree: true);
                session.Process.WaitForExit(3000);
            }
        }
        catch
        {
            // Best effort cleanup during explicit stop or canceled startup.
        }
        finally
        {
            session.Process.Dispose();
            TryDeleteTemporaryFile(session.ConfigPath, session.DeleteConfigOnStop);
        }
    }

    private static void DetachSession(LocalFrpcSession session)
    {
        try
        {
            session.Process.Dispose();
        }
        catch
        {
            // Application shutdown must not stop a managed frpc process.
        }
    }

    private static void TryDeleteTemporaryFile(string path, bool deleteConfigOnStop)
    {
        if (!deleteConfigOnStop)
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temporary runtime config cleanup is best effort.
        }
    }

    private static string SanitizeMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message)
            ? "未返回详细错误。"
            : message.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();
    }

    private static string ShortenMessage(string message)
    {
        const int maxLength = 160;
        return message.Length <= maxLength
            ? message
            : message[..maxLength] + "...";
    }

    private static string FormatProcessStartError(Exception exception)
    {
        if (exception is Win32Exception win32Exception
            && win32Exception.Message.Contains("not a valid application", StringComparison.OrdinalIgnoreCase))
        {
            return "当前系统无法运行所选 frpc 核心，请选择 Windows x64 版 frpc.exe。";
        }

        if (exception.Message.Contains("not a valid application", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("valid application for this OS platform", StringComparison.OrdinalIgnoreCase))
        {
            return "当前系统无法运行所选 frpc 核心，请选择 Windows x64 版 frpc.exe。";
        }

        return SanitizeMessage(exception.Message);
    }

    private sealed record LocalFrpcSession(Process Process, string ConfigPath, bool DeleteConfigOnStop);

    private sealed record LocalProcessInfo(int ProcessId, string CommandLine);
}
