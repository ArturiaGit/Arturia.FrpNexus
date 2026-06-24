using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Configuration;

namespace Arturia.FrpNexus.Infrastructure.Configuration;

public sealed class SqliteFrpNexusSettingsStore(
    ISettingsService settingsService,
    ILocalFrpcConfigurationService frpcConfigurationService) : IFrpNexusSettingsStore
{
    private const string CliConfigurationNodeName = "";

    public async Task<FrpNexusSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        var configuration = await frpcConfigurationService.GetConfigurationAsync(
            CliConfigurationNodeName,
            cancellationToken);

        return FrpNexusSettings.Default with
        {
            FrpcPath = configuration.FrpcBinaryPath
        };
    }

    public async Task SaveAsync(FrpNexusSettings settings, CancellationToken cancellationToken = default)
    {
        var snapshot = await settingsService.GetSettingsAsync(cancellationToken);
        await settingsService.SaveSettingsAsync(snapshot, cancellationToken);
        await frpcConfigurationService.SaveFrpcBinaryPathAsync(settings.FrpcPath, cancellationToken);
    }
}
