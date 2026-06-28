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

    public static string RedactSecrets(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = StripControlSequences(value);
        normalized = SecretOptionWithEqualsRegex().Replace(normalized, "${name}=[REDACTED]");
        return SecretOptionWithSpaceRegex().Replace(normalized, "${name} [REDACTED]");
    }

    [GeneratedRegex(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])", RegexOptions.CultureInvariant)]
    private static partial Regex AnsiEscapeSequenceRegex();

    [GeneratedRegex(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", RegexOptions.CultureInvariant)]
    private static partial Regex ControlCharactersRegex();

    [GeneratedRegex(
        @"(?<name>(?:--?|/)(?:token|auth-token|password|passwd|pwd|passphrase|private-key-passphrase|key-passphrase|secret|access-token|refresh-token))=(?:""[^""]*""|'[^']*'|[^\s;|&]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretOptionWithEqualsRegex();

    [GeneratedRegex(
        @"(?<name>(?:--?|/)(?:token|auth-token|password|passwd|pwd|passphrase|private-key-passphrase|key-passphrase|secret|access-token|refresh-token))\s+(?:""[^""]*""|'[^']*'|[^\s;|&]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretOptionWithSpaceRegex();
}
