using Arturia.FrpNexus.Core.AvalonDaemon;

namespace Arturia.FrpNexus.Tests.Cli;

internal sealed class FakeAvalonDaemon : IAvalonDaemon
{
    private DaemonRuntimeSnapshot snapshot = new(RuntimeStatus.Stopped, null, "未启动。", Array.Empty<DaemonLogEntry>());

    public StartTunnelRequest? LastStartRequest { get; private set; }

    public Task<DaemonRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(snapshot);
    }

    public Task StartAsync(string profileId, CancellationToken cancellationToken = default)
    {
        snapshot = new DaemonRuntimeSnapshot(RuntimeStatus.Running, profileId, "Fake daemon running.", Array.Empty<DaemonLogEntry>());
        return Task.CompletedTask;
    }

    public Task StartAsync(StartTunnelRequest request, CancellationToken cancellationToken = default)
    {
        LastStartRequest = request;
        snapshot = new DaemonRuntimeSnapshot(RuntimeStatus.Failed, request.Profile.Id, "Fake daemon did not start real frpc.", Array.Empty<DaemonLogEntry>());
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        snapshot = snapshot with { Status = RuntimeStatus.Stopped, HealthMessage = "Fake daemon stopped." };
        return Task.CompletedTask;
    }

    public Task RestartAsync(string profileId, CancellationToken cancellationToken = default)
    {
        return StartAsync(profileId, cancellationToken);
    }

    public Task RestartAsync(StartTunnelRequest request, CancellationToken cancellationToken = default)
    {
        return StartAsync(request, cancellationToken);
    }
}
