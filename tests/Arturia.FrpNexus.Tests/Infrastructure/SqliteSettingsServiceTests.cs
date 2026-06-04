using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Infrastructure.Persistence;
using Arturia.FrpNexus.Infrastructure.Settings;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class SqliteSettingsServiceTests
{
    [Fact]
    public void DatabasePathProvider_ShouldUseLocalApplicationData()
    {
        var provider = new FrpNexusDatabasePathProvider();

        var path = provider.GetDatabasePath();

        Assert.Contains(Path.Combine("Arturia", "FrpNexus", "data"), path);
        Assert.EndsWith("frpnexus.db", path);
    }

    [Fact]
    public async Task GetSettingsAsync_ShouldReturnDefaultsOnFirstRun()
    {
        var pathProvider = new TestDatabasePathProvider();
        var service = CreateService(pathProvider);

        var settings = await service.GetSettingsAsync();

        Assert.Equal("Light", settings.Theme);
        Assert.Equal("zh-CN", settings.Language);
        Assert.Equal("GitHub Releases", settings.FrpDownloadSource);
        Assert.EndsWith(Path.Combine("Arturia", "FrpNexus", "core"), settings.CoreDirectory);
        Assert.EndsWith(Path.Combine("Arturia", "FrpNexus", "configs"), settings.ConfigDirectory);
        Assert.EndsWith(Path.Combine("Arturia", "FrpNexus", "logs"), settings.LogDirectory);
        Assert.Equal(pathProvider.GetDatabasePath(), settings.SqliteDatabasePath);
    }

    [Fact]
    public async Task SaveSettingsAsync_ShouldPersistOrdinarySettings()
    {
        var pathProvider = new TestDatabasePathProvider();
        var service = CreateService(pathProvider);
        var expected = new FrpNexusSettingsSnapshot(
            "Dark",
            "zh-CN",
            "GHProxy",
            @"D:\FrpNexus\core",
            @"D:\FrpNexus\configs",
            @"D:\FrpNexus\logs",
            @"D:\Should\Not\Override\frpnexus.db");

        await service.SaveSettingsAsync(expected);

        var actual = await service.GetSettingsAsync();

        Assert.Equal(expected.Theme, actual.Theme);
        Assert.Equal(expected.Language, actual.Language);
        Assert.Equal(expected.FrpDownloadSource, actual.FrpDownloadSource);
        Assert.Equal(expected.CoreDirectory, actual.CoreDirectory);
        Assert.Equal(expected.ConfigDirectory, actual.ConfigDirectory);
        Assert.Equal(expected.LogDirectory, actual.LogDirectory);
        Assert.NotEqual(expected.SqliteDatabasePath, actual.SqliteDatabasePath);
        Assert.Equal(pathProvider.GetDatabasePath(), actual.SqliteDatabasePath);
    }

    [Fact]
    public void SettingsSnapshot_ShouldOnlyExposeOrdinarySettings()
    {
        var properties = typeof(FrpNexusSettingsSnapshot)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(properties, property => property.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Contains("PrivateKey", StringComparison.OrdinalIgnoreCase));
    }

    private static SqliteSettingsService CreateService(TestDatabasePathProvider pathProvider)
    {
        var connectionFactory = new SqliteConnectionFactory(pathProvider);
        var initializer = new SqliteDatabaseInitializer(connectionFactory);

        return new SqliteSettingsService(connectionFactory, initializer, pathProvider);
    }

    private sealed class TestDatabasePathProvider : IFrpNexusDatabasePathProvider
    {
        private readonly string _databasePath = Path.Combine(
            Path.GetTempPath(),
            "FrpNexusTests",
            Guid.NewGuid().ToString("N"),
            "frpnexus.db");

        public string GetDatabasePath()
        {
            return _databasePath;
        }
    }
}
