namespace Arturia.FrpNexus.Core.ExcaliburTunnel;

public sealed record TunnelConfigurationParseResult(
    bool IsValid,
    TunnelProfile? Profile,
    IReadOnlyList<string> Errors)
{
    public static TunnelConfigurationParseResult Success(TunnelProfile profile) => new(true, profile, Array.Empty<string>());

    public static TunnelConfigurationParseResult Failed(params string[] errors) => new(false, null, errors);
}
