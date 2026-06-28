using System;
using System.IO;

namespace Arturia.FrpNexus.Desktop.Logging;

public static class DesktopLogPaths
{
    public static string GetWarningLogDirectory(string? configuredDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return configuredDirectory;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Arturia", "FrpNexus", "logs");
    }

    public static string GetWarningLogPath(string? configuredDirectory = null)
    {
        return Path.Combine(GetWarningLogDirectory(configuredDirectory), "frpnexus-.log");
    }
}
