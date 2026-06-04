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
                config_path TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS tunnels (
                name TEXT PRIMARY KEY NOT NULL,
                protocol TEXT NOT NULL,
                node_name TEXT NOT NULL,
                local_address TEXT NOT NULL,
                local_port INTEGER NOT NULL,
                remote_endpoint TEXT NOT NULL,
                status TEXT NOT NULL,
                status_detail TEXT NOT NULL
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
    }
}
