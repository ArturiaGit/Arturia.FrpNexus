using System.Text.Json;
using Arturia.FrpNexus.Application.Abstractions;

namespace Arturia.FrpNexus.Infrastructure.Settings;

public sealed class JsonLocalStoragePathSettingsService : ILocalStoragePathSettingsService
{
    private const string DatabaseFileName = "frpnexus.db";
    private const string LogFilePattern = "frpnexus-*.log";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsFilePath;
    private readonly string _defaultRootDirectory;

    public JsonLocalStoragePathSettingsService()
        : this(null, null)
    {
    }

    public JsonLocalStoragePathSettingsService(
        string? settingsFilePath,
        string? defaultRootDirectory)
    {
        _defaultRootDirectory = string.IsNullOrWhiteSpace(defaultRootDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Arturia",
                "FrpNexus")
            : defaultRootDirectory;
        _settingsFilePath = string.IsNullOrWhiteSpace(settingsFilePath)
            ? Path.Combine(_defaultRootDirectory, "paths.json")
            : settingsFilePath;
    }

    public LocalStoragePathSettings GetSettings()
    {
        var defaults = CreateDefaultSettings();
        if (!File.Exists(_settingsFilePath))
        {
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            var stored = JsonSerializer.Deserialize<LocalStoragePathSettings>(json, JsonOptions);
            if (stored is null)
            {
                return defaults;
            }

            return new LocalStoragePathSettings(
                NormalizeDirectoryOrDefault(stored.LogDirectory, defaults.LogDirectory),
                NormalizeDirectoryOrDefault(stored.SqliteDatabaseDirectory, defaults.SqliteDatabaseDirectory));
        }
        catch
        {
            return defaults;
        }
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
        return Path.Combine(GetSqliteDatabaseDirectory(), DatabaseFileName);
    }

    public async Task SaveSettingsAsync(
        LocalStoragePathSettings pathSettings,
        CancellationToken cancellationToken = default)
    {
        var normalized = new LocalStoragePathSettings(
            NormalizeRequiredDirectory(pathSettings.LogDirectory, "日志目录不能为空。"),
            NormalizeRequiredDirectory(pathSettings.SqliteDatabaseDirectory, "SQLite 数据库目录不能为空。"));

        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken);
    }

    public async Task<SqliteDatabaseRelocationResult> PrepareSqliteDatabaseDirectoryAsync(
        string currentDatabasePath,
        string targetDatabaseDirectory,
        CancellationToken cancellationToken = default)
    {
        var targetDirectory = NormalizeRequiredDirectory(
            targetDatabaseDirectory,
            "SQLite 数据库目录不能为空。");
        Directory.CreateDirectory(targetDirectory);

        var sourceDatabasePath = Path.GetFullPath(currentDatabasePath);
        var targetDatabasePath = Path.Combine(targetDirectory, DatabaseFileName);
        var copied = false;
        var backupCreated = false;
        string? backupPath = null;

        if (File.Exists(sourceDatabasePath)
            && !string.Equals(sourceDatabasePath, Path.GetFullPath(targetDatabasePath), StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(targetDatabasePath))
            {
                backupPath = Path.Combine(
                    targetDirectory,
                    $"{DatabaseFileName}.bak-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}");
                File.Move(targetDatabasePath, backupPath);
                backupCreated = true;
            }

            await using var source = File.Open(sourceDatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            await using var target = File.Create(targetDatabasePath);
            await source.CopyToAsync(target, cancellationToken);
            copied = true;
        }

        var current = GetSettings();
        await SaveSettingsAsync(current with
        {
            SqliteDatabaseDirectory = targetDirectory
        }, cancellationToken);

        return new SqliteDatabaseRelocationResult(
            sourceDatabasePath,
            targetDatabasePath,
            copied,
            backupCreated,
            backupPath);
    }

    public async Task<LogDirectoryRelocationResult> PrepareLogDirectoryAsync(
        string currentLogDirectory,
        string targetLogDirectory,
        CancellationToken cancellationToken = default)
    {
        var sourceDirectory = NormalizeRequiredDirectory(currentLogDirectory, "日志目录不能为空。");
        var targetDirectory = NormalizeRequiredDirectory(targetLogDirectory, "日志目录不能为空。");
        var copiedFileCount = 0;
        var skippedFileCount = 0;

        if (PathEquals(sourceDirectory, targetDirectory))
        {
            return new LogDirectoryRelocationResult(sourceDirectory, targetDirectory, 0, 0);
        }

        if (Directory.Exists(sourceDirectory))
        {
            Directory.CreateDirectory(targetDirectory);

            foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory, LogFilePattern, SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var targetFile = Path.Combine(targetDirectory, Path.GetFileName(sourceFile));
                if (File.Exists(targetFile))
                {
                    skippedFileCount++;
                    continue;
                }

                await using var source = File.Open(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                await using var target = File.Create(targetFile);
                await source.CopyToAsync(target, cancellationToken);
                copiedFileCount++;
            }
        }

        var current = GetSettings();
        await SaveSettingsAsync(current with
        {
            LogDirectory = targetDirectory
        }, cancellationToken);

        return new LogDirectoryRelocationResult(
            sourceDirectory,
            targetDirectory,
            copiedFileCount,
            skippedFileCount);
    }

    private LocalStoragePathSettings CreateDefaultSettings()
    {
        return new LocalStoragePathSettings(
            Path.Combine(_defaultRootDirectory, "logs"),
            Path.Combine(_defaultRootDirectory, "data"));
    }

    private static string NormalizeDirectoryOrDefault(string value, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : Path.GetFullPath(value.Trim());
    }

    private static string NormalizeRequiredDirectory(string value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return Path.GetFullPath(value.Trim());
    }

    private static bool PathEquals(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }
}
