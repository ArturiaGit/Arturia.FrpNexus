using System.Globalization;
using System.Text;
using Arturia.FrpNexus.Core.ExcaliburTunnel;

namespace Arturia.FrpNexus.Infrastructure.ExcaliburTunnel;

public static class FrpTomlParser
{
    public static TunnelConfigurationParseResult Parse(string configuration)
    {
        var root = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var proxy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inProxy = false;

        foreach (var rawLine in configuration.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.Equals("[[proxies]]", StringComparison.OrdinalIgnoreCase))
            {
                inProxy = true;
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex < 1)
            {
                return TunnelConfigurationParseResult.Failed($"不支持的 TOML 行：{line}");
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (inProxy)
            {
                proxy[key] = value;
            }
            else
            {
                root[key] = value;
            }
        }

        var errors = new List<string>();
        var serverAddress = ReadString(root, "serverAddr", errors);
        var serverPort = ReadPort(root, "serverPort", errors);
        var id = ReadString(proxy, "name", errors);
        var type = ReadString(proxy, "type", errors);
        var localHost = ReadString(proxy, "localIP", errors);
        var localPort = ReadPort(proxy, "localPort", errors);
        var remotePort = ReadPort(proxy, "remotePort", errors);

        var protocol = type?.ToLowerInvariant() switch
        {
            "tcp" => TunnelProtocol.Tcp,
            "udp" => TunnelProtocol.Udp,
            null => TunnelProtocol.Tcp,
            _ => (TunnelProtocol?)null
        };

        if (protocol is null)
        {
            errors.Add("Phase 6 parser 只支持 TCP/UDP 类型。");
        }

        if (errors.Count > 0 || serverAddress is null || id is null || localHost is null || protocol is null)
        {
            return new TunnelConfigurationParseResult(false, null, errors);
        }

        var profile = new TunnelProfile(
            id,
            id,
            protocol.Value,
            localHost,
            localPort,
            remotePort,
            serverAddress,
            serverPort,
            true);

        return TunnelConfigurationParseResult.Success(profile);
    }

    private static string? ReadString(Dictionary<string, string> values, string key, List<string> errors)
    {
        if (!values.TryGetValue(key, out var rawValue))
        {
            errors.Add($"缺少字段：{key}。");
            return null;
        }

        if (rawValue.Length < 2 || rawValue[0] != '"' || rawValue[^1] != '"')
        {
            errors.Add($"字段 {key} 必须是 TOML 字符串。");
            return null;
        }

        return Unescape(rawValue[1..^1], key, errors);
    }

    private static int ReadPort(Dictionary<string, string> values, string key, List<string> errors)
    {
        if (!values.TryGetValue(key, out var rawValue))
        {
            errors.Add($"缺少字段：{key}。");
            return 0;
        }

        if (!int.TryParse(rawValue, NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
        {
            errors.Add($"字段 {key} 必须是 1-65535 的端口。");
            return 0;
        }

        return port;
    }

    private static string Unescape(string value, string key, List<string> errors)
    {
        var builder = new StringBuilder(value.Length);

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character != '\\')
            {
                builder.Append(character);
                continue;
            }

            if (++index >= value.Length)
            {
                errors.Add($"字段 {key} 包含无效转义。");
                return string.Empty;
            }

            builder.Append(value[index] switch
            {
                '\\' => '\\',
                '"' => '"',
                'b' => '\b',
                't' => '\t',
                'n' => '\n',
                'f' => '\f',
                'r' => '\r',
                _ => value[index]
            });
        }

        return builder.ToString();
    }
}
