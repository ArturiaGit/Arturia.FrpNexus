using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Application.Abstractions;

public interface ITomlConfigurationService
{
    string GenerateProxyToml(ConfigurationPreview preview);

    Task ValidateAsync(string tomlContent, CancellationToken cancellationToken = default);
}
