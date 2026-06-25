using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Serilog;
using Serilog.Core;
using Serilog.Events;
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
    public async Task InitializeAsync_ShouldLogWarningWhenDatabaseInitializationFails()
    {
        var sink = new CollectingLogSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(sink)
            .CreateLogger();
        var initializer = new SqliteDatabaseInitializer(new ThrowingConnectionFactory(), logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.InitializeAsync());

        var warning = Assert.Single(sink.Events, item => item.Level == LogEventLevel.Warning);
        Assert.Equal("SQLite database initialization failed", warning.MessageTemplate.Text);
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
    public async Task SaveRuntimeProcessAsync_ShouldRedactSecretsFromCommandLine()
    {
        var service = CreateService();
        var process = new RuntimeProcess(
            "frpc-secret",
            "Node-Alpha-HK",
            "frpc",
            FrpNexusStatus.Running,
            "2048",
            "2h",
            "127.0.0.1:8080",
            "/opt/frp/frpc --token SECRET_TOKEN --password=SECRET_PASSWORD --private-key-passphrase SECRET_PASSPHRASE -c /etc/frp/frpc.toml");

        await service.SaveRuntimeProcessAsync(process);

        var actual = await service.GetRuntimeProcessAsync(process.Name);

        Assert.NotNull(actual);
        Assert.DoesNotContain("SECRET_TOKEN", actual.CommandLine, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_PASSWORD", actual.CommandLine, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_PASSPHRASE", actual.CommandLine, StringComparison.Ordinal);
        Assert.Contains("--token [REDACTED]", actual.CommandLine, StringComparison.Ordinal);
        Assert.Contains("--password=[REDACTED]", actual.CommandLine, StringComparison.Ordinal);
        Assert.Contains("--private-key-passphrase [REDACTED]", actual.CommandLine, StringComparison.Ordinal);
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
    public async Task ListRuntimeProcessesAsync_ShouldRemoveLegacySampleProcessesOnly()
    {
        var service = CreateService();
        var realSameName = new RuntimeProcess(
            "frpc-web",
            "真实节点",
            "frpc",
            FrpNexusStatus.Running,
            "4096",
            "00:13",
            "127.0.0.1:8080");
        var realProcess = CreateProcess("frpc-real", FrpNexusStatus.Running);

        await service.SaveRuntimeProcessAsync(new RuntimeProcess("frps-main", "Web-Server-HK", "frps", FrpNexusStatus.Running, "14022", "4d 12h 30m", "0.0.0.0:7000"));
        await service.SaveRuntimeProcessAsync(new RuntimeProcess("frpc-web", "Web-Server-HK", "frpc", FrpNexusStatus.Running, "14090", "4d 10h 12m", "127.0.0.1:8080"));
        await service.SaveRuntimeProcessAsync(new RuntimeProcess("frpc-db", "DB-Node-SH", "frpc", FrpNexusStatus.Stopped, "-", "-", "127.0.0.1:3306"));
        await service.SaveRuntimeProcessAsync(new RuntimeProcess("frpc-edge", "Edge-Router-BJ", "frpc", FrpNexusStatus.Error, "-", "连接失败", "127.0.0.1:7777"));
        await service.SaveRuntimeProcessAsync(realSameName);
        await service.SaveRuntimeProcessAsync(realProcess);

        var processes = await service.ListRuntimeProcessesAsync();

        Assert.Equal(2, processes.Count);
        Assert.Contains(processes, process => process == realSameName);
        Assert.Contains(processes, process => process == realProcess);
        Assert.DoesNotContain(processes, process => process.NodeName is "Web-Server-HK" or "DB-Node-SH" or "Edge-Router-BJ");
    }

    [Fact]
    public async Task ReplaceRuntimeProcessesForNodeAsync_ShouldReplaceOnlyCurrentNodeFrpRecords()
    {
        var service = CreateService();

        await service.SaveRuntimeProcessAsync(new RuntimeProcess("frps-100", "Node-Alpha-HK", "frps", FrpNexusStatus.Running, "100", "10m", "-"));
        await service.SaveRuntimeProcessAsync(new RuntimeProcess("frpc-101", "Node-Alpha-HK", "frpc", FrpNexusStatus.Running, "101", "9m", "-"));
        await service.SaveRuntimeProcessAsync(new RuntimeProcess("ssh-keep", "Node-Alpha-HK", "ssh", FrpNexusStatus.Running, "102", "8m", "-"));
        await service.SaveRuntimeProcessAsync(new RuntimeProcess("frps-other", "Node-Beta-SG", "frps", FrpNexusStatus.Running, "200", "7m", "-"));

        await service.ReplaceRuntimeProcessesForNodeAsync(
            "Node-Alpha-HK",
            [new RuntimeProcess("frps-300", "Node-Alpha-HK", "frps", FrpNexusStatus.Running, "300", "1m", "-")]);

        var processes = await service.ListRuntimeProcessesAsync();

        Assert.DoesNotContain(processes, process => process.Name is "frps-100" or "frpc-101");
        Assert.Contains(processes, process => process.Name == "frps-300" && process.NodeName == "Node-Alpha-HK");
        Assert.Contains(processes, process => process.Name == "ssh-keep" && process.NodeName == "Node-Alpha-HK");
        Assert.Contains(processes, process => process.Name == "frps-other" && process.NodeName == "Node-Beta-SG");
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

    private sealed class ThrowingConnectionFactory : ISqliteConnectionFactory
    {
        public SqliteConnection CreateConnection()
        {
            throw new InvalidOperationException("database path unavailable");
        }
    }

    private sealed class CollectingLogSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }
}
