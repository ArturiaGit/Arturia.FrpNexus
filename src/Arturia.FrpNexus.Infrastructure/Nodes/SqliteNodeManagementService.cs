using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Persistence;

namespace Arturia.FrpNexus.Infrastructure.Nodes;

public sealed class SqliteNodeManagementService(
    ISqliteConnectionFactory connectionFactory,
    ISqliteDatabaseInitializer databaseInitializer) : INodeManagementService
{
    public async Task<IReadOnlyList<NodeProfile>> ListNodesAsync(CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name,
                   host,
                   ssh_port,
                   user_name,
                   authentication,
                   operating_system,
                   connection_status,
                   frp_status,
                   frp_version,
                   uptime,
                   config_path
            FROM nodes
            ORDER BY name;
            """;

        var nodes = new List<NodeProfile>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            nodes.Add(ReadNode(reader));
        }

        return nodes;
    }

    public async Task<NodeProfile?> GetNodeAsync(string nodeName, CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name,
                   host,
                   ssh_port,
                   user_name,
                   authentication,
                   operating_system,
                   connection_status,
                   frp_status,
                   frp_version,
                   uptime,
                   config_path
            FROM nodes
            WHERE name = $name;
            """;
        command.Parameters.AddWithValue("$name", nodeName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadNode(reader)
            : null;
    }

    public async Task SaveNodeAsync(NodeProfile node, CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO nodes (
                name,
                host,
                ssh_port,
                user_name,
                authentication,
                operating_system,
                connection_status,
                frp_status,
                frp_version,
                uptime,
                config_path
            )
            VALUES (
                $name,
                $host,
                $ssh_port,
                $user_name,
                $authentication,
                $operating_system,
                $connection_status,
                $frp_status,
                $frp_version,
                $uptime,
                $config_path
            )
            ON CONFLICT(name) DO UPDATE SET
                host = excluded.host,
                ssh_port = excluded.ssh_port,
                user_name = excluded.user_name,
                authentication = excluded.authentication,
                operating_system = excluded.operating_system,
                connection_status = excluded.connection_status,
                frp_status = excluded.frp_status,
                frp_version = excluded.frp_version,
                uptime = excluded.uptime,
                config_path = excluded.config_path;
            """;

        command.Parameters.AddWithValue("$name", node.Name);
        command.Parameters.AddWithValue("$host", node.Host);
        command.Parameters.AddWithValue("$ssh_port", node.SshPort);
        command.Parameters.AddWithValue("$user_name", node.UserName);
        command.Parameters.AddWithValue("$authentication", node.Authentication);
        command.Parameters.AddWithValue("$operating_system", node.OperatingSystem);
        command.Parameters.AddWithValue("$connection_status", node.ConnectionStatus.ToString());
        command.Parameters.AddWithValue("$frp_status", node.FrpStatus.ToString());
        command.Parameters.AddWithValue("$frp_version", node.FrpVersion);
        command.Parameters.AddWithValue("$uptime", node.Uptime);
        command.Parameters.AddWithValue("$config_path", node.ConfigPath);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteNodeAsync(string nodeName, CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM nodes WHERE name = $name;";
        command.Parameters.AddWithValue("$name", nodeName);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static NodeProfile ReadNode(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new NodeProfile(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            ParseStatus(reader.GetString(6)),
            ParseStatus(reader.GetString(7)),
            reader.GetString(8),
            reader.GetString(9),
            reader.GetString(10));
    }

    private static FrpNexusStatus ParseStatus(string value)
    {
        return Enum.TryParse<FrpNexusStatus>(value, ignoreCase: true, out var status)
            ? status
            : FrpNexusStatus.Pending;
    }
}
