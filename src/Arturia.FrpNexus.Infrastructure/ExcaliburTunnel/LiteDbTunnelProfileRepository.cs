using Arturia.FrpNexus.Core.ExcaliburTunnel;
using Arturia.FrpNexus.Infrastructure.Configuration;
using LiteDB;

namespace Arturia.FrpNexus.Infrastructure.ExcaliburTunnel;

public sealed class LiteDbTunnelProfileRepository(LiteDbConnectionFactory connectionFactory) : ITunnelProfileRepository
{
    private const string CollectionName = "tunnel_profiles";

    public Task<IReadOnlyList<TunnelProfile>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var database = connectionFactory.Open();
        var profiles = GetCollection(database)
            .FindAll()
            .Select(document => document.ToProfile())
            .ToArray();

        return Task.FromResult<IReadOnlyList<TunnelProfile>>(profiles);
    }

    public Task<TunnelProfile?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(id))
        {
            return Task.FromResult<TunnelProfile?>(null);
        }

        using var database = connectionFactory.Open();
        var document = GetCollection(database).FindById(id);

        return Task.FromResult(document?.ToProfile());
    }

    public Task SaveAsync(TunnelProfile profile, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            throw new ArgumentException("TunnelProfile.Id 不能为空。", nameof(profile));
        }

        using var database = connectionFactory.Open();
        GetCollection(database).Upsert(ProfileDocument.FromProfile(profile));

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(id))
        {
            return Task.FromResult(false);
        }

        using var database = connectionFactory.Open();
        var deleted = GetCollection(database).Delete(id);

        return Task.FromResult(deleted);
    }

    private static ILiteCollection<ProfileDocument> GetCollection(ILiteDatabase database)
    {
        return database.GetCollection<ProfileDocument>(CollectionName);
    }

    private sealed class ProfileDocument
    {
        [BsonId]
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public TunnelProtocol Protocol { get; set; }

        public string LocalHost { get; set; } = string.Empty;

        public int LocalPort { get; set; }

        public int RemotePort { get; set; }

        public string ServerAddress { get; set; } = string.Empty;

        public int ServerPort { get; set; }

        public bool Enabled { get; set; }

        public TunnelProfile ToProfile()
        {
            return new TunnelProfile(Id, Name, Protocol, LocalHost, LocalPort, RemotePort, ServerAddress, ServerPort, Enabled);
        }

        public static ProfileDocument FromProfile(TunnelProfile profile)
        {
            return new ProfileDocument
            {
                Id = profile.Id,
                Name = profile.Name,
                Protocol = profile.Protocol,
                LocalHost = profile.LocalHost,
                LocalPort = profile.LocalPort,
                RemotePort = profile.RemotePort,
                ServerAddress = profile.ServerAddress,
                ServerPort = profile.ServerPort,
                Enabled = profile.Enabled
            };
        }
    }
}
