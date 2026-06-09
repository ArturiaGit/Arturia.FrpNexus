namespace Arturia.FrpNexus.Core.AvalonDaemon;

public sealed record DaemonLogEntry(
    DateTimeOffset Timestamp,
    DaemonLogLevel Level,
    string Source,
    string Message);
