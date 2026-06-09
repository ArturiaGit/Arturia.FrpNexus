using Arturia.FrpNexus.Core.Configuration;
using Arturia.FrpNexus.Infrastructure.Configuration;

namespace Arturia.FrpNexus.Tests.Configuration;

public sealed class LiteDbFrpNexusSettingsStoreTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "FrpNexusTests", Guid.NewGuid().ToString("N"));
    private readonly string databasePath;

    public LiteDbFrpNexusSettingsStoreTests()
    {
        databasePath = Path.Combine(tempDirectory, "frpnexus.db");
    }

    [Fact]
    public async Task LoadAsync_WithMissingDatabase_ReturnsDefaultSettings()
    {
        var store = CreateStore();

        var settings = await store.LoadAsync();

        Assert.Equal(FrpNexusSettings.Default, settings);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_PersistsSettings()
    {
        var store = CreateStore();
        var expected = new FrpNexusSettings(1, "/opt/frp/frpc", true, "my-server");

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task SaveAsync_WithRepeatedSave_UpdatesDefaultSettingsDocument()
    {
        var store = CreateStore();

        await store.SaveAsync(new FrpNexusSettings(1, "/opt/frp/old", false, "old-profile"));
        await store.SaveAsync(new FrpNexusSettings(1, "/opt/frp/new", true, "new-profile"));
        var settings = await store.LoadAsync();

        Assert.Equal("/opt/frp/new", settings.FrpcPath);
        Assert.True(settings.MinimizeToTrayOnClose);
        Assert.Equal("new-profile", settings.ActiveProfileId);
    }

    [Fact]
    public async Task SaveAsync_UsesTemporaryDatabasePath()
    {
        var store = CreateStore();

        await store.SaveAsync(FrpNexusSettings.Default with { FrpcPath = "frpc" });

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

    private LiteDbFrpNexusSettingsStore CreateStore()
    {
        return new LiteDbFrpNexusSettingsStore(new LiteDbConnectionFactory(new TemporaryDatabasePathProvider(databasePath)));
    }
}
