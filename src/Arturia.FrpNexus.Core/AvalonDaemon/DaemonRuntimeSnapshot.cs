namespace Arturia.FrpNexus.Core.AvalonDaemon;

public sealed record DaemonRuntimeSnapshot(
    RuntimeStatus Status,
    string? ActiveProfileId,
    string HealthMessage,
    IReadOnlyList<DaemonLogEntry> RecentLogs);
