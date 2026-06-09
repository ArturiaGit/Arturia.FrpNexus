using Arturia.FrpNexus.Core.ExcaliburTunnel;
using Arturia.FrpNexus.Infrastructure.ExcaliburTunnel;

namespace Arturia.FrpNexus.Tests.ExcaliburTunnel;

public sealed class FrpTomlParserTests
{
    [Fact]
    public void Parse_WithSerializerGeneratedTcpToml_ReturnsProfile()
    {
        var source = CreateProfile(TunnelProtocol.Tcp);
        var result = FrpTomlParser.Parse(FrpTomlSerializer.Serialize(source));

        Assert.True(result.IsValid);
        Assert.NotNull(result.Profile);
        Assert.Equal(source.Id, result.Profile.Id);
        Assert.Equal(TunnelProtocol.Tcp, result.Profile.Protocol);
        Assert.Equal(source.LocalHost, result.Profile.LocalHost);
        Assert.Equal(source.LocalPort, result.Profile.LocalPort);
        Assert.Equal(source.RemotePort, result.Profile.RemotePort);
        Assert.Equal(source.ServerAddress, result.Profile.ServerAddress);
        Assert.Equal(source.ServerPort, result.Profile.ServerPort);
    }

    [Fact]
    public void Parse_WithSerializerGeneratedUdpToml_ReturnsUdpProfile()
    {
        var result = FrpTomlParser.Parse(FrpTomlSerializer.Serialize(CreateProfile(TunnelProtocol.Udp)));

        Assert.True(result.IsValid);
        Assert.NotNull(result.Profile);
        Assert.Equal(TunnelProtocol.Udp, result.Profile.Protocol);
    }

    [Theory]
    [InlineData("serverAddr")]
    [InlineData("name")]
    [InlineData("localIP")]
    public void Parse_WithMissingRequiredField_ReturnsInvalid(string missingField)
    {
        var toml = FrpTomlSerializer.Serialize(CreateProfile(TunnelProtocol.Tcp));
        var filtered = string.Join(Environment.NewLine, toml.Split(Environment.NewLine).Where(line => !line.StartsWith(missingField, StringComparison.Ordinal)));

        var result = FrpTomlParser.Parse(filtered);

        Assert.False(result.IsValid);
        Assert.Null(result.Profile);
        Assert.Contains(result.Errors, error => error.Contains(missingField, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("serverPort", "0")]
    [InlineData("localPort", "65536")]
    [InlineData("remotePort", "not-a-port")]
    public void Parse_WithInvalidPort_ReturnsInvalid(string field, string value)
    {
        var toml = FrpTomlSerializer.Serialize(CreateProfile(TunnelProtocol.Tcp));
        toml = ReplaceAssignment(toml, field, value);

        var result = FrpTomlParser.Parse(toml);

        Assert.False(result.IsValid);
        Assert.Null(result.Profile);
        Assert.Contains(result.Errors, error => error.Contains(field, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("http")]
    [InlineData("https")]
    public void Parse_WithUnsupportedType_ReturnsInvalid(string type)
    {
        var toml = ReplaceAssignment(FrpTomlSerializer.Serialize(CreateProfile(TunnelProtocol.Tcp)), "type", $"\"{type}\"");

        var result = FrpTomlParser.Parse(toml);

        Assert.False(result.IsValid);
        Assert.Null(result.Profile);
        Assert.Contains(result.Errors, error => error.Contains("TCP/UDP", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_WithEscapedStrings_UnescapesBasicSequences()
    {
        var source = CreateProfile(TunnelProtocol.Tcp) with
        {
            Id = "profile\"one",
            LocalHost = "host\\name",
            ServerAddress = "frp.example\ninternal"
        };

        var result = FrpTomlParser.Parse(FrpTomlSerializer.Serialize(source));

        Assert.True(result.IsValid);
        Assert.NotNull(result.Profile);
        Assert.Equal(source.Id, result.Profile.Id);
        Assert.Equal(source.LocalHost, result.Profile.LocalHost);
        Assert.Equal(source.ServerAddress, result.Profile.ServerAddress);
    }

    private static string ReplaceAssignment(string toml, string field, string value)
    {
        var lines = toml.Split(Environment.NewLine);
        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].StartsWith(field, StringComparison.Ordinal))
            {
                lines[index] = $"{field} = {value}";
            }
        }

        return string.Join(Environment.NewLine, lines);
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
