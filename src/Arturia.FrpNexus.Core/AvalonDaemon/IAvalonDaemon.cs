namespace Arturia.FrpNexus.Core.AvalonDaemon;

public interface IAvalonDaemon
{
    Task<DaemonRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    Task StartAsync(string profileId, CancellationToken cancellationToken = default);

    Task StartAsync(StartTunnelRequest request, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task RestartAsync(string profileId, CancellationToken cancellationToken = default);

    Task RestartAsync(StartTunnelRequest request, CancellationToken cancellationToken = default);

}
