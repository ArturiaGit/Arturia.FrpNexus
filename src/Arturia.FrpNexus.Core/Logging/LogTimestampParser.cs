using System.Globalization;
using System.Text.RegularExpressions;

namespace Arturia.FrpNexus.Core.Logging;

public static partial class LogTimestampParser
{
    public const string UnknownTimestamp = "未知时间";

    private static readonly string[] TimestampFormats =
    [
        "yyyy-MM-dd HH:mm:ss.fffffff zzz",
        "yyyy-MM-dd HH:mm:ss.ffffff zzz",
        "yyyy-MM-dd HH:mm:ss.fffff zzz",
        "yyyy-MM-dd HH:mm:ss.ffff zzz",
        "yyyy-MM-dd HH:mm:ss.fff zzz",
        "yyyy-MM-dd HH:mm:ss.ff zzz",
        "yyyy-MM-dd HH:mm:ss.f zzz",
        "yyyy-MM-dd HH:mm:ss zzz",
        "yyyy-MM-dd HH:mm:ss.fffffff",
        "yyyy-MM-dd HH:mm:ss.ffffff",
        "yyyy-MM-dd HH:mm:ss.fffff",
        "yyyy-MM-dd HH:mm:ss.ffff",
        "yyyy-MM-dd HH:mm:ss.fff",
        "yyyy-MM-dd HH:mm:ss.ff",
        "yyyy-MM-dd HH:mm:ss.f",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy/MM/dd HH:mm:ss.fff",
        "yyyy/MM/dd HH:mm:ss",
        "HH:mm:ss"
    ];

    public static bool TryParseSerilogLine(
        string line,
        out string timestamp,
        out string level,
        out string message)
    {
        timestamp = UnknownTimestamp;
        level = string.Empty;
        message = line.Trim();

        var match = BracketedSerilogLineRegex().Match(line);
        if (!match.Success)
        {
            match = DefaultSerilogLineRegex().Match(line);
        }

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

    public static bool TryParseTimestamp(string value, out DateTimeOffset timestamp)
    {
        timestamp = default;
        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value.Trim(), UnknownTimestamp, StringComparison.Ordinal))
        {
            return false;
        }

        var normalized = value.Trim();
        if (DateTimeOffset.TryParseExact(
            normalized,
            TimestampFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out timestamp))
        {
            return true;
        }

        if (DateTimeOffset.TryParse(
            normalized,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out timestamp))
        {
            return true;
        }

        return DateTimeOffset.TryParse(
            normalized,
            CultureInfo.CurrentCulture,
            DateTimeStyles.AssumeLocal,
            out timestamp);
    }

    [GeneratedRegex(@"^\[(?<timestamp>\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:\s+(?:Z|[+-]\d{2}:\d{2}))?)\s+(?<level>[A-Za-z]{2,12})\]\s*(?<message>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex BracketedSerilogLineRegex();

    [GeneratedRegex(@"^(?<timestamp>\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:\s+(?:Z|[+-]\d{2}:\d{2}))?)\s+\[(?<level>[A-Za-z]{2,12})\]\s*(?<message>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex DefaultSerilogLineRegex();

    [GeneratedRegex(@"^(?<timestamp>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}(?:\.\d{1,9})?)\s+(?<message>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex FrpLineRegex();
}
