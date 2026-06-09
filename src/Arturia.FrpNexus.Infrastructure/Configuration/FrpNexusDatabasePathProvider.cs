using System.Runtime.InteropServices;
using Arturia.FrpNexus.Core.Configuration;

namespace Arturia.FrpNexus.Infrastructure.Configuration;

public sealed class FrpNexusDatabasePathProvider : IFrpNexusDatabasePathProvider
{
    public const string DatabaseFileName = "frpnexus.db";

    public string GetDatabasePath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Arturia", "FrpNexus", DatabaseFileName);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", "Arturia", "FrpNexus", DatabaseFileName);
        }

        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var configRoot = string.IsNullOrWhiteSpace(xdgConfigHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : xdgConfigHome;

        return Path.Combine(configRoot, "frpnexus", DatabaseFileName);
    }
}
