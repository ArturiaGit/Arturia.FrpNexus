using Arturia.FrpNexus.Cli.Commands;
using Arturia.FrpNexus.Core.ExcaliburTunnel;
using Arturia.FrpNexus.Infrastructure.Configuration;
using Arturia.FrpNexus.Infrastructure.ExcaliburTunnel;
using Arturia.FrpNexus.Tests.Configuration;

namespace Arturia.FrpNexus.Tests.Cli;

public sealed class ProfileCommandsTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "FrpNexusTests", Guid.NewGuid().ToString("N"));
    private readonly string databasePath;

    public ProfileCommandsTests()
    {
        databasePath = Path.Combine(tempDirectory, "frpnexus.db");
    }

    [Fact]
    public async Task List_WithEmptyDatabase_WritesEmptyMessage()
    {
        var commands = CreateCommands(out _);

        var output = await ConsoleCapture.CaptureAsync(() => commands.List());

        Assert.Contains("尚未创建 tunnel profile", output);
    }

    [Theory]
    [InlineData("tcp", TunnelProtocol.Tcp)]
    [InlineData("udp", TunnelProtocol.Udp)]
    public async Task Add_WithSupportedProtocol_SavesProfile(string protocol, TunnelProtocol expectedProtocol)
    {
        var commands = CreateCommands(out var repository);

        var result = await ConsoleCapture.CaptureAsync(() => commands.Add("my-server", protocol: protocol));
        var profile = await repository.FindByIdAsync("my-server");

        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(profile);
        Assert.Equal(expectedProtocol, profile.Protocol);
        Assert.Contains("已保存 profile", result.Output);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("https")]
    public async Task Add_WithUnsupportedProtocol_FailsValidation(string protocol)
    {
        var commands = CreateCommands(out var repository);

        var result = await ConsoleCapture.CaptureAsync(() => commands.Add("web", protocol: protocol));
        var profile = await repository.FindByIdAsync("web");

        Assert.Equal(1, result.ExitCode);
        Assert.Null(profile);
        Assert.Contains("validation 失败", result.Output);
        Assert.Contains("TCP/UDP", result.Output);
    }

    [Fact]
    public async Task Show_WithExistingProfile_WritesFields()
    {
        var commands = CreateCommands(out var repository);
        await repository.SaveAsync(CreateProfile("my-server"));

        var result = await ConsoleCapture.CaptureAsync(() => commands.Show("my-server"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("id: my-server", result.Output);
        Assert.Contains("local: 127.0.0.1:8080", result.Output);
    }

    [Fact]
    public async Task Show_WithMissingProfile_Fails()
    {
        var commands = CreateCommands(out _);

        var result = await ConsoleCapture.CaptureAsync(() => commands.Show("missing"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("未找到 profile: missing", result.Output);
    }

    [Fact]
    public async Task Remove_WithExistingProfile_DeletesProfile()
    {
        var commands = CreateCommands(out var repository);
        await repository.SaveAsync(CreateProfile("my-server"));

        var result = await ConsoleCapture.CaptureAsync(() => commands.Remove("my-server"));
        var profile = await repository.FindByIdAsync("my-server");

        Assert.Equal(0, result.ExitCode);
        Assert.Null(profile);
        Assert.Contains("已删除 profile", result.Output);
    }

    [Fact]
    public async Task Remove_WithMissingProfile_Fails()
    {
        var commands = CreateCommands(out _);

        var result = await ConsoleCapture.CaptureAsync(() => commands.Remove("missing"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("未找到 profile: missing", result.Output);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private ProfileCommands CreateCommands(out LiteDbTunnelProfileRepository repository)
    {
        repository = new LiteDbTunnelProfileRepository(new LiteDbConnectionFactory(new TemporaryDatabasePathProvider(databasePath)));
        return new ProfileCommands(repository, new FrpExcaliburTunnel());
    }

    private static TunnelProfile CreateProfile(string id)
    {
        return new TunnelProfile(id, id, TunnelProtocol.Tcp, "127.0.0.1", 8080, 18080, "frp.example.internal", 7000, true);
    }
}
