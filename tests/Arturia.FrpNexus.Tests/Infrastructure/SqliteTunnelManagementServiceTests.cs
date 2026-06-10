using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Persistence;
using Arturia.FrpNexus.Infrastructure.Tunnels;
using Microsoft.Data.Sqlite;

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
    public async Task InitializeAsync_ShouldCreateRemarkColumnWithoutStatusDetail()
    {
        var pathProvider = new TestDatabasePathProvider();
        var connectionFactory = new SqliteConnectionFactory(pathProvider);
        var initializer = new SqliteDatabaseInitializer(connectionFactory);

        await initializer.InitializeAsync();

        var columns = await ReadColumnsAsync(connectionFactory);

        Assert.Contains("remark", columns);
        Assert.DoesNotContain("status_detail", columns);
    }

    [Fact]
    public async Task InitializeAsync_ShouldMigrateLegacyStatusDetailToEmptyRemark()
    {
        var pathProvider = new TestDatabasePathProvider();
        var connectionFactory = new SqliteConnectionFactory(pathProvider);
        await CreateLegacyTunnelTableAsync(connectionFactory);
        var initializer = new SqliteDatabaseInitializer(connectionFactory);

        await initializer.InitializeAsync();

        var columns = await ReadColumnsAsync(connectionFactory);
        var service = new SqliteTunnelManagementService(connectionFactory, initializer);
        var tunnel = await service.GetTunnelAsync("legacy-web");

        Assert.Contains("remark", columns);
        Assert.DoesNotContain("status_detail", columns);
        Assert.NotNull(tunnel);
        Assert.Equal(string.Empty, tunnel.Remark);
        Assert.Equal(FrpNexusStatus.Error, tunnel.Status);
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
        Assert.Equal("长期备注", tunnels[0].Remark);
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
            "长期备注");
    }

    private static async Task<IReadOnlyList<string>> ReadColumnsAsync(SqliteConnectionFactory connectionFactory)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(tunnels);";

        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static async Task CreateLegacyTunnelTableAsync(SqliteConnectionFactory connectionFactory)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE tunnels (
                name TEXT PRIMARY KEY NOT NULL,
                protocol TEXT NOT NULL,
                node_name TEXT NOT NULL,
                local_address TEXT NOT NULL,
                local_port INTEGER NOT NULL,
                remote_endpoint TEXT NOT NULL,
                status TEXT NOT NULL,
                status_detail TEXT NOT NULL
            );

            INSERT INTO tunnels (
                name,
                protocol,
                node_name,
                local_address,
                local_port,
                remote_endpoint,
                status,
                status_detail
            )
            VALUES (
                'legacy-web',
                'Http',
                'Node-Alpha-HK',
                '127.0.0.1',
                8080,
                'legacy.example.com',
                'Error',
                '端口占用'
            );
            """;
        await command.ExecuteNonQueryAsync();
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
