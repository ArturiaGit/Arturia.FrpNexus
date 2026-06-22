using Arturia.FrpNexus.Application.Abstractions;

namespace Arturia.FrpNexus.Infrastructure.Persistence;

public interface IFrpNexusDatabasePathProvider
{
    string GetDatabasePath();
}

public sealed class FrpNexusDatabasePathProvider : IFrpNexusDatabasePathProvider
{
    private readonly string _databasePath;

    public FrpNexusDatabasePathProvider(ILocalStoragePathSettingsService pathSettingsService)
    {
        _databasePath = pathSettingsService.GetSqliteDatabasePath();
    }

    public string GetDatabasePath()
    {
        return _databasePath;
    }
}
