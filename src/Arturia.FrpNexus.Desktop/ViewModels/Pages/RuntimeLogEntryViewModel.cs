using Arturia.FrpNexus.Core.AvalonDaemon;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed class RuntimeLogEntryViewModel
{
    public RuntimeLogEntryViewModel(DaemonLogEntry entry)
    {
        TimeText = entry.Timestamp.ToLocalTime().ToString("HH:mm:ss");
        LevelText = FormatLevel(entry.Level);
        Source = string.IsNullOrWhiteSpace(entry.Source) ? "runtime" : entry.Source;
        Message = entry.Message;
        DisplayLine = $"[{TimeText} {LevelText}] {Source}: {Message}";
    }

    public string TimeText { get; }

    public string LevelText { get; }

    public string Source { get; }

    public string Message { get; }

    public string DisplayLine { get; }

    private static string FormatLevel(DaemonLogLevel level)
    {
        return level switch
        {
            DaemonLogLevel.Success => "SUCCESS",
            DaemonLogLevel.Warning => "WARN",
            DaemonLogLevel.Error => "ERROR",
            _ => "INFO"
        };
    }
}
