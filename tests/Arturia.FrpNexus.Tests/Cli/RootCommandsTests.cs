using Arturia.FrpNexus.Cli.Commands;
using Arturia.FrpNexus.Core.Configuration;
using Arturia.FrpNexus.Core.ExcaliburTunnel;
using Arturia.FrpNexus.Infrastructure.Configuration;
using Arturia.FrpNexus.Infrastructure.ExcaliburTunnel;
using Arturia.FrpNexus.Tests.Configuration;

namespace Arturia.FrpNexus.Tests.Cli;

public sealed class RootCommandsTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "FrpNexusTests", Guid.NewGuid().ToString("N"));
    private readonly string databasePath;

    public RootCommandsTests()
    {
        databasePath = Path.Combine(tempDirectory, "frpnexus.db");
    }

    [Fact]
    public async Task Run_WithMissingProfile_FailsWithoutFallback()
    {
        var commands = CreateCommands(out _, out _, out var daemon);

        var result = await ConsoleCapture.CaptureAsync(() => commands.Run("missing", frpcPath: "fake-frpc"));

        Assert.Equal(1, result.ExitCode);
        Assert.Null(daemon.LastStartRequest);
        Assert.Contains("未找到 profile: missing", result.Output);
        Assert.Contains("不再 fallback", result.Output);
    }

    [Fact]
    public async Task Run_WithDatabaseProfile_UsesProfileWithoutStartingRealFrpc()
    {
        var commands = CreateCommands(out var repository, out _, out var daemon);
        await repository.SaveAsync(CreateProfile("my-server") with { LocalPort = 9000 });

        var result = await ConsoleCapture.CaptureAsync(() => commands.Run("my-server", frpcPath: "fake-frpc"));

        Assert.Equal(1, result.ExitCode);
        Assert.NotNull(daemon.LastStartRequest);
        Assert.Equal("my-server", daemon.LastStartRequest.Profile.Id);
        Assert.Equal(9000, daemon.LastStartRequest.Profile.LocalPort);
        Assert.Equal("fake-frpc", daemon.LastStartRequest.FrpcPath);
    }

    [Fact]
    public async Task Run_UsesExplicitFrpcPathBeforeSettingsAndEnvironment()
    {
        using var environment = new TemporaryEnvironmentVariable("FRPNEXUS_FRPC_PATH", "env-frpc");
        var commands = CreateCommands(out var repository, out var settingsStore, out var daemon);
        await repository.SaveAsync(CreateProfile("my-server"));
        await settingsStore.SaveAsync(FrpNexusSettings.Default with { FrpcPath = "settings-frpc" });

        await ConsoleCapture.CaptureAsync(() => commands.Run("my-server", frpcPath: "explicit-frpc"));

        Assert.Equal("explicit-frpc", daemon.LastStartRequest?.FrpcPath);
    }

    [Fact]
    public async Task Run_UsesSettingsFrpcPathBeforeEnvironment()
    {
        using var environment = new TemporaryEnvironmentVariable("FRPNEXUS_FRPC_PATH", "env-frpc");
        var commands = CreateCommands(out var repository, out var settingsStore, out var daemon);
        await repository.SaveAsync(CreateProfile("my-server"));
        await settingsStore.SaveAsync(FrpNexusSettings.Default with { FrpcPath = "settings-frpc" });

        await ConsoleCapture.CaptureAsync(() => commands.Run("my-server"));

        Assert.Equal("settings-frpc", daemon.LastStartRequest?.FrpcPath);
    }

    [Fact]
    public async Task Run_UsesEnvironmentFrpcPathWhenExplicitAndSettingsAreMissing()
    {
        using var environment = new TemporaryEnvironmentVariable("FRPNEXUS_FRPC_PATH", "env-frpc");
        var commands = CreateCommands(out var repository, out _, out var daemon);
        await repository.SaveAsync(CreateProfile("my-server"));

        await ConsoleCapture.CaptureAsync(() => commands.Run("my-server"));

        Assert.Equal("env-frpc", daemon.LastStartRequest?.FrpcPath);
    }

    [Fact]
    public async Task Run_WithNoFrpcPath_FailsWithoutStartingDaemon()
    {
        using var environment = new TemporaryEnvironmentVariable("FRPNEXUS_FRPC_PATH", null);
        var commands = CreateCommands(out var repository, out _, out var daemon);
        await repository.SaveAsync(CreateProfile("my-server"));

        var result = await ConsoleCapture.CaptureAsync(() => commands.Run("my-server"));

        Assert.Equal(1, result.ExitCode);
        Assert.Null(daemon.LastStartRequest);
        Assert.Contains("frpc 路径未设置", result.Output);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private RootCommands CreateCommands(
        out LiteDbTunnelProfileRepository repository,
        out LiteDbFrpNexusSettingsStore settingsStore,
        out FakeAvalonDaemon daemon)
    {
        var connectionFactory = new LiteDbConnectionFactory(new TemporaryDatabasePathProvider(databasePath));
        repository = new LiteDbTunnelProfileRepository(connectionFactory);
        settingsStore = new LiteDbFrpNexusSettingsStore(connectionFactory);
        daemon = new FakeAvalonDaemon();

        return new RootCommands(daemon, repository, settingsStore);
    }

    private static TunnelProfile CreateProfile(string id)
    {
        return new TunnelProfile(id, id, TunnelProtocol.Tcp, "127.0.0.1", 8080, 18080, "frp.example.internal", 7000, true);
    }

    private sealed class TemporaryEnvironmentVariable : IDisposable
    {
        private readonly string name;
        private readonly string? originalValue;

        public TemporaryEnvironmentVariable(string name, string? value)
        {
            this.name = name;
            originalValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(name, originalValue);
        }
    }
}
