using Arturia.FrpNexus.Cli.Commands;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Configuration;
using Arturia.FrpNexus.Infrastructure.Configuration;
using Arturia.FrpNexus.Infrastructure.Persistence;
using Arturia.FrpNexus.Infrastructure.Settings;
using Microsoft.Data.Sqlite;

namespace Arturia.FrpNexus.Tests.Cli;

public sealed class ConfigCommandsTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "FrpNexusTests", Guid.NewGuid().ToString("N"));
    private readonly string databasePath;

    public ConfigCommandsTests()
    {
        databasePath = Path.Combine(tempDirectory, "frpnexus.db");
    }

    [Fact]
    public async Task Show_WithDefaultSettings_WritesDefaultValues()
    {
        var commands = new ConfigCommands(CreateStore());

        var output = await ConsoleCapture.CaptureAsync(() => commands.Show());

        Assert.Contains("FrpNexus 配置", output);
        Assert.Contains("version: 1", output);
        Assert.Contains("frpc-path: 未设置", output);
    }

    [Fact]
    public async Task GetFrpcPath_WhenUnset_WritesClearMessage()
    {
        var commands = new ConfigCommands(CreateStore());

        var result = await ConsoleCapture.CaptureAsync(() => commands.Get("frpc-path"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("frpc-path 未设置", result.Output);
    }

    [Fact]
    public async Task SetFrpcPath_SavesValueForLaterRead()
    {
        var store = CreateStore();
        var commands = new ConfigCommands(store);

        var result = await ConsoleCapture.CaptureAsync(() => commands.Set("frpc-path", "/tmp/frpc"));
        var settings = await store.LoadAsync();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("/tmp/frpc", settings.FrpcPath);
        Assert.Contains("已保存 frpc-path", result.Output);
    }

    [Fact]
    public async Task SetFrpcPath_WithBlankPath_Fails()
    {
        var commands = new ConfigCommands(CreateStore());

        var result = await ConsoleCapture.CaptureAsync(() => commands.Set("frpc-path", " "));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("frpc-path 不能为空", result.Output);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private SqliteFrpNexusSettingsStore CreateStore()
    {
        var pathProvider = new TestDatabasePathProvider(databasePath);
        var connectionFactory = new SqliteConnectionFactory(pathProvider);
        var initializer = new SqliteDatabaseInitializer(connectionFactory);
        var pathSettings = new FakeLocalStoragePathSettingsService(databasePath);
        var settingsService = new SqliteSettingsService(connectionFactory, initializer, pathProvider, pathSettings);
        var frpcConfigurationService = new SqliteLocalFrpcConfigurationService(connectionFactory, initializer);

        return new SqliteFrpNexusSettingsStore(settingsService, frpcConfigurationService);
    }

    private sealed class TestDatabasePathProvider(string path) : Arturia.FrpNexus.Infrastructure.Persistence.IFrpNexusDatabasePathProvider
    {
        public string GetDatabasePath()
        {
            return path;
        }
    }

    private sealed class FakeLocalStoragePathSettingsService(string databasePath) : ILocalStoragePathSettingsService
    {
        public LocalStoragePathSettings GetSettings()
        {
            return new LocalStoragePathSettings(
                Path.Combine(Path.GetDirectoryName(databasePath)!, "logs"),
                Path.GetDirectoryName(databasePath)!);
        }

        public string GetLogDirectory()
        {
            return GetSettings().LogDirectory;
        }

        public string GetSqliteDatabaseDirectory()
        {
            return GetSettings().SqliteDatabaseDirectory;
        }

        public string GetSqliteDatabasePath()
        {
            return databasePath;
        }

        public Task SaveSettingsAsync(
            LocalStoragePathSettings pathSettings,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<SqliteDatabaseRelocationResult> PrepareSqliteDatabaseDirectoryAsync(
            string currentDatabasePath,
            string targetDatabaseDirectory,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SqliteDatabaseRelocationResult(
                currentDatabasePath,
                Path.Combine(targetDatabaseDirectory, "frpnexus.db"),
                false,
                false,
                null));
        }

        public Task<LogDirectoryRelocationResult> PrepareLogDirectoryAsync(
            string currentLogDirectory,
            string targetLogDirectory,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LogDirectoryRelocationResult(
                currentLogDirectory,
                targetLogDirectory,
                0,
                0));
        }
    }
}
