using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Infrastructure.Persistence;

namespace Arturia.FrpNexus.Infrastructure.Settings;

public sealed class SqliteSettingsService(
    ISqliteConnectionFactory connectionFactory,
    ISqliteDatabaseInitializer databaseInitializer,
    IFrpNexusDatabasePathProvider databasePathProvider) : ISettingsService
{
    private const string FrpDownloadSourceKey = "frp_download_source";
    private const string CustomFrpDownloadSourceUrlKey = "frp_custom_download_source_url";
    private const string LogDirectoryKey = "log_directory";

    public async Task<FrpNexusSettingsSnapshot> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT key, value FROM settings;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values[reader.GetString(0)] = reader.GetString(1);
        }

        var defaults = CreateDefaultSettings();

        return defaults with
        {
            FrpDownloadSource = GetValue(values, FrpDownloadSourceKey, defaults.FrpDownloadSource),
            CustomFrpDownloadSourceUrl = GetValue(values, CustomFrpDownloadSourceUrlKey, defaults.CustomFrpDownloadSourceUrl),
            LogDirectory = GetValue(values, LogDirectoryKey, defaults.LogDirectory),
            SqliteDatabasePath = databasePathProvider.GetDatabasePath()
        };
    }

    public async Task SaveSettingsAsync(FrpNexusSettingsSnapshot settings, CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await UpsertAsync(connection, FrpDownloadSourceKey, settings.FrpDownloadSource, cancellationToken);
        await UpsertAsync(connection, CustomFrpDownloadSourceUrlKey, settings.CustomFrpDownloadSourceUrl, cancellationToken);
        await UpsertAsync(connection, LogDirectoryKey, settings.LogDirectory, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public FrpNexusSettingsSnapshot CreateDefaultSettings()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = Path.Combine(localAppData, "Arturia", "FrpNexus");

        return new FrpNexusSettingsSnapshot(
            "GitHub Releases",
            Path.Combine(root, "logs"),
            databasePathProvider.GetDatabasePath(),
            string.Empty);
    }

    private static string GetValue(IReadOnlyDictionary<string, string> values, string key, string defaultValue)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;
    }

    private static async Task UpsertAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string key,
        string value,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO settings (key, value)
            VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
