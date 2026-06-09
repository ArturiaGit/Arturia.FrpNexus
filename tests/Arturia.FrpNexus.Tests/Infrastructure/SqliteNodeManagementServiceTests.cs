using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Nodes;
using Arturia.FrpNexus.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class SqliteNodeManagementServiceTests
{
    [Fact]
    public async Task InitializeAsync_ShouldCreateNodesTable()
    {
        var pathProvider = new TestDatabasePathProvider();
        var connectionFactory = new SqliteConnectionFactory(pathProvider);
        var initializer = new SqliteDatabaseInitializer(connectionFactory);

        await initializer.InitializeAsync();

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'nodes';";

        var tableName = await command.ExecuteScalarAsync();

        Assert.Equal("nodes", tableName);
    }

    [Fact]
    public async Task SaveNodeAsync_ShouldPersistAndReadNode()
    {
        var service = CreateService();
        var expected = CreateNode("东京-生产节点");

        await service.SaveNodeAsync(expected);

        var actual = await service.GetNodeAsync(expected.Name);
        var nodes = await service.ListNodesAsync();

        Assert.Equal(expected, actual);
        Assert.Single(nodes);
        Assert.Equal(expected, nodes[0]);
    }

    [Fact]
    public async Task DeleteNodeAsync_ShouldRemoveNode()
    {
        var service = CreateService();
        var node = CreateNode("待删除节点");

        await service.SaveNodeAsync(node);
        await service.DeleteNodeAsync(node.Name);

        Assert.Null(await service.GetNodeAsync(node.Name));
        Assert.Empty(await service.ListNodesAsync());
    }

    [Fact]
    public async Task UpdateConnectionTestResultAsync_ShouldPersistSafeMetadata()
    {
        var service = CreateService();
        var node = CreateNode("连接测试节点");
        var testedAt = DateTimeOffset.Parse("2026-06-04T12:00:00+00:00");

        await service.SaveNodeAsync(node);
        await service.UpdateConnectionTestResultAsync(node.Name, FrpNexusStatus.Online, testedAt);

        var actual = await service.GetNodeAsync(node.Name);

        Assert.NotNull(actual);
        Assert.Equal(FrpNexusStatus.Online, actual.ConnectionStatus);
        Assert.Equal(testedAt, actual.LastConnectionTestedAt);
        Assert.DoesNotContain("PASSWORD", actual.Authentication, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE_KEY_CONTENT", actual.Authentication, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateLastConnectionAsync_ShouldPersistTimestampOnly()
    {
        var service = CreateService();
        var node = CreateNode("最后连接节点") with
        {
            ConnectionStatus = FrpNexusStatus.Offline,
            LastConnectionTestedAt = DateTimeOffset.Parse("2026-06-01T08:00:00+00:00")
        };
        var connectedAt = DateTimeOffset.Parse("2026-06-05T12:34:56+00:00");

        await service.SaveNodeAsync(node);
        await service.UpdateLastConnectionAsync(node.Name, connectedAt);

        var actual = await service.GetNodeAsync(node.Name);

        Assert.NotNull(actual);
        Assert.Equal(FrpNexusStatus.Offline, actual.ConnectionStatus);
        Assert.Equal(connectedAt, actual.LastConnectionTestedAt);
        Assert.Equal(node.Authentication, actual.Authentication);
    }

    [Fact]
    public void NodeProfile_ShouldNotExposeSensitiveCredentialFields()
    {
        var properties = typeof(NodeProfile)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(properties, property => property.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Contains("PrivateKeyContent", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Contains("Passphrase", StringComparison.OrdinalIgnoreCase));
    }

    private static SqliteNodeManagementService CreateService()
    {
        var pathProvider = new TestDatabasePathProvider();
        var connectionFactory = new SqliteConnectionFactory(pathProvider);
        var initializer = new SqliteDatabaseInitializer(connectionFactory);

        return new SqliteNodeManagementService(connectionFactory, initializer);
    }

    private static NodeProfile CreateNode(string name)
    {
        return new NodeProfile(
            name,
            "203.0.113.10",
            22,
            "deploy",
            "密钥 (ID_RSA_HK)",
            "Ubuntu 22.04 LTS",
            FrpNexusStatus.Online,
            FrpNexusStatus.Running,
            "v0.61.1",
            "15 天 04:12",
            "/opt/frpnexus/frpc.toml");
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
