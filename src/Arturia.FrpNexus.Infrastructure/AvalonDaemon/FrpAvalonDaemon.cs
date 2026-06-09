using System.ComponentModel;
using System.Diagnostics;
using Arturia.FrpNexus.Core.AvalonDaemon;
using Arturia.FrpNexus.Core.ExcaliburTunnel;
using Arturia.FrpNexus.Infrastructure.ExcaliburTunnel;

namespace Arturia.FrpNexus.Infrastructure.AvalonDaemon;

public sealed class FrpAvalonDaemon : IAvalonDaemon, IDisposable
{
    private const int MaxLogEntries = 200;
    private readonly object _syncRoot = new();
    private readonly List<DaemonLogEntry> _logs = [];
    private Process? _process;
    private string? _activeProfileId;
    private string? _generatedConfigPath;
    private bool _keepGeneratedConfig;
    private RuntimeStatus _status = RuntimeStatus.Stopped;
    private string _healthMessage = "Phase 6 frpc foreground runtime ready.";

    public Task<DaemonRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            return Task.FromResult(new DaemonRuntimeSnapshot(
                _status,
                _activeProfileId,
                _healthMessage,
                _logs.ToArray()));
        }
    }

    public Task StartAsync(string profileId, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Phase 6 真实 frpc 启动需要 StartTunnelRequest，包含 TunnelProfile 与 --frpc-path。");
    }

    public async Task StartAsync(StartTunnelRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_syncRoot)
        {
            if (_process is { HasExited: false })
            {
                throw new InvalidOperationException("当前 FrpNexus CLI 进程已经启动了一个 frpc 子进程。");
            }

            _status = RuntimeStatus.Starting;
            _activeProfileId = request.Profile.Id;
            _healthMessage = "正在启动 frpc。";
            _keepGeneratedConfig = request.KeepGeneratedConfig;
        }

        if (string.IsNullOrWhiteSpace(request.FrpcPath) || !File.Exists(request.FrpcPath))
        {
            MarkFailed($"frpc binary 不存在：{request.FrpcPath}");
            return;
        }

        if (request.Profile.Protocol is TunnelProtocol.Http or TunnelProtocol.Https)
        {
            MarkFailed("Phase 6 只支持 TCP/UDP 隧道，不启动 HTTP/HTTPS。 ");
            return;
        }

        var configPath = WriteTemporaryConfiguration(request.Profile);
        var startInfo = new ProcessStartInfo
        {
            FileName = request.FrpcPath,
            Arguments = $"-c \"{configPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (_, args) => AppendProcessLog(DaemonLogLevel.Info, args.Data);
        process.ErrorDataReceived += (_, args) => AppendProcessLog(DaemonLogLevel.Error, args.Data);
        process.Exited += (_, _) => HandleProcessExited(process.ExitCode);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!process.Start())
            {
                MarkFailed("frpc 进程启动失败：Process.Start 返回 false。");
                process.Dispose();
                return;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            lock (_syncRoot)
            {
                _process = process;
                _generatedConfigPath = configPath;
                _status = RuntimeStatus.Running;
                _healthMessage = "frpc 已由当前 FrpNexus CLI 进程启动。";
            }

            AppendLog(DaemonLogLevel.Success, $"frpc 已启动，PID: {process.Id}，Profile: {request.Profile.Id}。");
        }
        catch (OperationCanceledException)
        {
            process.Dispose();
            MarkFailed("frpc 启动已取消。");
            await StopAsync(CancellationToken.None);
        }
        catch (Win32Exception exception)
        {
            process.Dispose();
            MarkFailed($"frpc 启动失败：{exception.Message}");
        }
        catch (InvalidOperationException exception)
        {
            process.Dispose();
            MarkFailed($"frpc 启动失败：{exception.Message}");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Process? process;

        lock (_syncRoot)
        {
            process = _process;
            _status = RuntimeStatus.Stopping;
            _healthMessage = "正在停止当前 FrpNexus CLI 进程启动的 frpc。";
        }

        if (process is null)
        {
            lock (_syncRoot)
            {
                _status = RuntimeStatus.Stopped;
                _activeProfileId = null;
                _healthMessage = "当前进程没有可停止的 frpc 子进程。";
            }

            AppendLog(DaemonLogLevel.Info, "Phase 6 不支持跨进程 daemon 状态/停止；真实后台服务化留到 Phase 7。");
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(cancellationToken);
            }
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            process.Dispose();
            CleanupConfigFile();

            lock (_syncRoot)
            {
                _process = null;
                _status = RuntimeStatus.Stopped;
                _activeProfileId = null;
                _healthMessage = "frpc 已停止。";
            }

            AppendLog(DaemonLogLevel.Info, "已停止当前 FrpNexus CLI 进程启动的 frpc 子进程。");
        }
    }

    public async Task RestartAsync(string profileId, CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken);
        await StartAsync(profileId, cancellationToken);
    }

    public async Task RestartAsync(StartTunnelRequest request, CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken);
        await StartAsync(request, cancellationToken);
    }

    public void Dispose()
    {
        _process?.Dispose();
    }

    private string WriteTemporaryConfiguration(TunnelProfile profile)
    {
        var directory = Path.Combine(Path.GetTempPath(), "FrpNexus");
        Directory.CreateDirectory(directory);

        var configPath = Path.Combine(directory, $"{SanitizeFileName(profile.Id)}-{Guid.NewGuid():N}.toml");
        File.WriteAllText(configPath, FrpTomlSerializer.Serialize(profile));

        AppendLog(DaemonLogLevel.Info, $"已生成临时 frpc 配置：{configPath}");
        return configPath;
    }

    private void HandleProcessExited(int exitCode)
    {
        lock (_syncRoot)
        {
            if (_status == RuntimeStatus.Stopping || _status == RuntimeStatus.Stopped)
            {
                return;
            }

            _status = exitCode == 0 ? RuntimeStatus.Stopped : RuntimeStatus.Failed;
            _healthMessage = $"frpc 已退出，ExitCode: {exitCode}。";
            _process = null;
        }

        CleanupConfigFile();
        AppendLog(exitCode == 0 ? DaemonLogLevel.Info : DaemonLogLevel.Error, $"frpc 进程退出，ExitCode: {exitCode}。");
    }

    private void MarkFailed(string message)
    {
        lock (_syncRoot)
        {
            _status = RuntimeStatus.Failed;
            _healthMessage = message;
        }

        CleanupConfigFile();
        AppendLog(DaemonLogLevel.Error, message);
    }

    private void AppendProcessLog(DaemonLogLevel fallbackLevel, string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var level = ClassifyLogLevel(message, fallbackLevel);
        AppendLog(level, message);
    }

    private void AppendLog(DaemonLogLevel level, string message)
    {
        lock (_syncRoot)
        {
            _logs.Add(new DaemonLogEntry(DateTimeOffset.UtcNow, level, "frpc", message));
            if (_logs.Count > MaxLogEntries)
            {
                _logs.RemoveRange(0, _logs.Count - MaxLogEntries);
            }
        }
    }

    private void CleanupConfigFile()
    {
        string? path;
        bool keep;

        lock (_syncRoot)
        {
            path = _generatedConfigPath;
            keep = _keepGeneratedConfig;
            _generatedConfigPath = null;
        }

        if (keep || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static DaemonLogLevel ClassifyLogLevel(string message, DaemonLogLevel fallbackLevel)
    {
        if (message.Contains("success", StringComparison.OrdinalIgnoreCase))
        {
            return DaemonLogLevel.Success;
        }

        if (message.Contains("warn", StringComparison.OrdinalIgnoreCase))
        {
            return DaemonLogLevel.Warning;
        }

        if (message.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("failed", StringComparison.OrdinalIgnoreCase))
        {
            return DaemonLogLevel.Error;
        }

        return fallbackLevel;
    }

    private static string SanitizeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalidCharacters.Contains(character) ? '-' : character).ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "profile" : sanitized;
    }
}
