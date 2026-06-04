using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Persistence;
using Arturia.FrpNexus.Infrastructure.Runtime;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class SqliteRuntimeRecordServiceTests
{
    [Fact]
    public async Task InitializeAsync_ShouldCreateRuntimeProcessesTable()
    {
        var pathProvider = new TestDatabasePathProvider();
        var connectionFactory = new SqliteConnectionFactory(pathProvider);
        var initializer = new SqliteDatabaseInitializer(connectionFactory);

        await initializer.InitializeAsync();

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'runtime_processes';";

        var tableName = await command.ExecuteScalarAsync();

        Assert.Equal("runtime_processes", tableName);
    }

    [Fact]
    public async Task SaveRuntimeProcessAsync_ShouldPersistAndReadProcess()
    {
        var service = CreateService();
        var expected = CreateProcess("frpc-web", FrpNexusStatus.Running);

        await service.SaveRuntimeProcessAsync(expected);

        var actual = await service.GetRuntimeProcessAsync(expected.Name);
        var processes = await service.ListRuntimeProcessesAsync();

        Assert.Equal(expected, actual);
        Assert.Single(processes);
        Assert.Equal(expected, processes[0]);
    }

    [Fact]
    public async Task DeleteRuntimeProcessAsync_ShouldRemoveProcess()
    {
        var service = CreateService();
        var process = CreateProcess("待删除进程", FrpNexusStatus.Stopped);

        await service.SaveRuntimeProcessAsync(process);
        await service.DeleteRuntimeProcessAsync(process.Name);

        Assert.Null(await service.GetRuntimeProcessAsync(process.Name));
        Assert.Empty(await service.ListRuntimeProcessesAsync());
    }

    [Fact]
    public void RuntimeProcess_ShouldNotExposeSensitiveCredentialFields()
    {
        var properties = typeof(RuntimeProcess)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(properties, property => property.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Contains("PrivateKeyContent", StringComparison.OrdinalIgnoreCase));
    }

    private static SqliteRuntimeRecordService CreateService()
    {
        var pathProvider = new TestDatabasePathProvider();
        var connectionFactory = new SqliteConnectionFactory(pathProvider);
        var initializer = new SqliteDatabaseInitializer(connectionFactory);

        return new SqliteRuntimeRecordService(connectionFactory, initializer);
    }

    private static RuntimeProcess CreateProcess(string name, FrpNexusStatus status)
    {
        return new RuntimeProcess(
            name,
            "Node-Alpha-HK",
            "frpc",
            status,
            status == FrpNexusStatus.Running ? "2048" : "-",
            status == FrpNexusStatus.Running ? "2h" : "-",
            "127.0.0.1:8080");
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
