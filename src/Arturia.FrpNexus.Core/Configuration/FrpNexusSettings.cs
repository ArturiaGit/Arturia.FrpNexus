namespace Arturia.FrpNexus.Core.Configuration;

public sealed record FrpNexusSettings(
    int Version,
    string FrpcPath,
    bool MinimizeToTrayOnClose,
    string? ActiveProfileId)
{
    public static FrpNexusSettings Default { get; } = new(1, string.Empty, false, null);
}
