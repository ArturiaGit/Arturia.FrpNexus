namespace Arturia.FrpNexus.Application.Abstractions;

public interface ILocalStoragePathSettingsService
{
    LocalStoragePathSettings GetSettings();

    string GetLogDirectory();

    string GetSqliteDatabaseDirectory();

    string GetSqliteDatabasePath();

    Task SaveSettingsAsync(
        LocalStoragePathSettings pathSettings,
        CancellationToken cancellationToken = default);

    Task<SqliteDatabaseRelocationResult> PrepareSqliteDatabaseDirectoryAsync(
        string currentDatabasePath,
        string targetDatabaseDirectory,
        CancellationToken cancellationToken = default);

    Task<LogDirectoryRelocationResult> PrepareLogDirectoryAsync(
        string currentLogDirectory,
        string targetLogDirectory,
        CancellationToken cancellationToken = default);
}

public sealed record LocalStoragePathSettings(
    string LogDirectory,
    string SqliteDatabaseDirectory);

public sealed record SqliteDatabaseRelocationResult(
    string SourceDatabasePath,
    string TargetDatabasePath,
    bool Copied,
    bool BackupCreated,
    string? BackupPath);

public sealed record LogDirectoryRelocationResult(
    string SourceLogDirectory,
    string TargetLogDirectory,
    int CopiedFileCount,
    int SkippedFileCount);
