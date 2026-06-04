using Arturia.FrpNexus.Application.Configuration;
using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Tests.Application;

public sealed class TomlConfigurationServiceTests
{
    [Fact]
    public void GenerateProxyToml_ShouldGenerateHttpCustomDomainProxy()
    {
        var service = new TomlConfigurationService();
        var preview = new ConfigurationPreview(
            "web_proxy_01",
            TunnelProtocol.Http,
            "127.0.0.1",
            8080,
            "example.com",
            string.Empty);

        var toml = service.GenerateProxyToml(preview);

        Assert.Contains("[[proxies]]", toml);
        Assert.Contains("name = \"web_proxy_01\"", toml);
        Assert.Contains("type = \"http\"", toml);
        Assert.Contains("localIP = \"127.0.0.1\"", toml);
        Assert.Contains("localPort = 8080", toml);
        Assert.Contains("customDomains = [\"example.com\"]", toml);
        Assert.DoesNotContain("remotePort", toml);
    }

    [Theory]
    [InlineData(TunnelProtocol.Tcp, "tcp")]
    [InlineData(TunnelProtocol.Udp, "udp")]
    public void GenerateProxyToml_ShouldGenerateRemotePortProxyForTcpAndUdp(TunnelProtocol protocol, string expectedType)
    {
        var service = new TomlConfigurationService();
        var preview = new ConfigurationPreview(
            "port_proxy",
            protocol,
            "127.0.0.1",
            22,
            "60022",
            string.Empty);

        var toml = service.GenerateProxyToml(preview);

        Assert.Contains($"type = \"{expectedType}\"", toml);
        Assert.Contains("remotePort = 60022", toml);
        Assert.DoesNotContain("customDomains", toml);
    }

    [Fact]
    public async Task ValidateAsync_ShouldAcceptGeneratedToml()
    {
        var service = new TomlConfigurationService();
        var toml = service.GenerateProxyToml(new ConfigurationPreview(
            "secure_api",
            TunnelProtocol.Https,
            "127.0.0.1",
            8443,
            "api.example.com",
            string.Empty));

        await service.ValidateAsync(toml);
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectIncompleteToml()
    {
        var service = new TomlConfigurationService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ValidateAsync("[[proxies]]"));

        Assert.Contains("缺少必要字段", exception.Message);
    }

    [Fact]
    public void GenerateProxyToml_ShouldRejectInvalidPort()
    {
        var service = new TomlConfigurationService();
        var preview = new ConfigurationPreview(
            "bad_proxy",
            TunnelProtocol.Tcp,
            "127.0.0.1",
            8080,
            "70000",
            string.Empty);

        var exception = Assert.Throws<InvalidOperationException>(() => service.GenerateProxyToml(preview));

        Assert.Equal("TCP/UDP 隧道的远程端点必须是 1 到 65535 之间的远程端口。", exception.Message);
    }
}
