using Arturia.FrpNexus.Core.ExcaliburTunnel;
using Arturia.FrpNexus.Infrastructure.ExcaliburTunnel;

namespace Arturia.FrpNexus.Tests.ExcaliburTunnel;

public sealed class FrpTomlSerializerTests
{
    [Fact]
    public void Serialize_WithTcpProfile_EmitsMinimalFrpcToml()
    {
        var toml = FrpTomlSerializer.Serialize(CreateProfile(TunnelProtocol.Tcp));

        Assert.Contains("serverAddr = \"frp.example.internal\"", toml);
        Assert.Contains("serverPort = 7000", toml);
        Assert.Contains("[[proxies]]", toml);
        Assert.Contains("name = \"my-server\"", toml);
        Assert.Contains("type = \"tcp\"", toml);
        Assert.Contains("localIP = \"127.0.0.1\"", toml);
        Assert.Contains("localPort = 8080", toml);
        Assert.Contains("remotePort = 18080", toml);
    }

    [Fact]
    public void Serialize_WithUdpProfile_EmitsUdpType()
    {
        var toml = FrpTomlSerializer.Serialize(CreateProfile(TunnelProtocol.Udp));

        Assert.Contains("type = \"udp\"", toml);
    }

    [Fact]
    public void Serialize_WithSpecialCharacters_EscapesTomlStrings()
    {
        var profile = CreateProfile(TunnelProtocol.Tcp) with
        {
            Id = "profile\"one",
            LocalHost = "host\\name",
            ServerAddress = "frp.example\ninternal"
        };

        var toml = FrpTomlSerializer.Serialize(profile);

        Assert.Contains("name = \"profile\\\"one\"", toml);
        Assert.Contains("localIP = \"host\\\\name\"", toml);
        Assert.Contains("serverAddr = \"frp.example\\ninternal\"", toml);
    }

    [Theory]
    [InlineData(TunnelProtocol.Http)]
    [InlineData(TunnelProtocol.Https)]
    public void Serialize_WithUnsupportedProtocol_Throws(TunnelProtocol protocol)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => FrpTomlSerializer.Serialize(CreateProfile(protocol)));

        Assert.Contains("TCP/UDP", exception.Message);
    }

    private static TunnelProfile CreateProfile(TunnelProtocol protocol)
    {
        return new TunnelProfile(
            "my-server",
            "My Server",
            protocol,
            "127.0.0.1",
            8080,
            18080,
            "frp.example.internal",
            7000,
            true);
    }
}
