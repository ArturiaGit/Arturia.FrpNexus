using System.Text;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Infrastructure.Persistence;

namespace Arturia.FrpNexus.Infrastructure.Settings;

public sealed class SqliteLocalFrpcConfigurationService(
    ISqliteConnectionFactory connectionFactory,
    ISqliteDatabaseInitializer databaseInitializer) : ILocalFrpcConfigurationService
{
    private const string FrpcBinaryPathKey = "local_frpc_binary_path";
    private const string FrpcConfigPathKeyPrefix = "local_frpc_config_path:";

    public async Task<LocalFrpcConfigurationSnapshot> GetConfigurationAsync(
        string nodeName,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var frpcBinaryPath = await ReadValueAsync(connection, FrpcBinaryPathKey, cancellationToken);
        var configKey = CreateNodeConfigPathKey(nodeName);
        var frpcConfigPath = await ReadValueAsync(connection, configKey, cancellationToken);
        var suggestedConfigPath = GetDefaultNodeConfigPath(nodeName);

        return new LocalFrpcConfigurationSnapshot(
            frpcBinaryPath ?? string.Empty,
            string.IsNullOrWhiteSpace(frpcConfigPath) ? suggestedConfigPath : frpcConfigPath,
            suggestedConfigPath);
    }

    public async Task SaveFrpcBinaryPathAsync(
        string frpcBinaryPath,
        CancellationToken cancellationToken = default)
    {
        await SaveValueAsync(FrpcBinaryPathKey, frpcBinaryPath.Trim(), cancellationToken);
    }

    public async Task SaveNodeConfigPathAsync(
        string nodeName,
        string frpcConfigPath,
        CancellationToken cancellationToken = default)
    {
        await SaveValueAsync(CreateNodeConfigPathKey(nodeName), frpcConfigPath.Trim(), cancellationToken);
    }

    public string GetDefaultNodeConfigPath(string nodeName)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Arturia",
            "FrpNexus",
            "configs",
            "frpc");
        var fileName = string.Concat(nodeName.Trim().Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "frpc";
        }

        return Path.Combine(directory, $"{fileName}.frpc.toml");
    }

    private async Task SaveValueAsync(
        string key,
        string value,
        CancellationToken cancellationToken)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

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

    private static async Task<string?> ReadValueAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string key,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value as string;
    }

    private static string CreateNodeConfigPathKey(string nodeName)
    {
        return FrpcConfigPathKeyPrefix + EncodeNodeName(nodeName);
    }

    private static string EncodeNodeName(string nodeName)
    {
        var bytes = Encoding.UTF8.GetBytes(nodeName.Trim());
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
