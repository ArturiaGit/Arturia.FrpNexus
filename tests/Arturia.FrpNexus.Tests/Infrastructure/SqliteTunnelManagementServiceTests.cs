using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Persistence;
using Arturia.FrpNexus.Infrastructure.Tunnels;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class SqliteTunnelManagementServiceTests
{
    [Fact]
    public async Task InitializeAsync_ShouldCreateTunnelsTable()
    {
        var pathProvider = new TestDatabasePathProvider();
        var connectionFactory = new SqliteConnectionFactory(pathProvider);
        var initializer = new SqliteDatabaseInitializer(connectionFactory);

        await initializer.InitializeAsync();

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'tunnels';";

        var tableName = await command.ExecuteScalarAsync();

        Assert.Equal("tunnels", tableName);
    }

    [Fact]
    public async Task SaveTunnelAsync_ShouldPersistAndReadTunnel()
    {
        var service = CreateService();
        var expected = CreateTunnel("web-dev-portal", TunnelProtocol.Http);

        await service.SaveTunnelAsync(expected);

        var actual = await service.GetTunnelAsync(expected.Name);
        var tunnels = await service.ListTunnelsAsync();

        Assert.Equal(expected, actual);
        Assert.Single(tunnels);
        Assert.Equal(expected, tunnels[0]);
    }

    [Fact]
    public async Task DeleteTunnelAsync_ShouldRemoveTunnel()
    {
        var service = CreateService();
        var tunnel = CreateTunnel("待删除隧道", TunnelProtocol.Tcp);

        await service.SaveTunnelAsync(tunnel);
        await service.DeleteTunnelAsync(tunnel.Name);

        Assert.Null(await service.GetTunnelAsync(tunnel.Name));
        Assert.Empty(await service.ListTunnelsAsync());
    }

    [Fact]
    public async Task SaveTunnelAsync_ShouldSupportMvpProtocols()
    {
        var service = CreateService();
        var expectedProtocols = new[]
        {
            TunnelProtocol.Tcp,
            TunnelProtocol.Udp,
            TunnelProtocol.Http,
            TunnelProtocol.Https
        };

        foreach (var protocol in expectedProtocols)
        {
            await service.SaveTunnelAsync(CreateTunnel(protocol.ToString(), protocol));
        }

        var tunnels = await service.ListTunnelsAsync();

        foreach (var protocol in expectedProtocols)
        {
            Assert.Contains(tunnels, tunnel => tunnel.Protocol == protocol);
        }
    }

    private static SqliteTunnelManagementService CreateService()
    {
        var pathProvider = new TestDatabasePathProvider();
        var connectionFactory = new SqliteConnectionFactory(pathProvider);
        var initializer = new SqliteDatabaseInitializer(connectionFactory);

        return new SqliteTunnelManagementService(connectionFactory, initializer);
    }

    private static TunnelProfile CreateTunnel(string name, TunnelProtocol protocol)
    {
        return new TunnelProfile(
            name,
            protocol,
            "Node-Alpha-HK",
            "127.0.0.1",
            protocol == TunnelProtocol.Tcp ? 22 : 8080,
            protocol is TunnelProtocol.Http or TunnelProtocol.Https ? "dev.example.com" : "60022",
            FrpNexusStatus.Running,
            "运行中");
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
