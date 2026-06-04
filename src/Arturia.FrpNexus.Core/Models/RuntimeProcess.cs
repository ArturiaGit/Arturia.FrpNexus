namespace Arturia.FrpNexus.Core.Models;

public sealed record RuntimeProcess(
    string Name,
    string NodeName,
    string ProcessKind,
    FrpNexusStatus Status,
    string ProcessId,
    string Uptime,
    string ListenAddress);
