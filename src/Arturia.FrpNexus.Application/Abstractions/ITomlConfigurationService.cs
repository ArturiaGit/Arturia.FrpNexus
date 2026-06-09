using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Application.Abstractions;

public interface ITomlConfigurationService
{
    string GenerateProxyToml(ConfigurationPreview preview);

    string GenerateClientToml(NodeProfile node, IReadOnlyList<TunnelProfile> tunnels, int webServerPort);

    string GenerateServerToml(int bindPort);

    Task ValidateAsync(string tomlContent, CancellationToken cancellationToken = default);
}
