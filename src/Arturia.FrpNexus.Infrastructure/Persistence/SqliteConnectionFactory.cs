using Microsoft.Data.Sqlite;

namespace Arturia.FrpNexus.Infrastructure.Persistence;

public interface ISqliteConnectionFactory
{
    SqliteConnection CreateConnection();
}

public sealed class SqliteConnectionFactory(IFrpNexusDatabasePathProvider pathProvider) : ISqliteConnectionFactory
{
    public SqliteConnection CreateConnection()
    {
        var databasePath = pathProvider.GetDatabasePath();
        var databaseDirectory = Path.GetDirectoryName(databasePath);

        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        return new SqliteConnection($"Data Source={databasePath}");
    }
}
