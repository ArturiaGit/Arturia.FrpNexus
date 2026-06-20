using System;
using System.IO;
using Arturia.FrpNexus.Core.Logging;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.Logging;

namespace Arturia.FrpNexus.Desktop.Services;

public sealed class LocalApplicationLogService : ILocalApplicationLogService
{
    private const string LocalNodeName = "客户端";
    private const string ProcessName = "FrpNexus";
    private readonly string? _logDirectory;

    public LocalApplicationLogService()
    {
    }

    public LocalApplicationLogService(string logDirectory)
    {
        _logDirectory = logDirectory;
    }

    public string CurrentLogDirectory => _logDirectory ?? DesktopLogPaths.GetWarningLogDirectory();

    public async Task<IReadOnlyList<LogEntry>> ReadRecentLogsAsync(
        int lineCount = 200,
        CancellationToken cancellationToken = default)
    {
        var maxLines = Math.Clamp(lineCount, 1, 1000);
        if (!Directory.Exists(CurrentLogDirectory))
        {
            return [];
        }

        var files = Directory
            .EnumerateFiles(CurrentLogDirectory, "frpnexus-*.log", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(file => file.Exists)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Take(8)
            .OrderBy(file => file.LastWriteTimeUtc)
            .ToArray();

        var lines = new List<string>(maxLines);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await foreach (var line in ReadFileLinesAsync(file.FullName, cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                lines.Add(line);
                if (lines.Count > maxLines)
                {
                    lines.RemoveRange(0, lines.Count - maxLines);
                }
            }
        }

        return lines.Select(ParseLogLine).ToArray();
    }

    private static async IAsyncEnumerable<string> ReadFileLinesAsync(
        string path,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is not null)
            {
                yield return line;
            }
        }
    }

    private static LogEntry ParseLogLine(string line)
    {
        line = LogTextSanitizer.StripControlSequences(line).Trim();
        var timestamp = LogTimestampParser.UnknownTimestamp;
        var level = DetectLevel(line);
        var message = line.Trim();

        if (LogTimestampParser.TryParseSerilogLine(
            line,
            out var parsedTimestamp,
            out var parsedLevel,
            out var parsedMessage))
        {
            timestamp = parsedTimestamp;
            level = NormalizeLevel(parsedLevel);
            message = string.IsNullOrWhiteSpace(parsedMessage) ? message : parsedMessage;
        }

        return new LogEntry(timestamp, level, LocalNodeName, ProcessName, message, LevelToStatus(level));
    }

    private static string DetectLevel(string line)
    {
        if (line.Contains("fatal", StringComparison.OrdinalIgnoreCase)
            || line.Contains("error", StringComparison.OrdinalIgnoreCase)
            || line.Contains("[ERR", StringComparison.OrdinalIgnoreCase))
        {
            return "ERROR";
        }

        if (line.Contains("warn", StringComparison.OrdinalIgnoreCase)
            || line.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "WARN";
        }

        if (line.Contains("debug", StringComparison.OrdinalIgnoreCase))
        {
            return "DEBUG";
        }

        return "INFO";
    }

    private static string NormalizeLevel(string value)
    {
        var normalized = value.Trim().Trim('[', ']').ToUpperInvariant();
        return normalized switch
        {
            "WRN" or "WARNING" => "WARN",
            "ERR" or "ERROR" or "FATAL" => "ERROR",
            "DBG" or "DEBUG" => "DEBUG",
            "INF" or "INFO" or "INFORMATION" => "INFO",
            _ => DetectLevel(value)
        };
    }

    private static FrpNexusStatus LevelToStatus(string level)
    {
        return level switch
        {
            "ERROR" => FrpNexusStatus.Error,
            "WARN" => FrpNexusStatus.Warning,
            _ => FrpNexusStatus.Ready
        };
    }
}
