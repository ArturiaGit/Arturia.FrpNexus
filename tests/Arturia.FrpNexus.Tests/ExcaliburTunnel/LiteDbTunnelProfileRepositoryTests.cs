using Arturia.FrpNexus.Core.ExcaliburTunnel;
using Arturia.FrpNexus.Infrastructure.Configuration;
using Arturia.FrpNexus.Infrastructure.ExcaliburTunnel;
using Arturia.FrpNexus.Tests.Configuration;

namespace Arturia.FrpNexus.Tests.ExcaliburTunnel;

public sealed class LiteDbTunnelProfileRepositoryTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "FrpNexusTests", Guid.NewGuid().ToString("N"));
    private readonly string databasePath;

    public LiteDbTunnelProfileRepositoryTests()
    {
        databasePath = Path.Combine(tempDirectory, "frpnexus.db");
    }

    [Fact]
    public async Task ListAsync_WithEmptyDatabase_ReturnsEmptyList()
    {
        var repository = CreateRepository();

        var profiles = await repository.ListAsync();

        Assert.Empty(profiles);
    }

    [Fact]
    public async Task SaveAsync_WithNewProfile_CanFindById()
    {
        var repository = CreateRepository();
        var profile = CreateProfile("my-server");

        await repository.SaveAsync(profile);
        var saved = await repository.FindByIdAsync(profile.Id);

        Assert.Equal(profile, saved);
    }

    [Fact]
    public async Task SaveAsync_WithExistingId_UpdatesProfileWithoutDuplicate()
    {
        var repository = CreateRepository();
        var original = CreateProfile("my-server");
        var updated = original with { Name = "Updated", LocalPort = 9000, Enabled = false };

        await repository.SaveAsync(original);
        await repository.SaveAsync(updated);
        var profiles = await repository.ListAsync();
        var saved = await repository.FindByIdAsync(original.Id);

        Assert.Single(profiles);
        Assert.Equal(updated, saved);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingProfile_ReturnsTrueAndRemovesProfile()
    {
        var repository = CreateRepository();
        await repository.SaveAsync(CreateProfile("my-server"));

        var deleted = await repository.DeleteAsync("my-server");
        var saved = await repository.FindByIdAsync("my-server");

        Assert.True(deleted);
        Assert.Null(saved);
    }

    [Fact]
    public async Task DeleteAsync_WithMissingProfile_ReturnsFalse()
    {
        var repository = CreateRepository();

        var deleted = await repository.DeleteAsync("missing");

        Assert.False(deleted);
    }

    [Fact]
    public async Task FindByIdAsync_WithMissingProfile_ReturnsNull()
    {
        var repository = CreateRepository();

        var profile = await repository.FindByIdAsync("missing");

        Assert.Null(profile);
    }

    [Fact]
    public async Task ListAsync_WithMultipleProfiles_ReturnsAllProfiles()
    {
        var repository = CreateRepository();
        await repository.SaveAsync(CreateProfile("one"));
        await repository.SaveAsync(CreateProfile("two") with { Protocol = TunnelProtocol.Udp });

        var profiles = await repository.ListAsync();

        Assert.Equal(2, profiles.Count);
        Assert.Contains(profiles, profile => profile.Id == "one");
        Assert.Contains(profiles, profile => profile.Id == "two");
    }

    [Fact]
    public async Task SaveAsync_WithBlankId_ThrowsArgumentException()
    {
        var repository = CreateRepository();
        var profile = CreateProfile(" ");

        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync(profile));
    }

    [Fact]
    public async Task SaveAsync_UsesTemporaryDatabasePath()
    {
        var repository = CreateRepository();

        await repository.SaveAsync(CreateProfile("my-server"));

        Assert.True(File.Exists(databasePath));
        Assert.StartsWith(tempDirectory, databasePath, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private LiteDbTunnelProfileRepository CreateRepository()
    {
        return new LiteDbTunnelProfileRepository(new LiteDbConnectionFactory(new TemporaryDatabasePathProvider(databasePath)));
    }

    private static TunnelProfile CreateProfile(string id)
    {
        return new TunnelProfile(
            id,
            "My Server",
            TunnelProtocol.Tcp,
            "127.0.0.1",
            8080,
            18080,
            "frp.example.internal",
            7000,
            true);
    }
}
