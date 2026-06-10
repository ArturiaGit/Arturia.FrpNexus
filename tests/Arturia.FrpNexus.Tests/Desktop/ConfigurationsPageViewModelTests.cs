using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Application.Configuration;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.Services;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class ConfigurationsPageViewModelTests
{
    [Fact]
    public void Constructor_ShouldStartWithReadonlyPreviewPlaceholder()
    {
        var viewModel = CreateViewModel();

        Assert.Null(viewModel.SelectedTargetNode);
        Assert.Equal(string.Empty, viewModel.TomlPreview);
        Assert.Single(viewModel.TomlPreviewLines);
        Assert.Contains("TOML", viewModel.TomlPreviewLines[0].Tokens[0].Text);
        Assert.Equal(TomlPreviewTokenKind.Comment, viewModel.TomlPreviewLines[0].Tokens[0].Kind);
        Assert.Contains("预览", viewModel.PreviewActionStatusText);
        Assert.Contains("请选择", viewModel.StatusText);
    }

    [Fact]
    public async Task LoadTargetNodesAsync_ShouldAutoPreviewEnabledTunnelsForFirstNode()
    {
        var node = CreateNode("VPS-HK");
        var enabledTunnel = CreateTunnel("web_proxy_01", node.Name, TunnelProtocol.Http, "example.com", FrpNexusStatus.Running);
        var disabledTunnel = CreateTunnel("ssh_bastion", node.Name, TunnelProtocol.Tcp, "60022", status: FrpNexusStatus.Stopped);
        var otherNodeTunnel = CreateTunnel("other_node_proxy", "VPS-SG", TunnelProtocol.Http, "ignored.example.com", FrpNexusStatus.Running);
        var viewModel = CreateViewModel(
            nodeManagementService: new FakeNodeManagementService([node]),
            tunnelManagementService: new FakeTunnelManagementService([enabledTunnel, disabledTunnel, otherNodeTunnel]));

        await viewModel.LoadTargetNodesCommand.ExecuteAsync(null);

        Assert.Equal(node, viewModel.SelectedTargetNode);
        Assert.Single(viewModel.ClientTunnels);
        Assert.Contains("1", viewModel.ClientTunnelCountText);
        Assert.Contains("serverAddr = \"203.0.113.10\"", viewModel.TomlPreview);
        Assert.Contains("name = \"web_proxy_01\"", viewModel.TomlPreview);
        Assert.DoesNotContain("ssh_bastion", viewModel.TomlPreview);
        Assert.DoesNotContain("ignored.example.com", viewModel.TomlPreview);
        Assert.Equal(string.Empty, viewModel.ErrorText);
        Assert.Contains("已生成", viewModel.StatusText);
    }

    [Fact]
    public async Task LoadTargetNodesAsync_ShouldShowEmptyPreviewWhenNodeHasNoEnabledTunnel()
    {
        var node = CreateNode("VPS-HK");
        var disabledTunnel = CreateTunnel("ssh_bastion", node.Name, TunnelProtocol.Tcp, "60022", status: FrpNexusStatus.Stopped);
        var viewModel = CreateViewModel(
            nodeManagementService: new FakeNodeManagementService([node]),
            tunnelManagementService: new FakeTunnelManagementService([disabledTunnel]));

        await viewModel.LoadTargetNodesCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.ClientTunnels);
        Assert.Equal(string.Empty, viewModel.TomlPreview);
        Assert.Contains("0", viewModel.ClientTunnelCountText);
        Assert.Contains("启用", viewModel.ErrorText);
        Assert.Contains("隧道", viewModel.StatusText);
    }

    [Fact]
    public async Task SelectedTargetNodeChange_ShouldAutoRefreshPreview()
    {
        var hk = CreateNode("VPS-HK", "203.0.113.10");
        var sg = CreateNode("VPS-SG", "203.0.113.20");
        var hkTunnel = CreateTunnel("hk_web", hk.Name, TunnelProtocol.Http, "hk.example.com", FrpNexusStatus.Running);
        var sgTunnel = CreateTunnel("sg_tcp", sg.Name, TunnelProtocol.Tcp, "60022", status: FrpNexusStatus.Running);
        var viewModel = CreateViewModel(
            nodeManagementService: new FakeNodeManagementService([hk, sg]),
            tunnelManagementService: new FakeTunnelManagementService([hkTunnel, sgTunnel]));

        await viewModel.LoadTargetNodesCommand.ExecuteAsync(null);
        viewModel.SelectedTargetNode = sg;
        await Task.Delay(50);

        Assert.Contains("serverAddr = \"203.0.113.20\"", viewModel.TomlPreview);
        Assert.Contains("name = \"sg_tcp\"", viewModel.TomlPreview);
        Assert.DoesNotContain("hk_web", viewModel.TomlPreview);
    }

    [Fact]
    public void TomlPreviewLines_ShouldRefreshWhenTomlPreviewChanges()
    {
        var viewModel = CreateViewModel();

        viewModel.TomlPreview = """
        [[proxies]]
        name = "manual_proxy"
        localPort = 9000
        # comment
        """;

        Assert.Equal(4, viewModel.TomlPreviewLines.Count);
        Assert.Contains(viewModel.TomlPreviewLines[1].Tokens, token => token.Text is "manual_proxy" or "\"manual_proxy\"");
        Assert.Contains(viewModel.TomlPreviewLines[2].Tokens, token => token.Kind == TomlPreviewTokenKind.Number && token.Text == "9000");
        Assert.Contains(viewModel.TomlPreviewLines[3].Tokens, token => token.Kind == TomlPreviewTokenKind.Section);
    }

    [Fact]
    public async Task ValidateTomlCommand_ShouldReportValidationSuccess()
    {
        var node = CreateNode("VPS-HK");
        var tunnel = CreateTunnel("web_proxy_01", node.Name, TunnelProtocol.Http, "example.com", FrpNexusStatus.Running);
        var viewModel = CreateViewModel(
            nodeManagementService: new FakeNodeManagementService([node]),
            tunnelManagementService: new FakeTunnelManagementService([tunnel]));
        viewModel.SelectedTargetNode = node;
        await viewModel.RefreshClientTunnelsCommand.ExecuteAsync(null);

        await viewModel.ValidateTomlCommand.ExecuteAsync(null);

        Assert.Contains("TOML", viewModel.StatusText);
        Assert.Equal("语法校验通过", viewModel.PreviewActionStatusText);
        Assert.Equal(string.Empty, viewModel.ErrorText);
    }

    [Fact]
    public async Task ValidateTomlCommand_ShouldReportValidationFailure()
    {
        var viewModel = CreateViewModel();
        viewModel.TomlPreview = "[[proxies]]";

        await viewModel.ValidateTomlCommand.ExecuteAsync(null);

        Assert.Contains("TOML", viewModel.StatusText);
        Assert.Contains("语法校验失败", viewModel.PreviewActionStatusText);
        Assert.NotEmpty(viewModel.ErrorText);
    }

    [Fact]
    public async Task CopyTomlCommand_ShouldRejectEmptyToml()
    {
        var clipboardService = new FakeClipboardService();
        var viewModel = CreateViewModel(clipboardService: clipboardService);

        await viewModel.CopyTomlCommand.ExecuteAsync(null);

        Assert.Null(clipboardService.LastText);
        Assert.Equal("没有可复制的 TOML 内容", viewModel.PreviewActionStatusText);
    }

    [Fact]
    public async Task CopyTomlCommand_ShouldCopyCurrentTomlPreview()
    {
        var clipboardService = new FakeClipboardService();
        var viewModel = CreateViewModel(clipboardService: clipboardService);
        viewModel.TomlPreview = """
        serverAddr = "203.0.113.10"
        [[proxies]]
        name = "web_proxy_01"
        """;

        await viewModel.CopyTomlCommand.ExecuteAsync(null);

        Assert.Equal(viewModel.TomlPreview, clipboardService.LastText);
        Assert.Equal("已复制 frpc.toml 内容", viewModel.PreviewActionStatusText);
    }

    [Fact]
    public async Task LoadTargetNodesAsync_ShouldReportRecoverableFailure()
    {
        var viewModel = CreateViewModel(nodeManagementService: new FailingNodeManagementService());

        await viewModel.LoadTargetNodesCommand.ExecuteAsync(null);

        Assert.NotEmpty(viewModel.TargetNodeCountText);
        Assert.NotEmpty(viewModel.StatusText);
    }

    private static ConfigurationsPageViewModel CreateViewModel(
        INodeManagementService? nodeManagementService = null,
        ITunnelManagementService? tunnelManagementService = null,
        IClipboardService? clipboardService = null)
    {
        return new ConfigurationsPageViewModel(
            new TomlConfigurationService(),
            nodeManagementService ?? new FakeNodeManagementService([]),
            tunnelManagementService ?? new FakeTunnelManagementService([]),
            clipboardService ?? new FakeClipboardService());
    }

    private static NodeProfile CreateNode(string name, string host = "203.0.113.10")
    {
        return new NodeProfile(
            name,
            host,
            22,
            "root",
            "Session",
            "Ubuntu 22.04 LTS",
            FrpNexusStatus.Online,
            FrpNexusStatus.Stopped,
            "-",
            "-",
            "/opt/frp/frps.toml");
    }

    private static TunnelProfile CreateTunnel(
        string name,
        string nodeName,
        TunnelProtocol protocol,
        string remoteEndpoint,
        FrpNexusStatus status,
        string localAddress = "127.0.0.1",
        int localPort = 8080)
    {
        return new TunnelProfile(
            name,
            protocol,
            nodeName,
            localAddress,
            localPort,
            remoteEndpoint,
            status,
            "remark");
    }

    private sealed class FakeNodeManagementService(IReadOnlyList<NodeProfile> nodes) : INodeManagementService
    {
        public Task<IReadOnlyList<NodeProfile>> ListNodesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(nodes);
        }

        public Task<NodeProfile?> GetNodeAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(nodes.FirstOrDefault(node => node.Name == name));
        }

        public Task SaveNodeAsync(NodeProfile node, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteNodeAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateLastConnectionAsync(string nodeName, DateTimeOffset connectedAt, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateConnectionTestResultAsync(string nodeName, FrpNexusStatus status, DateTimeOffset testedAt, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FailingNodeManagementService : INodeManagementService
    {
        public Task<IReadOnlyList<NodeProfile>> ListNodesAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("database unavailable");
        }

        public Task<NodeProfile?> GetNodeAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<NodeProfile?>(null);
        }

        public Task SaveNodeAsync(NodeProfile node, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteNodeAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateLastConnectionAsync(string nodeName, DateTimeOffset connectedAt, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateConnectionTestResultAsync(string nodeName, FrpNexusStatus status, DateTimeOffset testedAt, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTunnelManagementService(IReadOnlyList<TunnelProfile> tunnels) : ITunnelManagementService
    {
        public Task<IReadOnlyList<TunnelProfile>> ListTunnelsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(tunnels);
        }

        public Task<TunnelProfile?> GetTunnelAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(tunnels.FirstOrDefault(tunnel => tunnel.Name == name));
        }

        public Task SaveTunnelAsync(TunnelProfile tunnel, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteTunnelAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClipboardService(bool shouldFail = false) : IClipboardService
    {
        public string? LastText { get; private set; }

        public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            if (shouldFail)
            {
                throw new InvalidOperationException("clipboard unavailable");
            }

            LastText = text;
            return Task.CompletedTask;
        }
    }
}
