namespace Arturia.FrpNexus.Core.InvisibleAirService;

public sealed record SystemdServiceUnitPreview(
    bool IsValid,
    string UnitName,
    string UnitContent,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> SafetyNotes);
