using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Infrastructure.Persistence;

namespace Arturia.FrpNexus.Infrastructure.Settings;

public sealed class SqliteOnboardingStateService(
    ISqliteConnectionFactory connectionFactory,
    ISqliteDatabaseInitializer databaseInitializer) : IOnboardingStateService
{
    private const string AcceptedDisclaimerVersionKey = "onboarding_disclaimer_accepted_version";
    private const string AcceptedDisclaimerAtKey = "onboarding_disclaimer_accepted_at";

    public async Task<OnboardingStateSnapshot> GetStateAsync(CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT key, value
            FROM settings
            WHERE key IN ($acceptedVersionKey, $acceptedAtKey);
            """;
        command.Parameters.AddWithValue("$acceptedVersionKey", AcceptedDisclaimerVersionKey);
        command.Parameters.AddWithValue("$acceptedAtKey", AcceptedDisclaimerAtKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values[reader.GetString(0)] = reader.GetString(1);
        }

        return new OnboardingStateSnapshot(
            OnboardingDisclaimer.CurrentVersion,
            GetNonEmptyValue(values, AcceptedDisclaimerVersionKey),
            ParseAcceptedAt(GetNonEmptyValue(values, AcceptedDisclaimerAtKey)));
    }

    public Task AcceptCurrentDisclaimerAsync(
        DateTimeOffset acceptedAt,
        CancellationToken cancellationToken = default)
    {
        return AcceptDisclaimerVersionAsync(
            OnboardingDisclaimer.CurrentVersion,
            acceptedAt,
            cancellationToken);
    }

    public async Task AcceptDisclaimerVersionAsync(
        string disclaimerVersion,
        DateTimeOffset acceptedAt,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await UpsertAsync(connection, AcceptedDisclaimerVersionKey, disclaimerVersion, cancellationToken);
        await UpsertAsync(connection, AcceptedDisclaimerAtKey, acceptedAt.ToUniversalTime().ToString("O"), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static string? GetNonEmptyValue(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static DateTimeOffset? ParseAcceptedAt(string? value)
    {
        return DateTimeOffset.TryParse(value, out var acceptedAt)
            ? acceptedAt
            : null;
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
