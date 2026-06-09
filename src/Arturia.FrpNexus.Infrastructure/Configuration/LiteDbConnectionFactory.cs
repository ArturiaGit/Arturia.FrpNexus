using Arturia.FrpNexus.Core.Configuration;
using LiteDB;

namespace Arturia.FrpNexus.Infrastructure.Configuration;

public sealed class LiteDbConnectionFactory(IFrpNexusDatabasePathProvider pathProvider)
{
    public ILiteDatabase Open()
    {
        var databasePath = pathProvider.GetDatabasePath();

        try
        {
            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return new LiteDatabase(databasePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or LiteException or NotSupportedException or ArgumentException)
        {
            throw new FrpNexusStorageException($"无法打开 FrpNexus LiteDB 数据库：{databasePath}", exception);
        }
    }
}
