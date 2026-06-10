using System.Globalization;
using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Application.Configuration;

public static class FrpTunnelConfigurationValidator
{
    public const int DefaultFrpsServerPort = 7000;

    public static void ValidatePreview(ConfigurationPreview preview, int frpsServerPort = DefaultFrpsServerPort)
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

        ValidateRemoteEndpoint(preview.Protocol, preview.RemoteEndpoint, frpsServerPort);
    }

    public static void ValidateTunnel(TunnelProfile tunnel, int frpsServerPort = DefaultFrpsServerPort)
    {
        if (string.IsNullOrWhiteSpace(tunnel.Name))
        {
            throw new InvalidOperationException("隧道名称不能为空。");
        }

        if (string.IsNullOrWhiteSpace(tunnel.LocalAddress))
        {
            throw new InvalidOperationException("本地 IP 不能为空。");
        }

        if (tunnel.LocalPort is < 1 or > 65535)
        {
            throw new InvalidOperationException("本地端口必须是 1 到 65535 之间的数字。");
        }

        ValidateRemoteEndpoint(tunnel.Protocol, tunnel.RemoteEndpoint, frpsServerPort);
    }

    public static int ParseTcpUdpRemotePort(string endpoint, int frpsServerPort = DefaultFrpsServerPort)
    {
        var normalized = endpoint.Trim();
        if (!int.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out var remotePort)
            || remotePort is < 1 or > 65535)
        {
            throw new InvalidOperationException("TCP/UDP 隧道的远程端口必须是 1 到 65535 之间的数字。");
        }

        if (remotePort == frpsServerPort)
        {
            throw new InvalidOperationException(
                $"远程端口 {remotePort} 与 frps 服务端口冲突，请改用 25565、60000 等未占用端口。");
        }

        return remotePort;
    }

    private static void ValidateRemoteEndpoint(
        TunnelProtocol protocol,
        string endpoint,
        int frpsServerPort)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("远程端点不能为空。");
        }

        if (protocol is TunnelProtocol.Http or TunnelProtocol.Https)
        {
            if (int.TryParse(endpoint.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                throw new InvalidOperationException("HTTP/HTTPS 隧道需要填写域名；如需端口映射请改用 TCP/UDP。");
            }

            return;
        }

        ParseTcpUdpRemotePort(endpoint, frpsServerPort);
    }
}
