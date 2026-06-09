using Arturia.FrpNexus.Core.ExcaliburTunnel;

namespace Arturia.FrpNexus.Cli.Commands;

internal static class CliProfileFactory
{
    public static TunnelProfile Create(
        string profileId,
        string protocol = "tcp",
        string localHost = "127.0.0.1",
        int localPort = 8080,
        int remotePort = 18080,
        string serverAddress = "frp.example.internal",
        int serverPort = 7000)
    {
        return new TunnelProfile(
            profileId,
            profileId,
            ParseProtocol(protocol),
            localHost,
            localPort,
            remotePort,
            serverAddress,
            serverPort,
            true);
    }

    private static TunnelProtocol ParseProtocol(string protocol)
    {
        return protocol.ToLowerInvariant() switch
        {
            "tcp" => TunnelProtocol.Tcp,
            "udp" => TunnelProtocol.Udp,
            "http" => TunnelProtocol.Http,
            "https" => TunnelProtocol.Https,
            _ => TunnelProtocol.Tcp
        };
    }
}
