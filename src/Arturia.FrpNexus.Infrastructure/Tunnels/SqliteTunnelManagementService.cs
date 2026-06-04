using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Persistence;

namespace Arturia.FrpNexus.Infrastructure.Tunnels;

public sealed class SqliteTunnelManagementService(
    ISqliteConnectionFactory connectionFactory,
    ISqliteDatabaseInitializer databaseInitializer) : ITunnelManagementService
{
    public async Task<IReadOnlyList<TunnelProfile>> ListTunnelsAsync(CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name,
                   protocol,
                   node_name,
                   local_address,
                   local_port,
                   remote_endpoint,
                   status,
                   status_detail
            FROM tunnels
            ORDER BY name;
            """;

        var tunnels = new List<TunnelProfile>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tunnels.Add(ReadTunnel(reader));
        }

        return tunnels;
    }

    public async Task<TunnelProfile?> GetTunnelAsync(string tunnelName, CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name,
                   protocol,
                   node_name,
                   local_address,
                   local_port,
                   remote_endpoint,
                   status,
                   status_detail
            FROM tunnels
            WHERE name = $name;
            """;
        command.Parameters.AddWithValue("$name", tunnelName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadTunnel(reader)
            : null;
    }

    public async Task SaveTunnelAsync(TunnelProfile tunnel, CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO tunnels (
                name,
                protocol,
                node_name,
                local_address,
                local_port,
                remote_endpoint,
                status,
                status_detail
            )
            VALUES (
                $name,
                $protocol,
                $node_name,
                $local_address,
                $local_port,
                $remote_endpoint,
                $status,
                $status_detail
            )
            ON CONFLICT(name) DO UPDATE SET
                protocol = excluded.protocol,
                node_name = excluded.node_name,
                local_address = excluded.local_address,
                local_port = excluded.local_port,
                remote_endpoint = excluded.remote_endpoint,
                status = excluded.status,
                status_detail = excluded.status_detail;
            """;

        command.Parameters.AddWithValue("$name", tunnel.Name);
        command.Parameters.AddWithValue("$protocol", tunnel.Protocol.ToString());
        command.Parameters.AddWithValue("$node_name", tunnel.NodeName);
        command.Parameters.AddWithValue("$local_address", tunnel.LocalAddress);
        command.Parameters.AddWithValue("$local_port", tunnel.LocalPort);
        command.Parameters.AddWithValue("$remote_endpoint", tunnel.RemoteEndpoint);
        command.Parameters.AddWithValue("$status", tunnel.Status.ToString());
        command.Parameters.AddWithValue("$status_detail", tunnel.StatusDetail);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteTunnelAsync(string tunnelName, CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM tunnels WHERE name = $name;";
        command.Parameters.AddWithValue("$name", tunnelName);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static TunnelProfile ReadTunnel(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new TunnelProfile(
            reader.GetString(0),
            ParseProtocol(reader.GetString(1)),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt32(4),
            reader.GetString(5),
            ParseStatus(reader.GetString(6)),
            reader.GetString(7));
    }

    private static TunnelProtocol ParseProtocol(string value)
    {
        return Enum.TryParse<TunnelProtocol>(value, ignoreCase: true, out var protocol)
            ? protocol
            : TunnelProtocol.Tcp;
    }

    private static FrpNexusStatus ParseStatus(string value)
    {
        return Enum.TryParse<FrpNexusStatus>(value, ignoreCase: true, out var status)
            ? status
            : FrpNexusStatus.Pending;
    }
}
