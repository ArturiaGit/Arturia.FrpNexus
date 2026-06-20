using System;
using System.IO;

namespace Arturia.FrpNexus.Desktop.Logging;

public static class DesktopLogPaths
{
    public static string GetWarningLogDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Arturia", "FrpNexus", "logs");
    }

    public static string GetWarningLogPath()
    {
        return Path.Combine(GetWarningLogDirectory(), "frpnexus-.log");
    }
}
