using Arturia.FrpNexus.Core.Configuration;

namespace Arturia.FrpNexus.Tests.Configuration;

internal sealed class TemporaryDatabasePathProvider(string databasePath) : IFrpNexusDatabasePathProvider
{
    public string GetDatabasePath() => databasePath;
}
