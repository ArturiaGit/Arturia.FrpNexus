namespace Arturia.FrpNexus.Infrastructure.Persistence;

public interface IFrpNexusDatabasePathProvider
{
    string GetDatabasePath();
}

public sealed class FrpNexusDatabasePathProvider : IFrpNexusDatabasePathProvider
{
    public string GetDatabasePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Arturia", "FrpNexus", "data", "frpnexus.db");
    }
}
