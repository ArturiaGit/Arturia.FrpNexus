namespace Arturia.FrpNexus.Core.Configuration;

public interface IFrpNexusSettingsStore
{
    Task<FrpNexusSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(FrpNexusSettings settings, CancellationToken cancellationToken = default);
}
