namespace Arturia.FrpNexus.Core.ExcaliburTunnel;

public sealed record TunnelValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static TunnelValidationResult Success { get; } = new(true, Array.Empty<string>());

    public static TunnelValidationResult Failed(params string[] errors) => new(false, errors);
}
