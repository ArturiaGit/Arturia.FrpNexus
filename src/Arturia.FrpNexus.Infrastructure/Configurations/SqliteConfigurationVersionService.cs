using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Persistence;

namespace Arturia.FrpNexus.Infrastructure.Configurations;

public sealed class SqliteConfigurationVersionService(
    ISqliteConnectionFactory connectionFactory,
    ISqliteDatabaseInitializer databaseInitializer) : IConfigurationVersionService
{
    public async Task<IReadOnlyList<ConfigurationVersion>> ListConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name,
                   protocol,
                   local_address,
                   local_port,
                   remote_endpoint,
                   toml,
                   updated_at
            FROM configuration_versions
            ORDER BY updated_at DESC, name;
            """;

        var configurations = new List<ConfigurationVersion>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            configurations.Add(ReadConfiguration(reader));
        }

        return configurations;
    }

    public async Task<ConfigurationVersion?> GetConfigurationAsync(string name, CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name,
                   protocol,
                   local_address,
                   local_port,
                   remote_endpoint,
                   toml,
                   updated_at
            FROM configuration_versions
            WHERE name = $name;
            """;
        command.Parameters.AddWithValue("$name", name);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadConfiguration(reader)
            : null;
    }

    public async Task SaveConfigurationAsync(ConfigurationVersion configuration, CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO configuration_versions (
                name,
                protocol,
                local_address,
                local_port,
                remote_endpoint,
                toml,
                updated_at
            )
            VALUES (
                $name,
                $protocol,
                $local_address,
                $local_port,
                $remote_endpoint,
                $toml,
                $updated_at
            )
            ON CONFLICT(name) DO UPDATE SET
                protocol = excluded.protocol,
                local_address = excluded.local_address,
                local_port = excluded.local_port,
                remote_endpoint = excluded.remote_endpoint,
                toml = excluded.toml,
                updated_at = excluded.updated_at;
            """;

        command.Parameters.AddWithValue("$name", configuration.Name);
        command.Parameters.AddWithValue("$protocol", configuration.Protocol.ToString());
        command.Parameters.AddWithValue("$local_address", configuration.LocalAddress);
        command.Parameters.AddWithValue("$local_port", configuration.LocalPort);
        command.Parameters.AddWithValue("$remote_endpoint", configuration.RemoteEndpoint);
        command.Parameters.AddWithValue("$toml", configuration.Toml);
        command.Parameters.AddWithValue("$updated_at", configuration.UpdatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteConfigurationAsync(string name, CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM configuration_versions WHERE name = $name;";
        command.Parameters.AddWithValue("$name", name);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static ConfigurationVersion ReadConfiguration(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new ConfigurationVersion(
            reader.GetString(0),
            ParseProtocol(reader.GetString(1)),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetString(4),
            reader.GetString(5),
            ParseUpdatedAt(reader.GetString(6)));
    }

    private static TunnelProtocol ParseProtocol(string value)
    {
        return Enum.TryParse<TunnelProtocol>(value, ignoreCase: true, out var protocol)
            ? protocol
            : TunnelProtocol.Tcp;
    }

    private static DateTimeOffset ParseUpdatedAt(string value)
    {
        return DateTimeOffset.TryParse(value, out var updatedAt)
            ? updatedAt
            : DateTimeOffset.UnixEpoch;
    }
}
