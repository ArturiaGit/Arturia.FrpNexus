namespace Arturia.FrpNexus.Infrastructure.Persistence;

public interface ISqliteDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public sealed class SqliteDatabaseInitializer(ISqliteConnectionFactory connectionFactory) : ISqliteDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY NOT NULL,
                value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS nodes (
                name TEXT PRIMARY KEY NOT NULL,
                host TEXT NOT NULL,
                ssh_port INTEGER NOT NULL,
                user_name TEXT NOT NULL,
                authentication TEXT NOT NULL,
                operating_system TEXT NOT NULL,
                connection_status TEXT NOT NULL,
                frp_status TEXT NOT NULL,
                frp_version TEXT NOT NULL,
                uptime TEXT NOT NULL,
                config_path TEXT NOT NULL,
                last_connection_tested_at TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS tunnels (
                name TEXT PRIMARY KEY NOT NULL,
                protocol TEXT NOT NULL,
                node_name TEXT NOT NULL,
                local_address TEXT NOT NULL,
                local_port INTEGER NOT NULL,
                remote_endpoint TEXT NOT NULL,
                status TEXT NOT NULL,
                remark TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS configuration_versions (
                name TEXT PRIMARY KEY NOT NULL,
                protocol TEXT NOT NULL,
                local_address TEXT NOT NULL,
                local_port INTEGER NOT NULL,
                remote_endpoint TEXT NOT NULL,
                toml TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS runtime_processes (
                name TEXT PRIMARY KEY NOT NULL,
                node_name TEXT NOT NULL,
                process_kind TEXT NOT NULL,
                status TEXT NOT NULL,
                process_id TEXT NOT NULL,
                uptime TEXT NOT NULL,
                listen_address TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS deployment_records (
                step_name TEXT PRIMARY KEY NOT NULL,
                node_name TEXT NOT NULL,
                description TEXT NOT NULL,
                status TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);

        await EnsureColumnAsync(
            connection,
            "nodes",
            "last_connection_tested_at",
            "ALTER TABLE nodes ADD COLUMN last_connection_tested_at TEXT NULL;",
            cancellationToken);

        await RebuildLegacyTunnelsTableAsync(connection, cancellationToken);
    }

    private static async Task EnsureColumnAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string tableName,
        string columnName,
        string alterStatement,
        CancellationToken cancellationToken)
    {
        await using var inspectCommand = connection.CreateCommand();
        inspectCommand.CommandText = $"PRAGMA table_info({tableName});";

        await using var reader = await inspectCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = alterStatement;
        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RebuildLegacyTunnelsTableAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var columns = await ReadTableColumnsAsync(connection, "tunnels", cancellationToken);
        var hasStatusDetail = columns.Contains("status_detail");
        var hasRemark = columns.Contains("remark");

        if (!hasStatusDetail && hasRemark)
        {
            return;
        }

        var remarkSelectExpression = hasRemark
            ? "COALESCE(remark, '')"
            : "''";

        await using var command = connection.CreateCommand();
        command.CommandText = $$"""
            CREATE TABLE tunnels_new (
                name TEXT PRIMARY KEY NOT NULL,
                protocol TEXT NOT NULL,
                node_name TEXT NOT NULL,
                local_address TEXT NOT NULL,
                local_port INTEGER NOT NULL,
                remote_endpoint TEXT NOT NULL,
                status TEXT NOT NULL,
                remark TEXT NOT NULL DEFAULT ''
            );

            INSERT INTO tunnels_new (
                name,
                protocol,
                node_name,
                local_address,
                local_port,
                remote_endpoint,
                status,
                remark
            )
            SELECT name,
                   protocol,
                   node_name,
                   local_address,
                   local_port,
                   remote_endpoint,
                   status,
                   {{remarkSelectExpression}}
            FROM tunnels;

            DROP TABLE tunnels;
            ALTER TABLE tunnels_new RENAME TO tunnels;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<HashSet<string>> ReadTableColumnsAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }
}
