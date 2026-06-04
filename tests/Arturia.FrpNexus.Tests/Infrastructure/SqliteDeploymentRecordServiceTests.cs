using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Deployments;
using Arturia.FrpNexus.Infrastructure.Persistence;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class SqliteDeploymentRecordServiceTests
{
    [Fact]
    public async Task InitializeAsync_ShouldCreateDeploymentRecordsTable()
    {
        var pathProvider = new TestDatabasePathProvider();
        var connectionFactory = new SqliteConnectionFactory(pathProvider);
        var initializer = new SqliteDatabaseInitializer(connectionFactory);

        await initializer.InitializeAsync();

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'deployment_records';";

        var tableName = await command.ExecuteScalarAsync();

        Assert.Equal("deployment_records", tableName);
    }

    [Fact]
    public async Task SaveDeploymentRecordAsync_ShouldPersistAndReadRecord()
    {
        var service = CreateService();
        var expected = CreateRecord("生成 TOML", FrpNexusStatus.Ready);

        await service.SaveDeploymentRecordAsync(expected);

        var actual = await service.GetDeploymentRecordAsync(expected.StepName);
        var records = await service.ListDeploymentRecordsAsync();

        Assert.Equal(expected, actual);
        Assert.Single(records);
        Assert.Equal(expected, records[0]);
    }

    [Fact]
    public async Task DeleteDeploymentRecordAsync_ShouldRemoveRecord()
    {
        var service = CreateService();
        var record = CreateRecord("待删除部署步骤", FrpNexusStatus.Pending);

        await service.SaveDeploymentRecordAsync(record);
        await service.DeleteDeploymentRecordAsync(record.StepName);

        Assert.Null(await service.GetDeploymentRecordAsync(record.StepName));
        Assert.Empty(await service.ListDeploymentRecordsAsync());
    }

    [Fact]
    public void DeploymentRecord_ShouldNotExposeSensitiveCredentialFields()
    {
        var properties = typeof(DeploymentRecord)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(properties, property => property.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Contains("PrivateKeyContent", StringComparison.OrdinalIgnoreCase));
    }

    private static SqliteDeploymentRecordService CreateService()
    {
        var pathProvider = new TestDatabasePathProvider();
        var connectionFactory = new SqliteConnectionFactory(pathProvider);
        var initializer = new SqliteDatabaseInitializer(connectionFactory);

        return new SqliteDeploymentRecordService(connectionFactory, initializer);
    }

    private static DeploymentRecord CreateRecord(string stepName, FrpNexusStatus status)
    {
        return new DeploymentRecord(
            stepName,
            "Node-Alpha-HK",
            "本地部署步骤记录，不包含凭据内容。",
            status,
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
