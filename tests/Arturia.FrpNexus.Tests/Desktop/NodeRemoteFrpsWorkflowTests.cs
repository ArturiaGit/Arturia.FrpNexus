using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.ViewModels.Nodes;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class NodeRemoteFrpsWorkflowTests
{
    [Fact]
    public async Task RefreshAsync_ShouldPreferFrpsProcessUsingNodeConfigPath()
    {
        var runtimeService = new FakeRemoteRuntimeService
        {
            Processes =
            [
                new RuntimeProcess("other", "node-a", "frps", FrpNexusStatus.Running, "101", "00:10", "-", "/opt/frp/frps -c /tmp/other.toml"),
                new RuntimeProcess("target", "node-a", "frps", FrpNexusStatus.Running, "102", "01:20", "-", "/opt/frp/frps -c /etc/frp/frps.toml")
            ]
        };
        var workflow = new NodeRemoteFrpsWorkflow(runtimeService);

        var result = await workflow.RefreshAsync(CreateNode(), CreateCredential());

        Assert.Equal(FrpNexusStatus.Running, result.Status);
        Assert.Equal("01:20", result.Uptime);
        Assert.False(result.IsAmbiguous);
        Assert.True(result.ShouldClearRetention);
    }

    [Fact]
    public async Task RefreshAsync_ShouldReportAmbiguousWhenMultipleFrpsProcessesDoNotMatchConfigPath()
    {
        var runtimeService = new FakeRemoteRuntimeService
        {
            Processes =
            [
                new RuntimeProcess("first", "node-a", "frps", FrpNexusStatus.Running, "101", "00:10", "-", "/opt/frp/frps -c /tmp/a.toml"),
                new RuntimeProcess("second", "node-a", "frps", FrpNexusStatus.Running, "102", "00:20", "-", "/opt/frp/frps -c /tmp/b.toml")
            ]
        };
        var workflow = new NodeRemoteFrpsWorkflow(runtimeService);

        var result = await workflow.RefreshAsync(CreateNode(), CreateCredential());

        Assert.Equal(FrpNexusStatus.Warning, result.Status);
        Assert.True(result.IsAmbiguous);
        Assert.False(result.ShouldClearRetention);
    }

    [Theory]
    [InlineData(NodeRemoteFrpsAction.Start, "Start")]
    [InlineData(NodeRemoteFrpsAction.Stop, "Stop")]
    [InlineData(NodeRemoteFrpsAction.Restart, "Restart")]
    public async Task ExecuteAsync_ShouldCallMatchingRemoteRuntimeOperation(
        NodeRemoteFrpsAction action,
        string expectedOperation)
    {
        var runtimeService = new FakeRemoteRuntimeService();
        var workflow = new NodeRemoteFrpsWorkflow(runtimeService);

        var result = await workflow.ExecuteAsync(
            CreateNode(),
            CreateCredential(),
            action,
            "/opt/frp/frps");

        Assert.Equal(expectedOperation, runtimeService.LastOperation);
        Assert.Equal(FrpNexusStatus.Ready, result.Status);
        Assert.Equal("ok", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldBuildStartCommandWithUploadedBinaryAndConfigPath()
    {
        var runtimeService = new FakeRemoteRuntimeService();
        var workflow = new NodeRemoteFrpsWorkflow(runtimeService);

        await workflow.ExecuteAsync(
            CreateNode(),
            CreateCredential(),
            NodeRemoteFrpsAction.Start,
            "/custom/frps");

        Assert.Contains("chmod +x '/custom/frps'", runtimeService.LastCommand);
        Assert.Contains("-c '/etc/frp/frps.toml'", runtimeService.LastCommand);
    }

    private static NodeProfile CreateNode()
    {
        return new NodeProfile(
            "node-a",
            "203.0.113.10",
            22,
            "deploy",
            "SessionPassword",
            "Ubuntu",
            FrpNexusStatus.Online,
            FrpNexusStatus.Stopped,
            "v0.61.1",
            "-",
            "/etc/frp/frps.toml");
    }

    private static SshCredentialReference CreateCredential()
    {
        return new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "secret");
    }

    private sealed class FakeRemoteRuntimeService : IRemoteRuntimeService
    {
        public IReadOnlyList<RuntimeProcess> Processes { get; init; } = [];
        public string LastOperation { get; private set; } = string.Empty;
        public string LastCommand { get; private set; } = string.Empty;

        public Task<IReadOnlyList<RuntimeProcess>> GetProcessesAsync(
            RemoteRuntimeQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Processes);
        }

        public Task<RemoteRuntimeCommandResult> StartAsync(
            RemoteRuntimeCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            LastOperation = "Start";
            LastCommand = request.Command;
            return Task.FromResult(CreateResult(request));
        }

        public Task<RemoteRuntimeCommandResult> StopAsync(
            RemoteRuntimeCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            LastOperation = "Stop";
            LastCommand = request.Command;
            return Task.FromResult(CreateResult(request));
        }

        public Task<RemoteRuntimeCommandResult> RestartAsync(
            RemoteRuntimeCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            LastOperation = "Restart";
            LastCommand = request.Command;
            return Task.FromResult(CreateResult(request));
        }

        private static RemoteRuntimeCommandResult CreateResult(RemoteRuntimeCommandRequest request)
        {
            return new RemoteRuntimeCommandResult(
                request.Node.Name,
                request.ProcessName,
                FrpNexusStatus.Ready,
                DateTimeOffset.UtcNow,
                "ok");
        }
    }
}
