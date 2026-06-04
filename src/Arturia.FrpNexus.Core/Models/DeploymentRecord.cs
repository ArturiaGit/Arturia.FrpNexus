namespace Arturia.FrpNexus.Core.Models;

public sealed record DeploymentRecord(
    string StepName,
    string NodeName,
    string Description,
    FrpNexusStatus Status,
    DateTimeOffset UpdatedAt);
