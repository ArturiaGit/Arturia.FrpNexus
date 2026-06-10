using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Application.Configuration;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Runtime;
using Serilog.Core;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class LocalFrpcProcessServiceTests
{
    [Fact]
    public async Task ApplyNodeTunnelsAsync_WhenProcessExitsImmediately_ShouldReturnError()
    {
        var service = new LocalFrpcProcessService(Logger.None, new TomlConfigurationService());
        using var disposable = service;
        var request = new LocalFrpcProcessRequest(
            CreateNode(),
            [CreateTunnel()],
            ResolveImmediateExitExecutable(),
            CreateTempConfigPath());

        var result = await service.ApplyNodeTunnelsAsync(request);
        var snapshot = service.GetNodeStatus(request.Node.Name);

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Contains("启动后已退出", result.Message);
        Assert.NotEqual(FrpNexusStatus.Running, snapshot.Status);
    }

    [Fact]
    public async Task ApplyNodeTunnelsAsync_ShouldWriteClientTomlWithoutUtf8Bom()
    {
        var service = new LocalFrpcProcessService(Logger.None, new TomlConfigurationService());
        using var disposable = service;
        var configPath = CreateTempConfigPath();
        var request = new LocalFrpcProcessRequest(
            CreateNode(),
            [CreateTunnel()],
            ResolveImmediateExitExecutable(),
            configPath);

        await service.ApplyNodeTunnelsAsync(request);

        var bytes = await File.ReadAllBytesAsync(configPath);
        Assert.True(bytes.Length >= 3);
        Assert.False(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.Equal((byte)'s', bytes[0]);
    }

    [Fact]
    public void CommandUsesConfigPath_ShouldMatchFrpcCommandLineConfigPath()
    {
        var commandLine = "\"D:\\E\\frp\\frpc.exe\" -c \"D:\\E\\frp\\frpc.toml\"";

        Assert.True(LocalFrpcProcessService.CommandUsesConfigPath(commandLine, @"D:\E\frp\frpc.toml"));
        Assert.False(LocalFrpcProcessService.CommandUsesConfigPath(commandLine, @"D:\E\frp\other.frpc.toml"));
    }

    private static NodeProfile CreateNode()
    {
        return new NodeProfile(
            "Node-Immediate-Exit",
            "127.0.0.1",
            22,
            "root",
            "会话密码",
            "Windows",
            FrpNexusStatus.Online,
            FrpNexusStatus.Stopped,
            "-",
            "-",
            "/etc/frp/frps.toml");
    }

    private static TunnelProfile CreateTunnel()
    {
        return new TunnelProfile(
            "web-dev",
            TunnelProtocol.Http,
            "Node-Immediate-Exit",
            "127.0.0.1",
            8080,
            "dev.example.com",
            FrpNexusStatus.Running,
            string.Empty);
    }

    private static string ResolveImmediateExitExecutable()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.SystemDirectory, "where.exe");
        }

        return "/bin/true";
    }

    private static string CreateTempConfigPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FrpNexusTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "frpc.toml");
    }
}
