using Arturia.FrpNexus.Core.ExcaliburTunnel;
using Arturia.FrpNexus.Infrastructure.ExcaliburTunnel;

namespace Arturia.FrpNexus.Tests.ExcaliburTunnel;

public sealed class FrpExcaliburTunnelValidationTests
{
    private readonly FrpExcaliburTunnel tunnel = new();

    [Theory]
    [InlineData(TunnelProtocol.Tcp)]
    [InlineData(TunnelProtocol.Udp)]
    public void Validate_WithSupportedProfile_ReturnsSuccess(TunnelProtocol protocol)
    {
        var result = tunnel.Validate(CreateProfile(protocol));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WithEmptyRequiredStrings_ReturnsErrors()
    {
        var profile = CreateProfile(TunnelProtocol.Tcp) with
        {
            Id = " ",
            Name = string.Empty,
            LocalHost = " ",
            ServerAddress = string.Empty
        };

        var result = tunnel.Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Id", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("名称", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("本地地址", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("服务端地址", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WithOutOfRangePorts_ReturnsErrors()
    {
        var profile = CreateProfile(TunnelProtocol.Tcp) with
        {
            LocalPort = 0,
            RemotePort = 65536,
            ServerPort = -1
        };

        var result = tunnel.Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("本地端口", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("远端端口", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("服务端端口", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(TunnelProtocol.Http)]
    [InlineData(TunnelProtocol.Https)]
    public void Validate_WithUnsupportedProtocol_ReturnsPhase6Error(TunnelProtocol protocol)
    {
        var result = tunnel.Validate(CreateProfile(protocol));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Phase 6", StringComparison.Ordinal) && error.Contains("TCP/UDP", StringComparison.Ordinal));
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
