using System.Globalization;
using System.Text;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Application.Configuration;

public sealed class TomlConfigurationService : ITomlConfigurationService
{
    public string GenerateProxyToml(ConfigurationPreview preview)
    {
        ValidatePreview(preview);

        var builder = new StringBuilder();
        builder.AppendLine("[[proxies]]");
        builder.AppendLine(CultureInfo.InvariantCulture, $"name = \"{EscapeTomlString(preview.ProxyName.Trim())}\"");
        builder.AppendLine(CultureInfo.InvariantCulture, $"type = \"{ToFrpProtocol(preview.Protocol)}\"");
        builder.AppendLine(CultureInfo.InvariantCulture, $"localIP = \"{EscapeTomlString(preview.LocalAddress.Trim())}\"");
        builder.AppendLine(CultureInfo.InvariantCulture, $"localPort = {preview.LocalPort}");

        var endpoint = preview.RemoteEndpoint.Trim();
        if (preview.Protocol is TunnelProtocol.Http or TunnelProtocol.Https)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"customDomains = [\"{EscapeTomlString(endpoint)}\"]");
        }
        else
        {
            if (!int.TryParse(endpoint, out var remotePort) || remotePort is < 1 or > 65535)
            {
                throw new InvalidOperationException("TCP/UDP 隧道的远程端点必须是 1 到 65535 之间的远程端口。");
            }

            builder.AppendLine(CultureInfo.InvariantCulture, $"remotePort = {remotePort}");
        }

        return builder.ToString().TrimEnd();
    }

    public Task ValidateAsync(string tomlContent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(tomlContent))
        {
            throw new InvalidOperationException("TOML 内容不能为空。");
        }

        var requiredFragments = new[]
        {
            "[[proxies]]",
            "name = ",
            "type = ",
            "localIP = ",
            "localPort = "
        };

        foreach (var fragment in requiredFragments)
        {
            if (!tomlContent.Contains(fragment, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"TOML 缺少必要字段：{fragment.Trim()}");
            }
        }

        if (!tomlContent.Contains("customDomains = ", StringComparison.Ordinal)
            && !tomlContent.Contains("remotePort = ", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("TOML 必须包含 customDomains 或 remotePort。");
        }

        return Task.CompletedTask;
    }

    private static void ValidatePreview(ConfigurationPreview preview)
    {
        if (string.IsNullOrWhiteSpace(preview.ProxyName))
        {
            throw new InvalidOperationException("代理名称不能为空。");
        }

        if (string.IsNullOrWhiteSpace(preview.LocalAddress))
        {
            throw new InvalidOperationException("本地 IP 不能为空。");
        }

        if (preview.LocalPort is < 1 or > 65535)
        {
            throw new InvalidOperationException("本地端口必须是 1 到 65535 之间的数字。");
        }

        if (string.IsNullOrWhiteSpace(preview.RemoteEndpoint))
        {
            throw new InvalidOperationException("远程配置不能为空。");
        }
    }

    private static string ToFrpProtocol(TunnelProtocol protocol)
    {
        return protocol switch
        {
            TunnelProtocol.Tcp => "tcp",
            TunnelProtocol.Udp => "udp",
            TunnelProtocol.Http => "http",
            TunnelProtocol.Https => "https",
            _ => throw new InvalidOperationException("不支持的隧道协议。")
        };
    }

    private static string EscapeTomlString(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
