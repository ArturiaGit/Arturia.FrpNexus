using System.Text.RegularExpressions;

namespace Arturia.FrpNexus.Core.Logging;

public static partial class LogTimestampParser
{
    public const string UnknownTimestamp = "未知时间";

    public static bool TryParseSerilogLine(
        string line,
        out string timestamp,
        out string level,
        out string message)
    {
        timestamp = UnknownTimestamp;
        level = string.Empty;
        message = line.Trim();

        var match = SerilogLineRegex().Match(line);
        if (!match.Success)
        {
            return false;
        }

        timestamp = match.Groups["timestamp"].Value.Trim();
        level = match.Groups["level"].Value.Trim();
        message = match.Groups["message"].Value.Trim();
        return true;
    }

    public static bool TryParseFrpLine(
        string line,
        out string timestamp,
        out string message)
    {
        timestamp = UnknownTimestamp;
        message = line.Trim();

        var match = FrpLineRegex().Match(line);
        if (!match.Success)
        {
            return false;
        }

        timestamp = match.Groups["timestamp"].Value.Trim();
        message = match.Groups["message"].Value.Trim();
        return true;
    }

    [GeneratedRegex(@"^\[(?<timestamp>\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:\s+(?:Z|[+-]\d{2}:\d{2}))?)\s+(?<level>[A-Za-z]{2,12})\]\s*(?<message>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex SerilogLineRegex();

    [GeneratedRegex(@"^(?<timestamp>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}(?:\.\d{1,9})?)\s+(?<message>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex FrpLineRegex();
}
