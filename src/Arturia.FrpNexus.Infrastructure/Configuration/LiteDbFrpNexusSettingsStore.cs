using Arturia.FrpNexus.Core.Configuration;
using LiteDB;

namespace Arturia.FrpNexus.Infrastructure.Configuration;

public sealed class LiteDbFrpNexusSettingsStore(LiteDbConnectionFactory connectionFactory) : IFrpNexusSettingsStore
{
    private const string CollectionName = "settings";
    private const string DefaultSettingsId = "default";

    public Task<FrpNexusSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var database = connectionFactory.Open();
        var collection = database.GetCollection<SettingsDocument>(CollectionName);
        var document = collection.FindById(DefaultSettingsId);

        return Task.FromResult(document?.ToSettings() ?? FrpNexusSettings.Default);
    }

    public Task SaveAsync(FrpNexusSettings settings, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var database = connectionFactory.Open();
        var collection = database.GetCollection<SettingsDocument>(CollectionName);
        collection.Upsert(SettingsDocument.FromSettings(settings));

        return Task.CompletedTask;
    }

    private sealed class SettingsDocument
    {
        [BsonId]
        public string Id { get; set; } = DefaultSettingsId;

        public int Version { get; set; }

        public string FrpcPath { get; set; } = string.Empty;

        public bool MinimizeToTrayOnClose { get; set; }

        public string? ActiveProfileId { get; set; }

        public FrpNexusSettings ToSettings()
        {
            return new FrpNexusSettings(Version, FrpcPath, MinimizeToTrayOnClose, ActiveProfileId);
        }

        public static SettingsDocument FromSettings(FrpNexusSettings settings)
        {
            return new SettingsDocument
            {
                Id = DefaultSettingsId,
                Version = settings.Version,
                FrpcPath = settings.FrpcPath,
                MinimizeToTrayOnClose = settings.MinimizeToTrayOnClose,
                ActiveProfileId = settings.ActiveProfileId
            };
        }
    }
}
