namespace Arturia.FrpNexus.Core.Models;

public sealed record LogEntry(
    string Timestamp,
    string Level,
    string NodeName,
    string ProcessName,
    string Message,
    FrpNexusStatus Status);
