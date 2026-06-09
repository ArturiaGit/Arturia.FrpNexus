using Arturia.FrpNexus.Core.Configuration;

namespace Arturia.FrpNexus.Application.Configuration;

public sealed class SettingsService(IFrpNexusSettingsStore settingsStore)
{
    public Task<FrpNexusSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        return settingsStore.LoadAsync(cancellationToken);
    }

    public Task SaveAsync(FrpNexusSettings settings, CancellationToken cancellationToken = default)
    {
        return settingsStore.SaveAsync(settings, cancellationToken);
    }
}
