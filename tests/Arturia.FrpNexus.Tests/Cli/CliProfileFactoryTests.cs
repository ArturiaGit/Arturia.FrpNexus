using Arturia.FrpNexus.Cli.Commands;
using Arturia.FrpNexus.Core.ExcaliburTunnel;

namespace Arturia.FrpNexus.Tests.Cli;

public sealed class CliProfileFactoryTests
{
    [Fact]
    public void Create_WithDefaults_ReturnsTcpProfile()
    {
        var profile = CliProfileFactory.Create("my-server");

        Assert.Equal("my-server", profile.Id);
        Assert.Equal("my-server", profile.Name);
        Assert.Equal(TunnelProtocol.Tcp, profile.Protocol);
        Assert.Equal("127.0.0.1", profile.LocalHost);
        Assert.Equal(8080, profile.LocalPort);
        Assert.Equal(18080, profile.RemotePort);
        Assert.Equal("frp.example.internal", profile.ServerAddress);
        Assert.Equal(7000, profile.ServerPort);
        Assert.True(profile.Enabled);
    }

    [Fact]
    public void Create_WithUdpProtocol_ReturnsUdpProfile()
    {
        var profile = CliProfileFactory.Create("my-server", protocol: "udp");

        Assert.Equal(TunnelProtocol.Udp, profile.Protocol);
    }

    [Theory]
    [InlineData("http", TunnelProtocol.Http)]
    [InlineData("https", TunnelProtocol.Https)]
    public void Create_WithHttpProtocols_ReturnsMatchingProtocol(string protocol, TunnelProtocol expected)
    {
        var profile = CliProfileFactory.Create("my-server", protocol: protocol);

        Assert.Equal(expected, profile.Protocol);
    }

    [Fact]
    public void Create_WithUnknownProtocol_FallsBackToTcp()
    {
        var profile = CliProfileFactory.Create("my-server", protocol: "unknown");

        Assert.Equal(TunnelProtocol.Tcp, profile.Protocol);
    }

    [Fact]
    public void Create_WithCustomFields_MapsToTunnelProfile()
    {
        var profile = CliProfileFactory.Create(
            "custom-profile",
            localHost: "10.0.0.8",
            localPort: 9000,
            remotePort: 19000,
            serverAddress: "frp.internal",
            serverPort: 7100);

        Assert.Equal("custom-profile", profile.Id);
        Assert.Equal("10.0.0.8", profile.LocalHost);
        Assert.Equal(9000, profile.LocalPort);
        Assert.Equal(19000, profile.RemotePort);
        Assert.Equal("frp.internal", profile.ServerAddress);
        Assert.Equal(7100, profile.ServerPort);
    }
}
