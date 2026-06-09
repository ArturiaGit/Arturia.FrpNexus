using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Serilog;

namespace Arturia.FrpNexus.Infrastructure.Runtime;

public sealed class LocalFrpcProcessService(ILogger logger, ITomlConfigurationService tomlConfigurationService) : ILocalFrpcProcessService, IDisposable
{
    private const string FrpcPathEnvironmentVariable = "FRPNEXUS_FRPC_PATH";
    private const int FirstManagementPort = 7400;
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
        var frpcPath = ResolveFrpcPath();
        if (string.IsNullOrWhiteSpace(frpcPath))
        {
            return new LocalFrpcProcessResult(
                request.Node.Name,
                FrpNexusStatus.Error,
                completedAt,
                $"未找到本地 frpc。请设置环境变量 {FrpcPathEnvironmentVariable} 指向 frpc.exe。");
        }

        LocalFrpcSession? existingSession;
        lock (_gate)
        {
            if (_sessions.TryGetValue(request.Node.Name, out existingSession) && existingSession.Process.HasExited)
            {
                _sessions.Remove(request.Node.Name);
                TryDeleteFile(existingSession.ConfigPath);
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
                    request.EnabledTunnels,
                    existingSession.ManagementPort);
                File.WriteAllText(existingSession.ConfigPath, tomlContent, Encoding.UTF8);
                return await ReloadAsync(frpcPath, request.Node.Name, existingSession.ConfigPath, completedAt, cancellationToken);
            }

            var managementPort = AllocateManagementPort();
            var newTomlContent = tomlConfigurationService.GenerateClientToml(
                request.Node,
                request.EnabledTunnels,
                managementPort);
            configPath = CreateConfigFile(request.Node.Name, newTomlContent);
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
                TryDeleteFile(configPath);
                return new LocalFrpcProcessResult(
                    request.Node.Name,
                    FrpNexusStatus.Error,
                    completedAt,
                    "本地 frpc 启动失败：进程未创建。");
            }

            lock (_gate)
            {
                _sessions[request.Node.Name] = new LocalFrpcSession(process, configPath, managementPort);
            }

            logger.Information(
                "Local frpc started for node {NodeName} with {TunnelCount} enabled tunnels",
                request.Node.Name,
                request.EnabledTunnels.Count);

            return new LocalFrpcProcessResult(
                request.Node.Name,
                FrpNexusStatus.Running,
                completedAt,
                "本地 frpc 已按节点启动。关闭 FrpNexus 时会自动停止。");
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(configPath))
            {
                TryDeleteFile(configPath);
            }

            logger.Warning(ex, "Failed to start local frpc for node {NodeName}", request.Node.Name);
            return new LocalFrpcProcessResult(
                request.Node.Name,
                FrpNexusStatus.Error,
                completedAt,
                $"本地 frpc 启动失败：{SanitizeMessage(ex.Message)}");
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

    public LocalFrpcProcessSnapshot GetNodeStatus(string nodeName)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(nodeName, out var session))
            {
                return new LocalFrpcProcessSnapshot(nodeName, FrpNexusStatus.Stopped, "本地 frpc 未运行。");
            }

            if (session.Process.HasExited)
            {
                _sessions.Remove(nodeName);
                TryDeleteFile(session.ConfigPath);
                session.Process.Dispose();
                return new LocalFrpcProcessSnapshot(nodeName, FrpNexusStatus.Stopped, "本地 frpc 已退出。");
            }

            return new LocalFrpcProcessSnapshot(
                nodeName,
                FrpNexusStatus.Running,
                "本地 frpc 正在运行。",
                session.ManagementPort);
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
            StopSession(session);
        }
    }

    private static string? ResolveFrpcPath()
    {
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
                $"本地 frpc 热重载失败：{SanitizeMessage(ex.Message)}");
        }
    }

    private int AllocateManagementPort()
    {
        lock (_gate)
        {
            var usedPorts = _sessions.Values.Select(session => session.ManagementPort).ToHashSet();
            var port = FirstManagementPort;
            while (usedPorts.Contains(port))
            {
                port++;
            }

            while (!IsPortAvailable(port))
            {
                port++;
            }

            return port;
        }
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static string CreateConfigFile(string nodeName, string tomlContent)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Arturia",
            "FrpNexus",
            "runtime",
            "frpc");
        Directory.CreateDirectory(directory);

        var fileName = string.Concat(nodeName.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var path = Path.Combine(directory, $"{fileName}.toml");
        File.WriteAllText(path, tomlContent, Encoding.UTF8);
        return path;
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
            // Best effort cleanup during explicit stop or app shutdown.
        }
        finally
        {
            session.Process.Dispose();
            TryDeleteFile(session.ConfigPath);
        }
    }

    private static void TryDeleteFile(string path)
    {
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

    private sealed record LocalFrpcSession(Process Process, string ConfigPath, int ManagementPort);
}
