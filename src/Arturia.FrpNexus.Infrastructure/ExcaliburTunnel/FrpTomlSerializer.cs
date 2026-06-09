using System.Text;
using Arturia.FrpNexus.Core.ExcaliburTunnel;

namespace Arturia.FrpNexus.Infrastructure.ExcaliburTunnel;

public static class FrpTomlSerializer
{
    public static string Serialize(TunnelProfile profile)
    {
        var type = profile.Protocol switch
        {
            TunnelProtocol.Tcp => "tcp",
            TunnelProtocol.Udp => "udp",
            _ => throw new InvalidOperationException("Phase 6 只支持 TCP/UDP 隧道。")
        };

        return string.Join(Environment.NewLine,
        [
            $"serverAddr = \"{Escape(profile.ServerAddress)}\"",
            $"serverPort = {profile.ServerPort}",
            string.Empty,
            "[[proxies]]",
            $"name = \"{Escape(profile.Id)}\"",
            $"type = \"{type}\"",
            $"localIP = \"{Escape(profile.LocalHost)}\"",
            $"localPort = {profile.LocalPort}",
            $"remotePort = {profile.RemotePort}",
            string.Empty
        ]);
    }

    private static string Escape(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(character switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\b' => "\\b",
                '\t' => "\\t",
                '\n' => "\\n",
                '\f' => "\\f",
                '\r' => "\\r",
                _ when char.IsControl(character) => $"\\u{(int)character:x4}",
                _ => character
            });
        }

        return builder.ToString();
    }
}
