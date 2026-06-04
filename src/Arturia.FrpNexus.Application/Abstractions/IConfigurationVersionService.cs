using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Application.Abstractions;

public interface IConfigurationVersionService
{
    Task<IReadOnlyList<ConfigurationVersion>> ListConfigurationsAsync(CancellationToken cancellationToken = default);

    Task<ConfigurationVersion?> GetConfigurationAsync(string name, CancellationToken cancellationToken = default);

    Task SaveConfigurationAsync(ConfigurationVersion configuration, CancellationToken cancellationToken = default);

    Task DeleteConfigurationAsync(string name, CancellationToken cancellationToken = default);
}
