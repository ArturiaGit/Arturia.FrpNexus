using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Persistence;

namespace Arturia.FrpNexus.Infrastructure.Deployments;

public sealed class SqliteDeploymentRecordService(
    ISqliteConnectionFactory connectionFactory,
    ISqliteDatabaseInitializer databaseInitializer) : IDeploymentRecordService
{
    public async Task<IReadOnlyList<DeploymentRecord>> ListDeploymentRecordsAsync(CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT step_name,
                   node_name,
                   description,
                   status,
                   updated_at
            FROM deployment_records
            ORDER BY updated_at DESC, step_name;
            """;

        var records = new List<DeploymentRecord>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(ReadRecord(reader));
        }

        return records;
    }

    public async Task<DeploymentRecord?> GetDeploymentRecordAsync(string stepName, CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT step_name,
                   node_name,
                   description,
                   status,
                   updated_at
            FROM deployment_records
            WHERE step_name = $step_name;
            """;
        command.Parameters.AddWithValue("$step_name", stepName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadRecord(reader)
            : null;
    }

    public async Task SaveDeploymentRecordAsync(DeploymentRecord record, CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO deployment_records (
                step_name,
                node_name,
                description,
                status,
                updated_at
            )
            VALUES (
                $step_name,
                $node_name,
                $description,
                $status,
                $updated_at
            )
            ON CONFLICT(step_name) DO UPDATE SET
                node_name = excluded.node_name,
                description = excluded.description,
                status = excluded.status,
                updated_at = excluded.updated_at;
            """;

        command.Parameters.AddWithValue("$step_name", record.StepName);
        command.Parameters.AddWithValue("$node_name", record.NodeName);
        command.Parameters.AddWithValue("$description", record.Description);
        command.Parameters.AddWithValue("$status", record.Status.ToString());
        command.Parameters.AddWithValue("$updated_at", record.UpdatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteDeploymentRecordAsync(string stepName, CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM deployment_records WHERE step_name = $step_name;";
        command.Parameters.AddWithValue("$step_name", stepName);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DeploymentRecord ReadRecord(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new DeploymentRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            ParseStatus(reader.GetString(3)),
            ParseUpdatedAt(reader.GetString(4)));
    }

    private static FrpNexusStatus ParseStatus(string value)
    {
        return Enum.TryParse<FrpNexusStatus>(value, ignoreCase: true, out var status)
            ? status
            : FrpNexusStatus.Pending;
    }

    private static DateTimeOffset ParseUpdatedAt(string value)
    {
        return DateTimeOffset.TryParse(value, out var updatedAt)
            ? updatedAt
            : DateTimeOffset.UnixEpoch;
    }
}
