using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Persistence;

namespace Arturia.FrpNexus.Infrastructure.Runtime;

public sealed class SqliteRuntimeRecordService(
    ISqliteConnectionFactory connectionFactory,
    ISqliteDatabaseInitializer databaseInitializer) : IRuntimeRecordService
{
    public async Task<IReadOnlyList<RuntimeProcess>> ListRuntimeProcessesAsync(CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await DeleteLegacySampleProcessesAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name,
                   node_name,
                   process_kind,
                   status,
                   process_id,
                   uptime,
                   listen_address
            FROM runtime_processes
            ORDER BY node_name, name;
            """;

        var processes = new List<RuntimeProcess>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            processes.Add(ReadProcess(reader));
        }

        return processes;
    }

    public async Task<RuntimeProcess?> GetRuntimeProcessAsync(string processName, CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name,
                   node_name,
                   process_kind,
                   status,
                   process_id,
                   uptime,
                   listen_address
            FROM runtime_processes
            WHERE name = $name;
            """;
        command.Parameters.AddWithValue("$name", processName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadProcess(reader)
            : null;
    }

    public async Task SaveRuntimeProcessAsync(RuntimeProcess process, CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO runtime_processes (
                name,
                node_name,
                process_kind,
                status,
                process_id,
                uptime,
                listen_address
            )
            VALUES (
                $name,
                $node_name,
                $process_kind,
                $status,
                $process_id,
                $uptime,
                $listen_address
            )
            ON CONFLICT(name) DO UPDATE SET
                node_name = excluded.node_name,
                process_kind = excluded.process_kind,
                status = excluded.status,
                process_id = excluded.process_id,
                uptime = excluded.uptime,
                listen_address = excluded.listen_address;
            """;

        command.Parameters.AddWithValue("$name", process.Name);
        command.Parameters.AddWithValue("$node_name", process.NodeName);
        command.Parameters.AddWithValue("$process_kind", process.ProcessKind);
        command.Parameters.AddWithValue("$status", process.Status.ToString());
        command.Parameters.AddWithValue("$process_id", process.ProcessId);
        command.Parameters.AddWithValue("$uptime", process.Uptime);
        command.Parameters.AddWithValue("$listen_address", process.ListenAddress);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteRuntimeProcessAsync(string processName, CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM runtime_processes WHERE name = $name;";
        command.Parameters.AddWithValue("$name", processName);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteLegacySampleProcessesAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM runtime_processes
            WHERE (name = 'frps-main' AND node_name = 'Web-Server-HK')
               OR (name = 'frpc-web' AND node_name = 'Web-Server-HK')
               OR (name = 'frpc-db' AND node_name = 'DB-Node-SH')
               OR (name = 'frpc-edge' AND node_name = 'Edge-Router-BJ');
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static RuntimeProcess ReadProcess(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new RuntimeProcess(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            ParseStatus(reader.GetString(3)),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6));
    }

    private static FrpNexusStatus ParseStatus(string value)
    {
        return Enum.TryParse<FrpNexusStatus>(value, ignoreCase: true, out var status)
            ? status
            : FrpNexusStatus.Pending;
    }
}
