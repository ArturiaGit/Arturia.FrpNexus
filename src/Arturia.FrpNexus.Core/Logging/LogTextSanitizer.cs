using System.Text.RegularExpressions;

namespace Arturia.FrpNexus.Core.Logging;

public static partial class LogTextSanitizer
{
    public static string StripControlSequences(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var withoutAnsi = AnsiEscapeSequenceRegex().Replace(value, string.Empty);
        return ControlCharactersRegex().Replace(withoutAnsi, string.Empty);
    }

    [GeneratedRegex(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])", RegexOptions.CultureInvariant)]
    private static partial Regex AnsiEscapeSequenceRegex();

    [GeneratedRegex(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", RegexOptions.CultureInvariant)]
    private static partial Regex ControlCharactersRegex();
}
