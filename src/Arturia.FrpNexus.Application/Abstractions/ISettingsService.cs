namespace Arturia.FrpNexus.Application.Abstractions;

public interface ISettingsService
{
    Task<FrpNexusSettingsSnapshot> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task SaveSettingsAsync(FrpNexusSettingsSnapshot settings, CancellationToken cancellationToken = default);
}
