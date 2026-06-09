using Arturia.FrpNexus.Core.AvalonDaemon;

namespace Arturia.FrpNexus.Infrastructure.Mocks;

public sealed class MockAvalonDaemon : IAvalonDaemon
{
    private readonly List<DaemonLogEntry> _logs =
    [
        new DaemonLogEntry(
            DateTimeOffset.UnixEpoch,
            DaemonLogLevel.Info,
            nameof(MockAvalonDaemon),
            "AvalonDaemon mock 已就绪，未启动真实 FRP 进程。")
    ];

    private string? _activeProfileId;
    private RuntimeStatus _status = RuntimeStatus.Stopped;

    public Task<DaemonRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = new DaemonRuntimeSnapshot(
            _status,
            _activeProfileId,
            "Mock runtime only. No FRP process is managed.",
            _logs.ToArray());

        return Task.FromResult(snapshot);
    }

    public Task StartAsync(string profileId, CancellationToken cancellationToken = default)
    {
        _activeProfileId = profileId;
        _status = RuntimeStatus.Running;
        AppendLog(DaemonLogLevel.Success, $"Mock 启动请求已记录：{profileId}。未启动真实 frpc。");

        return Task.CompletedTask;
    }

    public Task StartAsync(StartTunnelRequest request, CancellationToken cancellationToken = default)
    {
        return StartAsync(request.Profile.Id, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _status = RuntimeStatus.Stopped;
        AppendLog(DaemonLogLevel.Info, "Mock 停止请求已记录。未停止真实进程。");

        return Task.CompletedTask;
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

    private void AppendLog(DaemonLogLevel level, string message)
    {
        _logs.Add(new DaemonLogEntry(DateTimeOffset.UtcNow, level, nameof(MockAvalonDaemon), message));
    }
}
