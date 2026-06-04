using System;
using System.IO;

namespace Arturia.FrpNexus.Desktop.Logging;

public static class DesktopLogPaths
{
    public static string GetWarningLogPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logDirectory = Path.Combine(localAppData, "Arturia", "FrpNexus", "logs");

        return Path.Combine(logDirectory, "frpnexus-.log");
    }
}
