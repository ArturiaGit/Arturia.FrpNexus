using System.Globalization;
using System.Text;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Application.Configuration;

public sealed class TomlConfigurationService : ITomlConfigurationService
{
    private const int DefaultFrpsServerPort = 7000;

    public string GenerateProxyToml(ConfigurationPreview preview)
    {
        ValidatePreview(preview);

        var builder = new StringBuilder();
        AppendProxyToml(builder, preview);

        return builder.ToString().TrimEnd();
    }

    public string GenerateClientToml(NodeProfile node, IReadOnlyList<TunnelProfile> tunnels, int webServerPort)
    {
        if (string.IsNullOrWhiteSpace(node.Host))
        {
            throw new InvalidOperationException("节点 Host 不能为空。");
        }

        if (webServerPort is < 1 or > 65535)
        {
            throw new InvalidOperationException("frpc 管理端口必须是 1 到 65535 之间的数字。");
        }

        if (tunnels.Count == 0)
        {
            throw new InvalidOperationException("至少需要一条启用隧道。");
        }

        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"serverAddr = \"{EscapeTomlString(node.Host.Trim())}\"");
        builder.AppendLine(CultureInfo.InvariantCulture, $"serverPort = {DefaultFrpsServerPort}");
        builder.AppendLine();
        builder.AppendLine("[webServer]");
        builder.AppendLine("addr = \"127.0.0.1\"");
        builder.AppendLine(CultureInfo.InvariantCulture, $"port = {webServerPort}");

        foreach (var tunnel in tunnels)
        {
            builder.AppendLine();
            AppendProxyToml(builder, new ConfigurationPreview(
                tunnel.Name,
                tunnel.Protocol,
                tunnel.LocalAddress,
                tunnel.LocalPort,
                tunnel.RemoteEndpoint,
                string.Empty));
        }

        return builder.ToString().TrimEnd();
    }

    public string GenerateServerToml(int bindPort)
    {
        if (bindPort is < 1 or > 65535)
        {
            throw new InvalidOperationException("frps 监听端口必须是 1 到 65535 之间的数字。");
        }

        return string.Create(CultureInfo.InvariantCulture, $"bindPort = {bindPort}");
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

    private static void AppendProxyToml(StringBuilder builder, ConfigurationPreview preview)
    {
        ValidatePreview(preview);

        builder.AppendLine("[[proxies]]");
        builder.AppendLine(CultureInfo.InvariantCulture, $"name = \"{EscapeTomlString(preview.ProxyName.Trim())}\"");
        builder.AppendLine(CultureInfo.InvariantCulture, $"type = \"{ToFrpProtocol(preview.Protocol)}\"");
        builder.AppendLine(CultureInfo.InvariantCulture, $"localIP = \"{EscapeTomlString(preview.LocalAddress.Trim())}\"");
        builder.AppendLine(CultureInfo.InvariantCulture, $"localPort = {preview.LocalPort}");

        var endpoint = preview.RemoteEndpoint.Trim();
        if (preview.Protocol is TunnelProtocol.Http or TunnelProtocol.Https)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"customDomains = [\"{EscapeTomlString(endpoint)}\"]");
            return;
        }

        if (!int.TryParse(endpoint, out var remotePort) || remotePort is < 1 or > 65535)
        {
            throw new InvalidOperationException("TCP/UDP 隧道的远程端点必须是 1 到 65535 之间的远程端口。");
        }

        builder.AppendLine(CultureInfo.InvariantCulture, $"remotePort = {remotePort}");
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
