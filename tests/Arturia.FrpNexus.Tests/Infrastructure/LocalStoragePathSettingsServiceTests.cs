using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Infrastructure.Settings;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class LocalStoragePathSettingsServiceTests
{
    [Fact]
    public void GetSettings_ShouldReturnDefaultsWhenPathFileDoesNotExist()
    {
        var root = CreateTempRoot();
        var service = new JsonLocalStoragePathSettingsService(
            Path.Combine(root, "paths.json"),
            root);

        var settings = service.GetSettings();

        Assert.Equal(Path.Combine(root, "logs"), settings.LogDirectory);
        Assert.Equal(Path.Combine(root, "data"), settings.SqliteDatabaseDirectory);
        Assert.Equal(Path.Combine(root, "data", "frpnexus.db"), service.GetSqliteDatabasePath());
    }

    [Fact]
    public async Task SaveSettingsAsync_ShouldPersistLogAndSqliteDirectoriesOutsideSqlite()
    {
        var root = CreateTempRoot();
        var service = new JsonLocalStoragePathSettingsService(
            Path.Combine(root, "paths.json"),
            root);
        var expectedLogDirectory = Path.Combine(root, "custom-logs");
        var expectedDataDirectory = Path.Combine(root, "custom-data");

        await service.SaveSettingsAsync(new LocalStoragePathSettings(
            expectedLogDirectory,
            expectedDataDirectory));

        var actual = service.GetSettings();
        Assert.Equal(expectedLogDirectory, actual.LogDirectory);
        Assert.Equal(expectedDataDirectory, actual.SqliteDatabaseDirectory);
        Assert.Equal(Path.Combine(expectedDataDirectory, "frpnexus.db"), service.GetSqliteDatabasePath());
    }

    [Fact]
    public async Task PrepareSqliteDatabaseDirectoryAsync_ShouldCopyCurrentDatabaseAndKeepOldFile()
    {
        var root = CreateTempRoot();
        var sourceDirectory = Path.Combine(root, "data");
        Directory.CreateDirectory(sourceDirectory);
        var sourceDatabase = Path.Combine(sourceDirectory, "frpnexus.db");
        await File.WriteAllTextAsync(sourceDatabase, "current database");
        var targetDirectory = Path.Combine(root, "moved-data");
        var service = new JsonLocalStoragePathSettingsService(
            Path.Combine(root, "paths.json"),
            root);

        var result = await service.PrepareSqliteDatabaseDirectoryAsync(sourceDatabase, targetDirectory);

        Assert.True(result.Copied);
        Assert.Equal(sourceDatabase, result.SourceDatabasePath);
        Assert.Equal(Path.Combine(targetDirectory, "frpnexus.db"), result.TargetDatabasePath);
        Assert.True(File.Exists(sourceDatabase));
        Assert.Equal("current database", await File.ReadAllTextAsync(result.TargetDatabasePath));
        Assert.Equal(targetDirectory, service.GetSettings().SqliteDatabaseDirectory);
    }

    [Fact]
    public async Task PrepareSqliteDatabaseDirectoryAsync_ShouldBackUpExistingTargetBeforeCopy()
    {
        var root = CreateTempRoot();
        var sourceDirectory = Path.Combine(root, "data");
        var targetDirectory = Path.Combine(root, "moved-data");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(targetDirectory);
        var sourceDatabase = Path.Combine(sourceDirectory, "frpnexus.db");
        var targetDatabase = Path.Combine(targetDirectory, "frpnexus.db");
        await File.WriteAllTextAsync(sourceDatabase, "current database");
        await File.WriteAllTextAsync(targetDatabase, "old target database");
        var service = new JsonLocalStoragePathSettingsService(
            Path.Combine(root, "paths.json"),
            root);

        var result = await service.PrepareSqliteDatabaseDirectoryAsync(sourceDatabase, targetDirectory);

        Assert.True(result.BackupCreated);
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));
        Assert.Equal("old target database", await File.ReadAllTextAsync(result.BackupPath!));
        Assert.Equal("current database", await File.ReadAllTextAsync(targetDatabase));
    }

    [Fact]
    public async Task PrepareLogDirectoryAsync_ShouldCopyOnlyFrpNexusLogsAndKeepOldFiles()
    {
        var root = CreateTempRoot();
        var sourceDirectory = Path.Combine(root, "logs");
        var targetDirectory = Path.Combine(root, "moved-logs");
        Directory.CreateDirectory(sourceDirectory);
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "frpnexus-20260621.log"), "warning log");
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "other.log"), "not ours");
        var service = new JsonLocalStoragePathSettingsService(
            Path.Combine(root, "paths.json"),
            root);

        var result = await service.PrepareLogDirectoryAsync(sourceDirectory, targetDirectory);

        Assert.Equal(1, result.CopiedFileCount);
        Assert.Equal(0, result.SkippedFileCount);
        Assert.Equal(targetDirectory, result.TargetLogDirectory);
        Assert.True(File.Exists(Path.Combine(sourceDirectory, "frpnexus-20260621.log")));
        Assert.Equal("warning log", await File.ReadAllTextAsync(Path.Combine(targetDirectory, "frpnexus-20260621.log")));
        Assert.False(File.Exists(Path.Combine(targetDirectory, "other.log")));
    }

    [Fact]
    public async Task PrepareLogDirectoryAsync_ShouldSkipExistingTargetLogWithoutOverwrite()
    {
        var root = CreateTempRoot();
        var sourceDirectory = Path.Combine(root, "logs");
        var targetDirectory = Path.Combine(root, "moved-logs");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(targetDirectory);
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "frpnexus-20260621.log"), "source log");
        await File.WriteAllTextAsync(Path.Combine(targetDirectory, "frpnexus-20260621.log"), "existing target");
        var service = new JsonLocalStoragePathSettingsService(
            Path.Combine(root, "paths.json"),
            root);

        var result = await service.PrepareLogDirectoryAsync(sourceDirectory, targetDirectory);

        Assert.Equal(0, result.CopiedFileCount);
        Assert.Equal(1, result.SkippedFileCount);
        Assert.Equal("existing target", await File.ReadAllTextAsync(Path.Combine(targetDirectory, "frpnexus-20260621.log")));
    }

    [Fact]
    public async Task PrepareLogDirectoryAsync_ShouldReturnZeroWhenSourceDirectoryDoesNotExist()
    {
        var root = CreateTempRoot();
        var sourceDirectory = Path.Combine(root, "missing-logs");
        var targetDirectory = Path.Combine(root, "moved-logs");
        var service = new JsonLocalStoragePathSettingsService(
            Path.Combine(root, "paths.json"),
            root);

        var result = await service.PrepareLogDirectoryAsync(sourceDirectory, targetDirectory);

        Assert.Equal(0, result.CopiedFileCount);
        Assert.Equal(0, result.SkippedFileCount);
        Assert.Equal(targetDirectory, result.TargetLogDirectory);
    }

    private static string CreateTempRoot()
    {
        return Path.Combine(Path.GetTempPath(), "FrpNexusTests", Guid.NewGuid().ToString("N"));
    }
}
