using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Application.Configuration;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.Services;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class ConfigurationsPageViewModelTests
{
    [Fact]
    public void Constructor_ShouldStartWithPlaceholderPreview()
    {
        var viewModel = CreateViewModel();

        Assert.Null(viewModel.SelectedTargetNode);
        Assert.Equal(string.Empty, viewModel.TomlPreview);
        Assert.Single(viewModel.TomlPreviewLines);
        Assert.Contains("TOML", viewModel.TomlPreviewLines[0].Tokens[0].Text);
        Assert.Equal(TomlPreviewTokenKind.Comment, viewModel.TomlPreviewLines[0].Tokens[0].Kind);
        Assert.Equal(string.Empty, viewModel.RemoteConfigPath);
        Assert.Equal(string.Empty, viewModel.ServerBindPort);
        Assert.Contains("验证", viewModel.PreviewActionStatusText);
    }

    [Fact]
    public async Task LoadTargetNodesAsync_ShouldLoadTargetNodesAndRefreshTunnelSource()
    {
        var node = CreateNode("VPS-HK", "/etc/frp/frps.toml");
        var tunnel = CreateTunnel("web_proxy_01", node.Name, TunnelProtocol.Http, "example.com");
        var viewModel = CreateViewModel(
            nodeManagementService: new FakeNodeManagementService([node]),
            tunnelManagementService: new FakeTunnelManagementService([tunnel]));

        await viewModel.LoadTargetNodesCommand.ExecuteAsync(null);
        await viewModel.RefreshClientTunnelsCommand.ExecuteAsync(null);

        Assert.Single(viewModel.TargetNodes);
        Assert.Equal(node, viewModel.SelectedTargetNode);
        Assert.Equal(string.Empty, viewModel.RemoteConfigPath);
        Assert.Single(viewModel.ClientTunnels);
        Assert.Contains("1", viewModel.ClientTunnelCountText);
    }

    [Fact]
    public async Task GenerateTomlCommand_ShouldRejectMissingTargetNode()
    {
        var viewModel = CreateViewModel(nodeManagementService: new FakeNodeManagementService([]));
        viewModel.SelectedTargetNode = null;

        await viewModel.GenerateTomlCommand.ExecuteAsync(null);

        Assert.Contains("目标节点", viewModel.ErrorText);
        Assert.Contains("失败", viewModel.StatusText);
    }

    [Fact]
    public async Task GenerateTomlCommand_ShouldRejectNodeWithoutTunnels()
    {
        var node = CreateNode("VPS-HK", "/etc/frp/frps.toml");
        var viewModel = CreateViewModel(
            nodeManagementService: new FakeNodeManagementService([node]),
            tunnelManagementService: new FakeTunnelManagementService([]));
        viewModel.SelectedTargetNode = node;

        await viewModel.GenerateTomlCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, viewModel.TomlPreview);
        Assert.Contains("隧道", viewModel.ErrorText);
        Assert.Contains("失败", viewModel.StatusText);
    }

    [Fact]
    public async Task GenerateTomlCommand_ShouldGenerateFullClientTomlForSelectedNodeTunnels()
    {
        var node = CreateNode("VPS-HK", "/etc/frp/frps.toml");
        var tunnels = new[]
        {
            CreateTunnel("web_proxy_01", node.Name, TunnelProtocol.Http, "example.com"),
            CreateTunnel("ssh_bastion", node.Name, TunnelProtocol.Tcp, "60022", "127.0.0.1", 22),
            CreateTunnel("other_node_proxy", "VPS-SG", TunnelProtocol.Http, "ignored.example.com")
        };
        var viewModel = CreateViewModel(
            nodeManagementService: new FakeNodeManagementService([node]),
            tunnelManagementService: new FakeTunnelManagementService(tunnels));
        viewModel.SelectedTargetNode = node;

        await viewModel.GenerateTomlCommand.ExecuteAsync(null);

        Assert.Contains("serverAddr = \"203.0.113.10\"", viewModel.TomlPreview);
        Assert.Contains("serverPort = 7000", viewModel.TomlPreview);
        Assert.DoesNotContain("[webServer]", viewModel.TomlPreview);
        Assert.DoesNotContain("addr = \"127.0.0.1\"", viewModel.TomlPreview);
        Assert.DoesNotContain("port = 7400", viewModel.TomlPreview);
        Assert.Contains("name = \"web_proxy_01\"", viewModel.TomlPreview);
        Assert.Contains("customDomains = [\"example.com\"]", viewModel.TomlPreview);
        Assert.Contains("name = \"ssh_bastion\"", viewModel.TomlPreview);
        Assert.Contains("remotePort = 60022", viewModel.TomlPreview);
        Assert.DoesNotContain("ignored.example.com", viewModel.TomlPreview);
        Assert.Contains(viewModel.TomlPreviewLines, line => line.Tokens.Any(token => token.Kind == TomlPreviewTokenKind.Section));
        Assert.Contains(viewModel.TomlPreviewLines, line => line.Tokens.Any(token => token.Kind == TomlPreviewTokenKind.Key));
        Assert.Contains(viewModel.TomlPreviewLines, line => line.Tokens.Any(token => token.Kind == TomlPreviewTokenKind.String));
        Assert.Contains(viewModel.TomlPreviewLines, line => line.Tokens.Any(token => token.Kind == TomlPreviewTokenKind.Number));
        Assert.Equal(string.Empty, viewModel.ErrorText);
        Assert.Contains("可验证语法", viewModel.PreviewActionStatusText);
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
        Assert.Contains(viewModel.TomlPreviewLines[1].Tokens, token => token.Text == "manual_proxy" || token.Text == "\"manual_proxy\"");
        Assert.Contains(viewModel.TomlPreviewLines[2].Tokens, token => token.Kind == TomlPreviewTokenKind.Number && token.Text == "9000");
        Assert.Contains(viewModel.TomlPreviewLines[3].Tokens, token => token.Kind == TomlPreviewTokenKind.Section);
    }

    [Fact]
    public void ToggleAdvancedOptionsCommand_ShouldToggleAdvancedOptions()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.IsAdvancedOptionsVisible);
        Assert.Equal("chevron_right", viewModel.AdvancedOptionsChevronIcon);
        Assert.Equal(0, viewModel.AdvancedOptionsPanelMaxHeight);
        Assert.Equal(0, viewModel.AdvancedOptionsPanelOpacity);
        Assert.Equal(-4, viewModel.AdvancedOptionsPanelOffsetY);
        Assert.False(viewModel.IsAdvancedOptionsInteractive);

        viewModel.ToggleAdvancedOptionsCommand.Execute(null);

        Assert.True(viewModel.IsAdvancedOptionsVisible);
        Assert.Equal("chevron_down", viewModel.AdvancedOptionsChevronIcon);
        Assert.Equal(112, viewModel.AdvancedOptionsPanelMaxHeight);
        Assert.Equal(1, viewModel.AdvancedOptionsPanelOpacity);
        Assert.Equal(0, viewModel.AdvancedOptionsPanelOffsetY);
        Assert.True(viewModel.IsAdvancedOptionsInteractive);
    }

    [Fact]
    public async Task ValidateTomlCommand_ShouldReportValidationSuccess()
    {
        var node = CreateNode("VPS-HK", "/etc/frp/frps.toml");
        var tunnel = CreateTunnel("web_proxy_01", node.Name, TunnelProtocol.Http, "example.com");
        var viewModel = CreateViewModel(
            nodeManagementService: new FakeNodeManagementService([node]),
            tunnelManagementService: new FakeTunnelManagementService([tunnel]));
        viewModel.SelectedTargetNode = node;
        await viewModel.GenerateTomlCommand.ExecuteAsync(null);

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
    public async Task CopyTomlCommand_ShouldReportClipboardFailure()
    {
        var viewModel = CreateViewModel(clipboardService: new FakeClipboardService(shouldFail: true));
        viewModel.TomlPreview = "serverAddr = \"203.0.113.10\"";

        await viewModel.CopyTomlCommand.ExecuteAsync(null);

        Assert.Contains("复制", viewModel.PreviewActionStatusText);
    }

    [Fact]
    public async Task LoadTargetNodesAsync_ShouldReportRecoverableFailure()
    {
        var viewModel = CreateViewModel(nodeManagementService: new FailingNodeManagementService());

        await viewModel.LoadTargetNodesCommand.ExecuteAsync(null);

        Assert.NotEmpty(viewModel.TargetNodeCountText);
        Assert.NotEmpty(viewModel.StatusText);
    }

    [Fact]
    public async Task UploadServerTomlCommand_ShouldRejectMissingTargetNode()
    {
        var viewModel = CreateViewModel(nodeManagementService: new FakeNodeManagementService([]));
        viewModel.SelectedTargetNode = null;

        await viewModel.UploadServerTomlCommand.ExecuteAsync(null);

        Assert.Contains("目标节点", viewModel.ServerUploadErrorText);
    }

    [Fact]
    public async Task UploadServerTomlCommand_ShouldRejectOfflineSession()
    {
        var node = CreateNode("VPS-HK", "/etc/frp/frps.toml");
        var viewModel = CreateViewModel(
            nodeManagementService: new FakeNodeManagementService([node]),
            nodeConnectionSessionService: new FakeNodeConnectionSessionService(NodeConnectionSessionState.Offline));
        viewModel.SelectedTargetNode = node;

        await viewModel.UploadServerTomlCommand.ExecuteAsync(null);

        Assert.Contains("SSH", viewModel.ServerUploadErrorText);
    }

    [Fact]
    public async Task UploadServerTomlCommand_ShouldUploadSelectedNodeConfigPath()
    {
        var node = CreateNode("VPS-HK", "/opt/frp/frps.toml");
        var uploadService = new FakeRemoteFileTransferService();
        var viewModel = CreateViewModel(
            nodeManagementService: new FakeNodeManagementService([node]),
            nodeConnectionSessionService: new FakeNodeConnectionSessionService(NodeConnectionSessionState.Online, CreateCredential()),
            remoteFileTransferService: uploadService);
        viewModel.SelectedTargetNode = node;
        viewModel.GenerateServerTomlCommand.Execute(null);

        await viewModel.UploadServerTomlCommand.ExecuteAsync(null);

        Assert.NotNull(uploadService.LastConfigurationRequest);
        Assert.Equal(node, uploadService.LastConfigurationRequest.Node);
        Assert.Equal("/opt/frp/frps.toml", uploadService.LastConfigurationRequest.RemotePath);
        Assert.Equal("bindPort = 7000", uploadService.LastConfigurationRequest.TomlContent);
        Assert.Contains("/opt/frp/frps.toml", viewModel.ServerUploadStatusText);
        Assert.Equal(string.Empty, viewModel.ServerUploadErrorText);
    }

    [Fact]
    public void GenerateServerTomlCommand_ShouldUseDefaultBindPortWhenInputIsEmpty()
    {
        var viewModel = CreateViewModel();

        viewModel.GenerateServerTomlCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.ServerBindPort);
        Assert.Equal("bindPort = 7000", viewModel.ServerTomlPreview);
        Assert.Equal(string.Empty, viewModel.ServerUploadErrorText);
    }

    [Fact]
    public async Task UploadServerTomlCommand_ShouldFallbackToDefaultConfigPathWhenNodePathAndInputAreEmpty()
    {
        var node = CreateNode("VPS-HK", string.Empty);
        var uploadService = new FakeRemoteFileTransferService();
        var viewModel = CreateViewModel(
            nodeManagementService: new FakeNodeManagementService([node]),
            nodeConnectionSessionService: new FakeNodeConnectionSessionService(NodeConnectionSessionState.Online, CreateCredential()),
            remoteFileTransferService: uploadService);
        viewModel.SelectedTargetNode = node;

        await viewModel.UploadServerTomlCommand.ExecuteAsync(null);

        Assert.NotNull(uploadService.LastConfigurationRequest);
        Assert.Equal(string.Empty, viewModel.RemoteConfigPath);
        Assert.Equal("/opt/frp/frps.toml", uploadService.LastConfigurationRequest.RemotePath);
        Assert.Equal("bindPort = 7000", uploadService.LastConfigurationRequest.TomlContent);
    }

    [Fact]
    public async Task UploadServerTomlCommand_ShouldUseNodeConfigPathAndManualPortWhenProvided()
    {
        var node = CreateNode("VPS-HK", "/opt/frp/frps.toml");
        var uploadService = new FakeRemoteFileTransferService();
        var viewModel = CreateViewModel(
            nodeManagementService: new FakeNodeManagementService([node]),
            nodeConnectionSessionService: new FakeNodeConnectionSessionService(NodeConnectionSessionState.Online, CreateCredential()),
            remoteFileTransferService: uploadService);
        viewModel.SelectedTargetNode = node;
        viewModel.RemoteConfigPath = " /custom/frp/frps.toml ";
        viewModel.ServerBindPort = "7100";

        await viewModel.UploadServerTomlCommand.ExecuteAsync(null);

        Assert.NotNull(uploadService.LastConfigurationRequest);
        Assert.Equal("/opt/frp/frps.toml", uploadService.LastConfigurationRequest.RemotePath);
        Assert.Equal("bindPort = 7100", uploadService.LastConfigurationRequest.TomlContent);
    }

    [Fact]
    public async Task UploadServerTomlCommand_ShouldReportUploadFailure()
    {
        var node = CreateNode("VPS-HK", "/etc/frp/frps.toml");
        var uploadService = new FakeRemoteFileTransferService(FrpNexusStatus.Error, "SFTP 上传失败");
        var viewModel = CreateViewModel(
            nodeManagementService: new FakeNodeManagementService([node]),
            nodeConnectionSessionService: new FakeNodeConnectionSessionService(NodeConnectionSessionState.Online, CreateCredential()),
            remoteFileTransferService: uploadService);
        viewModel.SelectedTargetNode = node;

        await viewModel.UploadServerTomlCommand.ExecuteAsync(null);

        Assert.Contains("SFTP", viewModel.ServerUploadErrorText);
        Assert.Contains("失败", viewModel.ServerUploadStatusText);
    }

    private static ConfigurationsPageViewModel CreateViewModel(
        INodeManagementService? nodeManagementService = null,
        ITunnelManagementService? tunnelManagementService = null,
        INodeConnectionSessionService? nodeConnectionSessionService = null,
        IRemoteFileTransferService? remoteFileTransferService = null,
        IClipboardService? clipboardService = null)
    {
        return new ConfigurationsPageViewModel(
            new TomlConfigurationService(),
            nodeManagementService ?? new FakeNodeManagementService([]),
            tunnelManagementService ?? new FakeTunnelManagementService([]),
            nodeConnectionSessionService ?? new FakeNodeConnectionSessionService(),
            remoteFileTransferService ?? new FakeRemoteFileTransferService(),
            clipboardService ?? new FakeClipboardService());
    }

    private static NodeProfile CreateNode(string name, string configPath)
    {
        return new NodeProfile(
            name,
            "203.0.113.10",
            22,
            "root",
            "Session",
            "Ubuntu 22.04 LTS",
            FrpNexusStatus.Online,
            FrpNexusStatus.Stopped,
            "-",
            "-",
            configPath);
    }

    private static TunnelProfile CreateTunnel(
        string name,
        string nodeName,
        TunnelProtocol protocol,
        string remoteEndpoint,
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
            FrpNexusStatus.Running,
            "运行中");
    }

    private static SshCredentialReference CreateCredential()
    {
        return new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "test-password");
    }

    private sealed class FakeNodeManagementService(IReadOnlyList<NodeProfile> nodes)
        : INodeManagementService
    {
        private readonly List<NodeProfile> _nodes = [.. nodes];

        public Task<IReadOnlyList<NodeProfile>> ListNodesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<NodeProfile>>(_nodes);
        }

        public Task<NodeProfile?> GetNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_nodes.FirstOrDefault(node => node.Name == nodeName));
        }

        public Task SaveNodeAsync(NodeProfile node, CancellationToken cancellationToken = default)
        {
            _nodes.RemoveAll(item => item.Name == node.Name);
            _nodes.Add(node);
            return Task.CompletedTask;
        }

        public Task DeleteNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            _nodes.RemoveAll(node => node.Name == nodeName);
            return Task.CompletedTask;
        }

        public Task UpdateLastConnectionAsync(string nodeName, DateTimeOffset connectedAt, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateConnectionTestResultAsync(
            string nodeName,
            FrpNexusStatus status,
            DateTimeOffset testedAt,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FailingNodeManagementService : INodeManagementService
    {
        public Task<IReadOnlyList<NodeProfile>> ListNodesAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("node database unavailable");
        }

        public Task<NodeProfile?> GetNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("node database unavailable");
        }

        public Task SaveNodeAsync(NodeProfile node, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("node database unavailable");
        }

        public Task DeleteNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("node database unavailable");
        }

        public Task UpdateLastConnectionAsync(string nodeName, DateTimeOffset connectedAt, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("node database unavailable");
        }

        public Task UpdateConnectionTestResultAsync(
            string nodeName,
            FrpNexusStatus status,
            DateTimeOffset testedAt,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("node database unavailable");
        }
    }

    private sealed class FakeTunnelManagementService(IReadOnlyList<TunnelProfile> tunnels)
        : ITunnelManagementService
    {
        private readonly List<TunnelProfile> _tunnels = [.. tunnels];

        public Task<IReadOnlyList<TunnelProfile>> ListTunnelsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TunnelProfile>>(_tunnels);
        }

        public Task<TunnelProfile?> GetTunnelAsync(string tunnelName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tunnels.FirstOrDefault(tunnel => tunnel.Name == tunnelName));
        }

        public Task SaveTunnelAsync(TunnelProfile tunnel, CancellationToken cancellationToken = default)
        {
            _tunnels.RemoveAll(item => item.Name == tunnel.Name);
            _tunnels.Add(tunnel);
            return Task.CompletedTask;
        }

        public Task DeleteTunnelAsync(string tunnelName, CancellationToken cancellationToken = default)
        {
            _tunnels.RemoveAll(tunnel => tunnel.Name == tunnelName);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNodeConnectionSessionService(
        NodeConnectionSessionState state = NodeConnectionSessionState.Offline,
        SshCredentialReference? credential = null)
        : INodeConnectionSessionService
    {
        public Task<NodeConnectionSessionResult> ConnectAsync(
            NodeProfile node,
            SshCredentialReference credential,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new NodeConnectionSessionResult(node.Name, state, DateTimeOffset.UtcNow, "connected"));
        }

        public Task<NodeConnectionSessionResult> DisconnectAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new NodeConnectionSessionResult(nodeName, NodeConnectionSessionState.Disconnected, null, "disconnected"));
        }

        public NodeConnectionSessionSnapshot GetSessionStatus(string nodeName)
        {
            return new NodeConnectionSessionSnapshot(nodeName, state, state == NodeConnectionSessionState.Online ? DateTimeOffset.UtcNow : null, "session");
        }

        public SshCredentialReference? GetConnectedCredential(string nodeName)
        {
            return credential;
        }

        public IReadOnlyList<NodeConnectionSessionSnapshot> ListActiveSessions()
        {
            return state == NodeConnectionSessionState.Online
                ? [new NodeConnectionSessionSnapshot("node", state, DateTimeOffset.UtcNow, "SSH 在线。")]
                : [];
        }
    }

    private sealed class FakeRemoteFileTransferService(
        FrpNexusStatus status = FrpNexusStatus.Ready,
        string message = "TOML 配置上传成功")
        : IRemoteFileTransferService
    {
        public RemoteConfigurationUploadRequest? LastConfigurationRequest { get; private set; }

        public Task<RemoteFilePresenceResult> CheckRemoteFilesAsync(
            RemoteFilePresenceRequest request,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<RemoteFilePresenceEntry> files = request.RemotePaths
                .Select(path => new RemoteFilePresenceEntry(path, true))
                .ToArray();
            return Task.FromResult(new RemoteFilePresenceResult(
                request.Node.Name,
                files,
                FrpNexusStatus.Ready,
                DateTimeOffset.UtcNow,
                "远程文件已就绪。"));
        }

        public Task<RemoteFileTransferResult> UploadFrpBinaryAsync(
            RemoteFileUploadRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RemoteFileTransferResult(
                request.Node.Name,
                request.RemotePath,
                status,
                DateTimeOffset.UtcNow,
                message));
        }

        public Task<RemoteFileTransferResult> UploadConfigurationAsync(
            RemoteConfigurationUploadRequest request,
            CancellationToken cancellationToken = default)
        {
            LastConfigurationRequest = request;
            return Task.FromResult(new RemoteFileTransferResult(
                request.Node.Name,
                request.RemotePath,
                status,
                DateTimeOffset.UtcNow,
                message));
        }

        public Task<RemoteFileDeleteResult> DeleteRemoteFilesAsync(
            RemoteFileDeleteRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RemoteFileDeleteResult(
                request.Node.Name,
                request.RemotePaths,
                [],
                status,
                DateTimeOffset.UtcNow,
                "远程文件已清理。"));
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
