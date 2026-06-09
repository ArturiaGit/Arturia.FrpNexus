namespace Arturia.FrpNexus.Core.InvisibleAirService;

public sealed record SystemdServiceUnitRequest(
    string ProfileId,
    string FrpNexusPath,
    string FrpcPath);
