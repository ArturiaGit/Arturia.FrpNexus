using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Configurations;
using Arturia.FrpNexus.Infrastructure.Persistence;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class SqliteConfigurationVersionServiceTests
{
    [Fact]
    public async Task InitializeAsync_ShouldCreateConfigurationVersionsTable()
    {
        var pathProvider = new TestDatabasePathProvider();
        var connectionFactory = new SqliteConnectionFactory(pathProvider);
        var initializer = new SqliteDatabaseInitializer(connectionFactory);

        await initializer.InitializeAsync();

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'configuration_versions';";

        var tableName = await command.ExecuteScalarAsync();

        Assert.Equal("configuration_versions", tableName);
    }

    [Fact]
    public async Task SaveConfigurationAsync_ShouldPersistAndReadConfiguration()
    {
        var service = CreateService();
        var expected = CreateConfiguration("web_proxy_01", TunnelProtocol.Http);

        await service.SaveConfigurationAsync(expected);

        var actual = await service.GetConfigurationAsync(expected.Name);
        var configurations = await service.ListConfigurationsAsync();

        Assert.Equal(expected, actual);
        Assert.Single(configurations);
        Assert.Equal(expected, configurations[0]);
    }

    [Fact]
    public async Task DeleteConfigurationAsync_ShouldRemoveConfiguration()
    {
        var service = CreateService();
        var configuration = CreateConfiguration("待删除配置", TunnelProtocol.Tcp);

        await service.SaveConfigurationAsync(configuration);
        await service.DeleteConfigurationAsync(configuration.Name);

        Assert.Null(await service.GetConfigurationAsync(configuration.Name));
        Assert.Empty(await service.ListConfigurationsAsync());
    }

    [Fact]
    public void ConfigurationVersion_ShouldNotExposeSensitiveCredentialFields()
    {
        var properties = typeof(ConfigurationVersion)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(properties, property => property.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Contains("PrivateKeyContent", StringComparison.OrdinalIgnoreCase));
    }

    private static SqliteConfigurationVersionService CreateService()
    {
        var pathProvider = new TestDatabasePathProvider();
        var connectionFactory = new SqliteConnectionFactory(pathProvider);
        var initializer = new SqliteDatabaseInitializer(connectionFactory);

        return new SqliteConfigurationVersionService(connectionFactory, initializer);
    }

    private static ConfigurationVersion CreateConfiguration(string name, TunnelProtocol protocol)
    {
        var endpoint = protocol is TunnelProtocol.Http or TunnelProtocol.Https
            ? "dev.example.com"
            : "60022";
        var toml = protocol is TunnelProtocol.Http or TunnelProtocol.Https
            ? $"""
              [[proxies]]
              name = "{name}"
              type = "{protocol.ToString().ToLowerInvariant()}"
              localIP = "127.0.0.1"
              localPort = 8080
              customDomains = ["{endpoint}"]
              """
            : $"""
              [[proxies]]
              name = "{name}"
              type = "{protocol.ToString().ToLowerInvariant()}"
              localIP = "127.0.0.1"
              localPort = 22
              remotePort = {endpoint}
              """;

        return new ConfigurationVersion(
            name,
            protocol,
            "127.0.0.1",
            protocol == TunnelProtocol.Tcp ? 22 : 8080,
            endpoint,
            toml,
            DateTimeOffset.Parse("2026-06-04T12:00:00Z"));
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
