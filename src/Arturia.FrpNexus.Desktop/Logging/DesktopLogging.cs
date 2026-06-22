using Serilog;
using Serilog.Events;

namespace Arturia.FrpNexus.Desktop.Logging;

public static class DesktopLogging
{
    public static ILogger CreateLogger(string? logDirectory = null)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(LogEventLevel.Information)
            .WriteTo.File(
                DesktopLogPaths.GetWarningLogPath(logDirectory),
                restrictedToMinimumLevel: LogEventLevel.Warning,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();
    }
}
