namespace Arturia.FrpNexus.Core.ExcaliburTunnel;

public interface IExcaliburTunnel
{
    TunnelValidationResult Validate(TunnelProfile profile);

    string PreviewConfiguration(TunnelProfile profile);

    TunnelConfigurationParseResult ParseConfiguration(string configuration);
}
