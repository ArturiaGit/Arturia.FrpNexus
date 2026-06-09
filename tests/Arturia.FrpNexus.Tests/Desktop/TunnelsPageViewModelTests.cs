using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class TunnelsPageViewModelTests
{
    [Fact]
    public async Task LoadTunnelsAsync_ShouldPopulateTunnelsFromService()
    {
        var service = new FakeTunnelManagementService(
        [
            new("本地 HTTP", TunnelProtocol.Http, "本地测试节点", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中")
        ]);
        var viewModel = CreateViewModel(service);

        await viewModel.LoadTunnelsAsync();

        Assert.Single(viewModel.Tunnels);
        Assert.Equal("本地 HTTP", viewModel.Tunnels[0].Name);
        Assert.Equal("共 1 条记录", viewModel.TunnelCountText);
    }

    [Fact]
    public async Task LoadTunnelsAsync_ShouldKeepEmptyStateWhenDatabaseIsEmpty()
    {
        var service = new FakeTunnelManagementService([]);
        var viewModel = CreateViewModel(service);

        await viewModel.LoadTunnelsAsync();

        Assert.Empty(viewModel.Tunnels);
        Assert.Empty(viewModel.TunnelRows);
        Assert.Null(viewModel.SelectedTunnel);
        Assert.Equal("共 0 条记录", viewModel.TunnelCountText);
    }

    [Fact]
    public async Task SearchText_ShouldFilterTunnelRowsByNameNodeOrEndpoint()
    {
        var service = new FakeTunnelManagementService(CreateFilterTunnels());
        var viewModel = CreateViewModel(service);
        await viewModel.LoadTunnelsAsync();

        viewModel.SearchText = "gamma";

        var row = Assert.Single(viewModel.TunnelRows);
        Assert.Equal("udp-game-server", row.Name);
        Assert.Equal("共 1 条记录", viewModel.TunnelCountText);

        viewModel.SearchText = "60022";

        row = Assert.Single(viewModel.TunnelRows);
        Assert.Equal("ssh-bastion", row.Name);
    }

    [Fact]
    public async Task SelectedProtocolFilter_ShouldFilterTunnelRowsByProtocol()
    {
        var service = new FakeTunnelManagementService(CreateFilterTunnels());
        var viewModel = CreateViewModel(service);
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedProtocolFilter = "TCP";

        Assert.Equal(2, viewModel.TunnelRows.Count);
        Assert.All(viewModel.TunnelRows, row => Assert.Equal(TunnelProtocol.Tcp, row.Protocol));
        Assert.Equal("共 2 条记录", viewModel.TunnelCountText);

        viewModel.SelectedProtocolFilter = TunnelsPageViewModel.AllProtocolFilter;

        Assert.Equal(4, viewModel.TunnelRows.Count);
    }

    [Fact]
    public async Task SaveTunnelCommand_ShouldCreateTunnelAndRefreshList()
    {
        var service = new FakeTunnelManagementService([]);
        var viewModel = CreateViewModel(service);
        await viewModel.LoadTunnelsAsync();

        await viewModel.StartCreateTunnelCommand.ExecuteAsync(null);
        viewModel.FormName = "api-local";
        viewModel.FormProtocol = TunnelProtocol.Https;
        viewModel.FormNodeName = "Node-Alpha-HK";
        viewModel.FormLocalAddress = "127.0.0.1";
        viewModel.FormLocalPort = "8443";
        viewModel.FormRemoteEndpoint = "api.example.com";
        viewModel.FormStatusDetail = "本地新增";

        await viewModel.SaveTunnelCommand.ExecuteAsync(null);

        Assert.Contains(viewModel.Tunnels, tunnel => tunnel.Name == "api-local" && tunnel.Protocol == TunnelProtocol.Https);
        Assert.Equal("api-local", viewModel.SelectedTunnel?.Name);
        Assert.False(viewModel.IsEditorOpen);
    }

    [Fact]
    public async Task StartCreateTunnelCommand_ShouldLeaveDefaultBackedFieldsEmpty()
    {
        var service = new FakeTunnelManagementService([]);
        var viewModel = CreateViewModel(service);
        await viewModel.LoadTunnelsAsync();

        await viewModel.StartCreateTunnelCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsEditingExistingTunnel);
        Assert.Equal("Node-Alpha-HK", viewModel.FormNodeName);
        Assert.Equal(string.Empty, viewModel.FormLocalAddress);
        Assert.Equal(string.Empty, viewModel.FormLocalPort);
        Assert.Equal(string.Empty, viewModel.FormStatusDetail);
    }

    [Fact]
    public async Task LoadTunnelsAsync_ShouldPopulateNodeOptionsFromNodeService()
    {
        var viewModel = CreateViewModel(new FakeTunnelManagementService([]));

        await viewModel.LoadTunnelsAsync();

        Assert.Equal(["Node-Alpha-HK", "Node-Beta-SG", "Node-Gamma-JP", "本地测试节点"], viewModel.NodeOptions.ToArray());
    }

    [Fact]
    public async Task StartCreateTunnelCommand_ShouldRequireExistingNodeWhenNoNodesExist()
    {
        var service = new FakeTunnelManagementService([]);
        var viewModel = CreateViewModel(service, []);
        await viewModel.LoadTunnelsAsync();

        await viewModel.StartCreateTunnelCommand.ExecuteAsync(null);
        viewModel.FormName = "missing-node";
        viewModel.FormRemoteEndpoint = "missing-node.example.com";

        await viewModel.SaveTunnelCommand.ExecuteAsync(null);

        Assert.Equal("请选择一个已创建的节点。", viewModel.FormErrorText);
        Assert.Empty(viewModel.Tunnels);
    }

    [Fact]
    public async Task SaveTunnelCommand_ShouldApplyDefaultsForOptionalBlankFields()
    {
        var service = new FakeTunnelManagementService([]);
        var viewModel = CreateViewModel(service);
        await viewModel.LoadTunnelsAsync();

        await viewModel.StartCreateTunnelCommand.ExecuteAsync(null);
        viewModel.FormName = "defaults-test";
        viewModel.FormNodeName = "Node-Alpha-HK";
        viewModel.FormLocalAddress = string.Empty;
        viewModel.FormLocalPort = string.Empty;
        viewModel.FormRemoteEndpoint = "defaults.example.com";
        viewModel.FormStatusDetail = string.Empty;

        await viewModel.SaveTunnelCommand.ExecuteAsync(null);

        var tunnel = viewModel.Tunnels.Single(item => item.Name == "defaults-test");
        Assert.Equal(TunnelsPageViewModel.DefaultLocalAddress, tunnel.LocalAddress);
        Assert.Equal(int.Parse(TunnelsPageViewModel.DefaultLocalPort), tunnel.LocalPort);
        Assert.Equal(TunnelsPageViewModel.DefaultStatusDetail, tunnel.StatusDetail);
    }

    [Fact]
    public async Task SaveTunnelCommand_ShouldRejectBlankNodeNameWithoutDefault()
    {
        var service = new FakeTunnelManagementService([]);
        var viewModel = CreateViewModel(service);
        await viewModel.LoadTunnelsAsync();

        await viewModel.StartCreateTunnelCommand.ExecuteAsync(null);
        viewModel.FormName = "missing-node";
        viewModel.FormNodeName = string.Empty;
        viewModel.FormRemoteEndpoint = "missing-node.example.com";

        await viewModel.SaveTunnelCommand.ExecuteAsync(null);

        Assert.Equal("请选择一个已创建的节点。", viewModel.FormErrorText);
        Assert.DoesNotContain(viewModel.Tunnels, tunnel => tunnel.Name == "missing-node");
    }

    [Fact]
    public async Task SaveTunnelCommand_ShouldUpdateExistingTunnel()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Stopped, "本地记录")
        ]);
        var viewModel = CreateViewModel(service);
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedTunnel = viewModel.Tunnels.Single();
        await viewModel.StartEditSelectedTunnelCommand.ExecuteAsync(null);
        viewModel.FormLocalPort = "9090";
        viewModel.FormRemoteEndpoint = "new.example.com";

        await viewModel.SaveTunnelCommand.ExecuteAsync(null);

        var tunnel = Assert.Single(viewModel.Tunnels);
        Assert.Equal(9090, tunnel.LocalPort);
        Assert.Equal("new.example.com", tunnel.RemoteEndpoint);
    }

    [Fact]
    public async Task StartEditSelectedTunnelCommand_ShouldShowSavedValues()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "10.0.0.8", 9090, "dev.example.com", FrpNexusStatus.Stopped, "已保存说明")
        ]);
        var viewModel = CreateViewModel(service);
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedTunnel = viewModel.Tunnels.Single();
        await viewModel.StartEditSelectedTunnelCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsEditingExistingTunnel);
        Assert.Equal("web-dev", viewModel.FormName);
        Assert.Equal("Node-Alpha-HK", viewModel.FormNodeName);
        Assert.Equal("10.0.0.8", viewModel.FormLocalAddress);
        Assert.Equal("9090", viewModel.FormLocalPort);
        Assert.Equal("dev.example.com", viewModel.FormRemoteEndpoint);
        Assert.Equal("已保存说明", viewModel.FormStatusDetail);
    }

    [Fact]
    public async Task SaveTunnelCommand_ShouldRejectDeletedNodeWhenEditingOldTunnel()
    {
        var service = new FakeTunnelManagementService(
        [
            new("orphan-tunnel", TunnelProtocol.Http, "Deleted-Node", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Stopped, "旧记录")
        ]);
        var viewModel = CreateViewModel(service);
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedTunnel = viewModel.Tunnels.Single();
        await viewModel.StartEditSelectedTunnelCommand.ExecuteAsync(null);
        Assert.Equal("Deleted-Node", viewModel.FormNodeName);
        Assert.Contains("Deleted-Node", viewModel.NodeOptions);

        await viewModel.SaveTunnelCommand.ExecuteAsync(null);

        Assert.Equal("请选择一个已创建的节点。", viewModel.FormErrorText);
    }

    [Fact]
    public async Task CancelEditCommand_ShouldResetExistingEditState()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Stopped, "本地记录")
        ]);
        var viewModel = CreateViewModel(service);
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedTunnel = viewModel.Tunnels.Single();
        await viewModel.StartEditSelectedTunnelCommand.ExecuteAsync(null);
        viewModel.CancelEditCommand.Execute(null);

        Assert.False(viewModel.IsEditorOpen);
        Assert.False(viewModel.IsEditingExistingTunnel);
        Assert.Equal("删除", viewModel.DeleteButtonText);
    }

    [Fact]
    public async Task DeleteSelectedTunnelCommand_ShouldRequireConfirmationAndDelete()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Stopped, "本地记录")
        ]);
        var viewModel = CreateViewModel(service);
        await viewModel.LoadTunnelsAsync();
        viewModel.SelectedTunnel = viewModel.Tunnels.Single();
        await viewModel.StartEditSelectedTunnelCommand.ExecuteAsync(null);

        await viewModel.DeleteSelectedTunnelCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Tunnels);
        Assert.True(viewModel.IsEditingExistingTunnel);
        Assert.Equal("确认删除", viewModel.DeleteButtonText);

        await viewModel.DeleteSelectedTunnelCommand.ExecuteAsync(null);

        Assert.DoesNotContain(viewModel.Tunnels, tunnel => tunnel.Name == "web-dev");
        Assert.False(viewModel.IsEditorOpen);
        Assert.False(viewModel.IsEditingExistingTunnel);
        Assert.Equal("删除", viewModel.DeleteButtonText);
    }

    [Fact]
    public async Task DeleteSelectedTunnelCommand_ShouldKeepListEmptyAfterDeletingLastTunnel()
    {
        var service = new FakeTunnelManagementService(
        [
            new("last-tunnel", TunnelProtocol.Tcp, "Node-Alpha-HK", "127.0.0.1", 22, "60022", FrpNexusStatus.Stopped, "本地记录")
        ]);
        var viewModel = CreateViewModel(service);
        await viewModel.LoadTunnelsAsync();
        viewModel.SelectedTunnel = viewModel.Tunnels.Single();
        await viewModel.StartEditSelectedTunnelCommand.ExecuteAsync(null);

        await viewModel.DeleteSelectedTunnelCommand.ExecuteAsync(null);
        await viewModel.DeleteSelectedTunnelCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.Tunnels);
        Assert.Empty(viewModel.TunnelRows);
        Assert.Null(viewModel.SelectedTunnel);
        Assert.Equal("共 0 条记录", viewModel.TunnelCountText);
        Assert.False(viewModel.IsEditorOpen);
        Assert.DoesNotContain(viewModel.Tunnels, tunnel => tunnel.Name is "web-dev-portal" or "ssh-bastion");
    }

    [Fact]
    public async Task SaveTunnelCommand_ShouldRejectInvalidForm()
    {
        var service = new FakeTunnelManagementService([]);
        var viewModel = CreateViewModel(service);
        await viewModel.StartCreateTunnelCommand.ExecuteAsync(null);
        viewModel.FormName = string.Empty;
        viewModel.FormLocalPort = "70000";

        await viewModel.SaveTunnelCommand.ExecuteAsync(null);

        Assert.Equal("隧道名称不能为空。", viewModel.FormErrorText);
        Assert.DoesNotContain(viewModel.Tunnels, tunnel => string.IsNullOrWhiteSpace(tunnel.Name));
    }

    [Fact]
    public async Task SaveTunnelCommand_ShouldSupportAllMvpProtocols()
    {
        var service = new FakeTunnelManagementService([]);
        var viewModel = CreateViewModel(service);
        await viewModel.LoadTunnelsAsync();

        foreach (var protocol in new[] { TunnelProtocol.Tcp, TunnelProtocol.Udp, TunnelProtocol.Http, TunnelProtocol.Https })
        {
            await viewModel.StartCreateTunnelCommand.ExecuteAsync(null);
            viewModel.FormName = $"custom-{protocol}";
            viewModel.FormProtocol = protocol;
            viewModel.FormNodeName = "Node-Alpha-HK";
            viewModel.FormLocalAddress = "127.0.0.1";
            viewModel.FormLocalPort = protocol == TunnelProtocol.Tcp ? "22" : "8080";
            viewModel.FormRemoteEndpoint = protocol is TunnelProtocol.Http or TunnelProtocol.Https
                ? $"{protocol}.example.com"
                : "60022";

            await viewModel.SaveTunnelCommand.ExecuteAsync(null);
        }

        Assert.Contains(viewModel.Tunnels, tunnel => tunnel.Name == "custom-Tcp" && tunnel.Protocol == TunnelProtocol.Tcp);
        Assert.Contains(viewModel.Tunnels, tunnel => tunnel.Name == "custom-Udp" && tunnel.Protocol == TunnelProtocol.Udp);
        Assert.Contains(viewModel.Tunnels, tunnel => tunnel.Name == "custom-Http" && tunnel.Protocol == TunnelProtocol.Http);
        Assert.Contains(viewModel.Tunnels, tunnel => tunnel.Name == "custom-Https" && tunnel.Protocol == TunnelProtocol.Https);
    }

    [Fact]
    public async Task ToggleTunnelRuntimeCommand_ShouldApplyNodeTunnelsAndStopLastNodeSession()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Stopped, "本地记录")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            new Arturia.FrpNexus.Application.Configuration.TomlConfigurationService(),
            localFrpc);
        await viewModel.LoadTunnelsAsync();

        await viewModel.ToggleTunnelRuntimeCommand.ExecuteAsync(viewModel.TunnelRows.Single());

        Assert.Equal(1, localFrpc.ApplyCount);
        Assert.Equal("Node-Alpha-HK", localFrpc.LastApplyRequest?.Node.Name);
        Assert.Equal(["web-dev"], localFrpc.LastApplyRequest?.EnabledTunnels.Select(tunnel => tunnel.Name).ToArray());
        Assert.Equal("本地 frpc 已按节点应用配置。", viewModel.RuntimeStatusText);
        Assert.Equal(FrpNexusStatus.Running, viewModel.Tunnels.Single().Status);

        await viewModel.ToggleTunnelRuntimeCommand.ExecuteAsync(viewModel.TunnelRows.Single());

        Assert.Equal(1, localFrpc.StopNodeCount);
        Assert.Equal("Node-Alpha-HK", localFrpc.LastStoppedNodeName);
        Assert.Equal("该节点本地 frpc 已停止。", viewModel.RuntimeStatusText);
        Assert.Equal(FrpNexusStatus.Stopped, viewModel.Tunnels.Single().Status);
    }

    [Fact]
    public async Task ToggleTunnelRuntimeCommand_ShouldReloadSameNodeWithoutStoppingOtherRunningTunnel()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中"),
            new("api-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8081, "api.example.com", FrpNexusStatus.Running, "运行中")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        localFrpc.MarkNodeRunning("Node-Alpha-HK");
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            new Arturia.FrpNexus.Application.Configuration.TomlConfigurationService(),
            localFrpc);
        await viewModel.LoadTunnelsAsync();

        await viewModel.ToggleTunnelRuntimeCommand.ExecuteAsync(viewModel.TunnelRows.Single(row => row.Name == "web-dev"));

        Assert.Equal(1, localFrpc.ApplyCount);
        Assert.Equal(0, localFrpc.StopNodeCount);
        Assert.Equal(["api-dev"], localFrpc.LastApplyRequest?.EnabledTunnels.Select(tunnel => tunnel.Name).ToArray());
        Assert.Equal(FrpNexusStatus.Stopped, viewModel.Tunnels.Single(tunnel => tunnel.Name == "web-dev").Status);
        Assert.Equal(FrpNexusStatus.Running, viewModel.Tunnels.Single(tunnel => tunnel.Name == "api-dev").Status);
    }

    [Fact]
    public void GenerateClientToml_ShouldIncludeMultipleProxiesAndWebServer()
    {
        var service = new Arturia.FrpNexus.Application.Configuration.TomlConfigurationService();
        var node = CreateNodeOptions().Single(item => item.Name == "Node-Alpha-HK");
        var toml = service.GenerateClientToml(node,
        [
            new("web-dev", TunnelProtocol.Http, node.Name, "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中"),
            new("ssh-dev", TunnelProtocol.Tcp, node.Name, "127.0.0.1", 22, "60022", FrpNexusStatus.Running, "运行中")
        ], 7400);

        Assert.Contains("serverAddr = \"10.0.0.1\"", toml);
        Assert.Contains("serverPort = 7000", toml);
        Assert.Contains("[webServer]", toml);
        Assert.Contains("port = 7400", toml);
        Assert.Equal(2, CountOccurrences(toml, "[[proxies]]"));
        Assert.Contains("name = \"web-dev\"", toml);
        Assert.Contains("customDomains = [\"dev.example.com\"]", toml);
        Assert.Contains("name = \"ssh-dev\"", toml);
        Assert.Contains("remotePort = 60022", toml);
    }

    private static IReadOnlyList<TunnelProfile> CreateFilterTunnels()
    {
        return
        [
            new("web-dev-portal", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中"),
            new("ssh-bastion", TunnelProtocol.Tcp, "Node-Beta-SG", "127.0.0.1", 22, "60022", FrpNexusStatus.Running, "运行中"),
            new("database-test", TunnelProtocol.Tcp, "Node-Alpha-HK", "127.0.0.1", 3306, "13306", FrpNexusStatus.Stopped, "已停止"),
            new("udp-game-server", TunnelProtocol.Udp, "Node-Gamma-JP", "127.0.0.1", 7777, "7777", FrpNexusStatus.Error, "端口被占用")
        ];
    }

    private static TunnelsPageViewModel CreateViewModel(
        ITunnelManagementService tunnelService,
        IReadOnlyList<NodeProfile>? nodes = null)
    {
        return new TunnelsPageViewModel(
            tunnelService,
            new FakeNodeManagementService(nodes ?? CreateNodeOptions()),
            new Arturia.FrpNexus.Application.Configuration.TomlConfigurationService(),
            new FakeLocalFrpcProcessService());
    }

    private static IReadOnlyList<NodeProfile> CreateNodeOptions()
    {
        return
        [
            new("Node-Beta-SG", "10.0.0.2", 22, "root", "会话密码", "Ubuntu", FrpNexusStatus.Offline, FrpNexusStatus.Stopped, "-", "-", "/opt/frp/frpc.toml"),
            new("Node-Alpha-HK", "10.0.0.1", 22, "root", "会话密码", "Ubuntu", FrpNexusStatus.Offline, FrpNexusStatus.Stopped, "-", "-", "/opt/frp/frpc.toml"),
            new("Node-Gamma-JP", "10.0.0.3", 22, "root", "会话密码", "Ubuntu", FrpNexusStatus.Offline, FrpNexusStatus.Stopped, "-", "-", "/opt/frp/frpc.toml"),
            new("本地测试节点", "10.0.0.4", 22, "root", "会话密码", "Ubuntu", FrpNexusStatus.Offline, FrpNexusStatus.Stopped, "-", "-", "/opt/frp/frpc.toml")
        ];
    }

    private static int CountOccurrences(string value, string fragment)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(fragment, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += fragment.Length;
        }

        return count;
    }

    private sealed class FakeTunnelManagementService(IReadOnlyList<TunnelProfile> tunnels) : ITunnelManagementService
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

    private sealed class FakeNodeManagementService(IReadOnlyList<NodeProfile> nodes) : INodeManagementService
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

        public Task UpdateConnectionTestResultAsync(string nodeName, FrpNexusStatus status, DateTimeOffset testedAt, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLocalFrpcProcessService : ILocalFrpcProcessService
    {
        private readonly HashSet<string> _runningNodes = new(System.StringComparer.OrdinalIgnoreCase);

        public int ApplyCount { get; private set; }

        public int StopNodeCount { get; private set; }

        public LocalFrpcProcessRequest? LastApplyRequest { get; private set; }

        public string? LastStoppedNodeName { get; private set; }

        public void MarkNodeRunning(string nodeName)
        {
            _runningNodes.Add(nodeName);
        }

        public Task<LocalFrpcProcessResult> ApplyNodeTunnelsAsync(LocalFrpcProcessRequest request, CancellationToken cancellationToken = default)
        {
            ApplyCount++;
            LastApplyRequest = request;
            _runningNodes.Add(request.Node.Name);
            return Task.FromResult(new LocalFrpcProcessResult(
                request.Node.Name,
                FrpNexusStatus.Running,
                DateTimeOffset.UtcNow,
                "本地 frpc 已按节点应用配置。"));
        }

        public Task<LocalFrpcProcessResult> StopNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            StopNodeCount++;
            LastStoppedNodeName = nodeName;
            _runningNodes.Remove(nodeName);
            return Task.FromResult(new LocalFrpcProcessResult(
                nodeName,
                FrpNexusStatus.Stopped,
                DateTimeOffset.UtcNow,
                "该节点本地 frpc 已停止。"));
        }

        public LocalFrpcProcessSnapshot GetNodeStatus(string nodeName)
        {
            return _runningNodes.Contains(nodeName)
                ? new LocalFrpcProcessSnapshot(nodeName, FrpNexusStatus.Running, "本地 frpc 正在运行。")
                : new LocalFrpcProcessSnapshot(nodeName, FrpNexusStatus.Stopped, "本地 frpc 未运行。");
        }
    }
}
