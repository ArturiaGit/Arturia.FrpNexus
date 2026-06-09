using Arturia.FrpNexus.Cli.Commands;
using Arturia.FrpNexus.Core.Configuration;
using Arturia.FrpNexus.Infrastructure.Configuration;
using Arturia.FrpNexus.Tests.Configuration;

namespace Arturia.FrpNexus.Tests.Cli;

public sealed class ConfigCommandsTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "FrpNexusTests", Guid.NewGuid().ToString("N"));
    private readonly string databasePath;

    public ConfigCommandsTests()
    {
        databasePath = Path.Combine(tempDirectory, "frpnexus.db");
    }

    [Fact]
    public async Task Show_WithDefaultSettings_WritesDefaultValues()
    {
        var commands = new ConfigCommands(CreateStore());

        var output = await ConsoleCapture.CaptureAsync(() => commands.Show());

        Assert.Contains("FrpNexus 配置", output);
        Assert.Contains("version: 1", output);
        Assert.Contains("frpc-path: 未设置", output);
    }

    [Fact]
    public async Task GetFrpcPath_WhenUnset_WritesClearMessage()
    {
        var commands = new ConfigCommands(CreateStore());

        var result = await ConsoleCapture.CaptureAsync(() => commands.Get("frpc-path"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("frpc-path 未设置", result.Output);
    }

    [Fact]
    public async Task SetFrpcPath_SavesValueForLaterRead()
    {
        var store = CreateStore();
        var commands = new ConfigCommands(store);

        var result = await ConsoleCapture.CaptureAsync(() => commands.Set("frpc-path", "/tmp/frpc"));
        var settings = await store.LoadAsync();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("/tmp/frpc", settings.FrpcPath);
        Assert.Contains("已保存 frpc-path", result.Output);
    }

    [Fact]
    public async Task SetFrpcPath_WithBlankPath_Fails()
    {
        var commands = new ConfigCommands(CreateStore());

        var result = await ConsoleCapture.CaptureAsync(() => commands.Set("frpc-path", " "));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("frpc-path 不能为空", result.Output);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private LiteDbFrpNexusSettingsStore CreateStore()
    {
        return new LiteDbFrpNexusSettingsStore(new LiteDbConnectionFactory(new TemporaryDatabasePathProvider(databasePath)));
    }
}
