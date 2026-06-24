using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.Services;
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
        viewModel.FormRemark = "本地新增";

        await viewModel.SaveTunnelCommand.ExecuteAsync(null);

        Assert.Contains(viewModel.Tunnels, tunnel =>
            tunnel.Name == "api-local"
            && tunnel.Protocol == TunnelProtocol.Https
            && tunnel.Remark == "本地新增");
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
        Assert.Equal(string.Empty, viewModel.FormRemark);
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
        viewModel.FormRemark = string.Empty;

        await viewModel.SaveTunnelCommand.ExecuteAsync(null);

        var tunnel = viewModel.Tunnels.Single(item => item.Name == "defaults-test");
        Assert.Equal(TunnelsPageViewModel.DefaultLocalAddress, tunnel.LocalAddress);
        Assert.Equal(int.Parse(TunnelsPageViewModel.DefaultLocalPort), tunnel.LocalPort);
        Assert.Equal(string.Empty, tunnel.Remark);
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
        Assert.Equal("已保存说明", viewModel.FormRemark);
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
    public async Task SaveTunnelCommand_WhenTcpRemotePortConflictsWithServerPort_ShouldReject()
    {
        var service = new FakeTunnelManagementService([]);
        var viewModel = CreateViewModel(service);
        await viewModel.LoadTunnelsAsync();

        await viewModel.StartCreateTunnelCommand.ExecuteAsync(null);
        viewModel.FormName = "minecraft";
        viewModel.FormProtocol = TunnelProtocol.Tcp;
        viewModel.FormNodeName = "Node-Alpha-HK";
        viewModel.FormLocalAddress = "127.0.0.1";
        viewModel.FormLocalPort = "25565";
        viewModel.FormRemoteEndpoint = "7000";

        await viewModel.SaveTunnelCommand.ExecuteAsync(null);

        Assert.Contains("远程端口 7000 与 frps 服务端口冲突", viewModel.FormErrorText);
        Assert.DoesNotContain(viewModel.Tunnels, tunnel => tunnel.Name == "minecraft");
    }

    [Fact]
    public async Task RemoteEndpointLabel_ShouldFollowProtocol()
    {
        var viewModel = CreateViewModel(new FakeTunnelManagementService([]));
        await viewModel.StartCreateTunnelCommand.ExecuteAsync(null);

        viewModel.FormProtocol = TunnelProtocol.Tcp;

        Assert.Equal("远程端口", viewModel.RemoteEndpointLabel);
        Assert.Equal("25565 / 60000", viewModel.RemoteEndpointPlaceholder);

        viewModel.FormProtocol = TunnelProtocol.Http;

        Assert.Equal("域名", viewModel.RemoteEndpointLabel);
        Assert.Equal("example.com", viewModel.RemoteEndpointPlaceholder);
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
    public async Task TunnelRows_ShouldUseEnabledStateLabelsInsteadOfProcessLabels()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Stopped, "本地记录"),
            new("api-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8081, "api.example.com", FrpNexusStatus.Running, "本地记录"),
            new("broken-dev", TunnelProtocol.Tcp, "Node-Beta-SG", "127.0.0.1", 22, "60022", FrpNexusStatus.Error, "本地记录")
        ]);
        var viewModel = CreateViewModel(service);
        await viewModel.LoadTunnelsAsync();

        var stopped = viewModel.TunnelRows.Single(row => row.Name == "web-dev");
        Assert.Equal("已停用", stopped.StatusText);
        Assert.Equal("启用", stopped.RuntimeActionLabel);

        var running = viewModel.TunnelRows.Single(row => row.Name == "api-dev");
        Assert.Equal("已启用", running.StatusText);
        Assert.Equal("停用", running.RuntimeActionLabel);

        var error = viewModel.TunnelRows.Single(row => row.Name == "broken-dev");
        Assert.Equal("异常", error.StatusText);
        Assert.Equal("重试", error.RuntimeActionLabel);
    }

    [Fact]
    public async Task ToggleTunnelEnabledCommand_WhenFrpcStopped_ShouldOnlyUpdateTunnelState()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Stopped, "本地记录")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            localFrpc,
            new FakeLocalFrpcConfigurationService(),
            new FakeRuntimeRecordService(),
            new FakeFilePickerService());
        await viewModel.LoadTunnelsAsync();

        await viewModel.ToggleTunnelEnabledCommand.ExecuteAsync(viewModel.TunnelRows.Single());

        Assert.Equal(0, localFrpc.ApplyCount);
        Assert.Null(localFrpc.LastApplyRequest);
        Assert.Contains("本地 frpc 尚未运行", viewModel.RuntimeStatusText);
        Assert.Equal(FrpNexusStatus.Running, viewModel.Tunnels.Single().Status);
        Assert.Equal("启用隧道 1 条", viewModel.LocalFrpcEnabledTunnelCountText);
        Assert.Equal("未运行", viewModel.LocalFrpcStatusText);
    }

    [Fact]
    public async Task ToggleTunnelEnabledCommand_WhenFrpcRunning_ShouldReloadNodeConfig()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Stopped, "本地记录"),
            new("api-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8081, "api.example.com", FrpNexusStatus.Running, "本地记录"),
            new("ssh-dev", TunnelProtocol.Tcp, "Node-Beta-SG", "127.0.0.1", 22, "60022", FrpNexusStatus.Running, "本地记录")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        localFrpc.MarkNodeRunning("Node-Alpha-HK");
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            localFrpc,
            new FakeLocalFrpcConfigurationService(),
            new FakeRuntimeRecordService(),
            new FakeFilePickerService());
        await viewModel.LoadTunnelsAsync();

        await viewModel.ToggleTunnelEnabledCommand.ExecuteAsync(viewModel.TunnelRows.Single(row => row.Name == "web-dev"));

        Assert.Equal(1, localFrpc.ApplyCount);
        Assert.Equal(0, localFrpc.StopNodeCount);
        Assert.Equal("Node-Alpha-HK", localFrpc.LastApplyRequest?.Node.Name);
        Assert.Equal(["api-dev", "web-dev"], localFrpc.LastApplyRequest?.EnabledTunnels.Select(tunnel => tunnel.Name).Order().ToArray());
        Assert.Equal(FrpNexusStatus.Running, viewModel.Tunnels.Single(tunnel => tunnel.Name == "web-dev").Status);
        Assert.Equal(FrpNexusStatus.Running, viewModel.Tunnels.Single(tunnel => tunnel.Name == "api-dev").Status);
        Assert.Equal(FrpNexusStatus.Running, viewModel.Tunnels.Single(tunnel => tunnel.Name == "ssh-dev").Status);
        Assert.Equal("运行中", viewModel.LocalFrpcStatusText);
    }

    [Fact]
    public async Task ToggleTunnelEnabledCommand_WhenDisablingLastRunningTunnel_ShouldStopNodeFrpc()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "本地记录")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        localFrpc.MarkNodeRunning("Node-Alpha-HK");
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            localFrpc,
            new FakeLocalFrpcConfigurationService(),
            new FakeRuntimeRecordService(),
            new FakeFilePickerService());
        await viewModel.LoadTunnelsAsync();

        await viewModel.ToggleTunnelEnabledCommand.ExecuteAsync(viewModel.TunnelRows.Single());

        Assert.Equal(1, localFrpc.StopNodeCount);
        Assert.Equal("Node-Alpha-HK", localFrpc.LastStoppedNodeName);
        Assert.Contains("该节点本地 frpc 已停止。", viewModel.RuntimeStatusText);
        Assert.Equal(FrpNexusStatus.Stopped, viewModel.Tunnels.Single().Status);
        Assert.Equal("本地记录", viewModel.Tunnels.Single().Remark);
        Assert.Equal("未运行", viewModel.LocalFrpcStatusText);
    }

    [Fact]
    public async Task StartLocalFrpcCommand_ShouldApplyAllEnabledTunnelsForSelectedNode()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中"),
            new("api-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8081, "api.example.com", FrpNexusStatus.Running, "运行中"),
            new("ssh-dev", TunnelProtocol.Tcp, "Node-Beta-SG", "127.0.0.1", 22, "60022", FrpNexusStatus.Running, "运行中")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        var runtimeRecords = new FakeRuntimeRecordService();
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            localFrpc,
            new FakeLocalFrpcConfigurationService(),
            runtimeRecords,
            new FakeFilePickerService());
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedClientNodeName = "Node-Alpha-HK";
        await viewModel.StartLocalFrpcCommand.ExecuteAsync(null);

        Assert.Equal(1, localFrpc.ApplyCount);
        Assert.Equal("Node-Alpha-HK", localFrpc.LastApplyRequest?.Node.Name);
        Assert.Equal(["api-dev", "web-dev"], localFrpc.LastApplyRequest?.EnabledTunnels.Select(tunnel => tunnel.Name).Order().ToArray());
        Assert.Equal("运行中", viewModel.LocalFrpcStatusText);
        var runtimeRecord = Assert.Single(runtimeRecords.SavedRecords);
        Assert.Equal("local-frpc:Node-Alpha-HK", runtimeRecord.Name);
        Assert.Equal("frpc", runtimeRecord.ProcessKind);
        Assert.Equal(FrpNexusStatus.Running, runtimeRecord.Status);
        Assert.Equal("4321", runtimeRecord.ProcessId);
    }

    [Fact]
    public async Task ToggleLocalFrpcCommand_WhenStopped_ShouldStartSelectedNode()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            localFrpc,
            new FakeLocalFrpcConfigurationService(),
            new FakeRuntimeRecordService(),
            new FakeFilePickerService());
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedClientNodeName = "Node-Alpha-HK";
        Assert.Equal("启动 frpc", viewModel.LocalFrpcToggleButtonText);

        await viewModel.ToggleLocalFrpcCommand.ExecuteAsync(null);

        Assert.Equal(1, localFrpc.ApplyCount);
        Assert.Equal(0, localFrpc.StopNodeCount);
        Assert.Equal("停止 frpc", viewModel.LocalFrpcToggleButtonText);
        Assert.Equal("运行中", viewModel.LocalFrpcStatusText);
    }

    [Fact]
    public async Task ToggleLocalFrpcCommand_WhenManagedRunning_ShouldStopSelectedNode()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        localFrpc.MarkNodeRunning("Node-Alpha-HK");
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            localFrpc,
            new FakeLocalFrpcConfigurationService(),
            new FakeRuntimeRecordService(),
            new FakeFilePickerService());
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedClientNodeName = "Node-Alpha-HK";
        Assert.Equal("停止 frpc", viewModel.LocalFrpcToggleButtonText);

        await viewModel.ToggleLocalFrpcCommand.ExecuteAsync(null);

        Assert.Equal(0, localFrpc.ApplyCount);
        Assert.Equal(1, localFrpc.StopNodeCount);
        Assert.Equal("启动 frpc", viewModel.LocalFrpcToggleButtonText);
        Assert.Equal("未运行", viewModel.LocalFrpcStatusText);
    }

    [Fact]
    public async Task ToggleLocalFrpcCommand_WhenExternalUnmanaged_ShouldNotStopExternalProcess()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        localFrpc.MarkNodeUnmanaged("Node-Alpha-HK");
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            localFrpc,
            new FakeLocalFrpcConfigurationService(),
            new FakeRuntimeRecordService(),
            new FakeFilePickerService());
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedClientNodeName = "Node-Alpha-HK";
        Assert.Equal("启动 frpc", viewModel.LocalFrpcToggleButtonText);

        await viewModel.ToggleLocalFrpcCommand.ExecuteAsync(null);

        Assert.Equal(1, localFrpc.ApplyCount);
        Assert.Equal(0, localFrpc.StopNodeCount);
    }

    [Fact]
    public async Task LocalFrpcToggleButtonText_WhenBusy_ShouldShowProcessing()
    {
        var viewModel = CreateViewModel(new FakeTunnelManagementService([]));
        await viewModel.LoadTunnelsAsync();

        viewModel.IsTunnelRuntimeBusy = true;

        Assert.Equal("处理中...", viewModel.LocalFrpcToggleButtonText);

        viewModel.IsTunnelRuntimeBusy = false;

        Assert.Equal("启动 frpc", viewModel.LocalFrpcToggleButtonText);
    }

    [Fact]
    public async Task StartLocalFrpcCommand_WhenEnabledTunnelRemotePortConflicts_ShouldNotStartAndRecordError()
    {
        var service = new FakeTunnelManagementService(
        [
            new("minecraft", TunnelProtocol.Tcp, "Node-Alpha-HK", "127.0.0.1", 25565, "7000", FrpNexusStatus.Running, "运行中")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        var runtimeRecords = new FakeRuntimeRecordService();
        var config = new FakeLocalFrpcConfigurationService
        {
            FrpcBinaryPath = CreateExistingTempFile("frpc.exe")
        };
        config.NodeConfigPaths["Node-Alpha-HK"] = CreateTempConfigPath("alpha.frpc.toml");
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            localFrpc,
            config,
            runtimeRecords,
            new FakeFilePickerService());
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedClientNodeName = "Node-Alpha-HK";
        await viewModel.StartLocalFrpcCommand.ExecuteAsync(null);

        Assert.Equal(0, localFrpc.ApplyCount);
        Assert.Null(localFrpc.LastApplyRequest);
        Assert.Contains("远程端口 7000 与 frps 服务端口冲突", viewModel.RuntimeStatusText);
        var runtimeRecord = Assert.Single(runtimeRecords.SavedRecords);
        Assert.Equal("local-frpc:Node-Alpha-HK", runtimeRecord.Name);
        Assert.Equal(FrpNexusStatus.Error, runtimeRecord.Status);
        Assert.Contains("远程端口 7000 与 frps 服务端口冲突", runtimeRecord.Uptime);
    }

    [Fact]
    public async Task RefreshLocalFrpcStatusFromProcessAsync_WhenManagedProcessExited_ShouldShowErrorAndRecordRuntimeError()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        localFrpc.MarkNodeRunning("Node-Alpha-HK");
        var runtimeRecords = new FakeRuntimeRecordService();
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            localFrpc,
            new FakeLocalFrpcConfigurationService(),
            runtimeRecords,
            new FakeFilePickerService());
        await viewModel.LoadTunnelsAsync();

        localFrpc.MarkNodeExited("Node-Alpha-HK");
        await viewModel.RefreshLocalFrpcStatusFromProcessAsync();

        Assert.Equal("异常", viewModel.LocalFrpcStatusText);
        var runtimeRecord = Assert.Single(runtimeRecords.SavedRecords);
        Assert.Equal("local-frpc:Node-Alpha-HK", runtimeRecord.Name);
        Assert.Equal(FrpNexusStatus.Error, runtimeRecord.Status);
        Assert.Contains("本地 frpc 已退出", runtimeRecord.Uptime);
    }

    [Fact]
    public async Task RefreshLocalFrpcStatusFromProcessAsync_WhenExternalFrpcExists_ShouldShowUnmanagedWithoutStoppingIt()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        localFrpc.MarkNodeUnmanaged("Node-Alpha-HK");
        var runtimeRecords = new FakeRuntimeRecordService();
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            localFrpc,
            new FakeLocalFrpcConfigurationService(),
            runtimeRecords,
            new FakeFilePickerService());
        await viewModel.LoadTunnelsAsync();

        await viewModel.RefreshLocalFrpcStatusFromProcessAsync();

        Assert.Equal("未接管", viewModel.LocalFrpcStatusText);
        Assert.Equal(0, localFrpc.StopNodeCount);
        Assert.Empty(runtimeRecords.SavedRecords);
        Assert.Contains("不是 FrpNexus 启动", viewModel.RuntimeStatusText);
    }

    [Fact]
    public async Task StartLocalFrpcCommand_WhenNoEnabledTunnels_ShouldShowPromptWithoutStarting()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Stopped, "本地记录")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            localFrpc,
            new FakeLocalFrpcConfigurationService(),
            new FakeRuntimeRecordService(),
            new FakeFilePickerService());
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedClientNodeName = "Node-Alpha-HK";
        await viewModel.StartLocalFrpcCommand.ExecuteAsync(null);

        Assert.Equal(0, localFrpc.ApplyCount);
        Assert.Contains("没有启用隧道", viewModel.RuntimeStatusText);
        Assert.Equal("启用隧道 0 条", viewModel.LocalFrpcEnabledTunnelCountText);
    }

    [Fact]
    public async Task StopLocalFrpcCommand_ShouldStopSelectedNodeSession()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        localFrpc.MarkNodeRunning("Node-Alpha-HK");
        var runtimeRecords = new FakeRuntimeRecordService();
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            localFrpc,
            new FakeLocalFrpcConfigurationService(),
            runtimeRecords,
            new FakeFilePickerService());
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedClientNodeName = "Node-Alpha-HK";
        await viewModel.StopLocalFrpcCommand.ExecuteAsync(null);

        Assert.Equal(1, localFrpc.StopNodeCount);
        Assert.Equal("Node-Alpha-HK", localFrpc.LastStoppedNodeName);
        Assert.Equal("未运行", viewModel.LocalFrpcStatusText);
        var runtimeRecord = Assert.Single(runtimeRecords.SavedRecords);
        Assert.Equal("local-frpc:Node-Alpha-HK", runtimeRecord.Name);
        Assert.Equal(FrpNexusStatus.Stopped, runtimeRecord.Status);
    }

    [Fact]
    public async Task ReloadLocalFrpcCommand_WhenStopped_ShouldPromptToStartFirst()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            localFrpc,
            new FakeLocalFrpcConfigurationService(),
            new FakeRuntimeRecordService(),
            new FakeFilePickerService());
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedClientNodeName = "Node-Alpha-HK";
        await viewModel.ReloadLocalFrpcCommand.ExecuteAsync(null);

        Assert.Equal(0, localFrpc.ApplyCount);
        Assert.Contains("请先启动 frpc", viewModel.RuntimeStatusText);
    }

    [Fact]
    public async Task ReloadLocalFrpcCommand_WhenRunning_ShouldApplySelectedNodeConfig()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        localFrpc.MarkNodeRunning("Node-Alpha-HK");
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            localFrpc,
            new FakeLocalFrpcConfigurationService(),
            new FakeRuntimeRecordService(),
            new FakeFilePickerService());
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedClientNodeName = "Node-Alpha-HK";
        await viewModel.ReloadLocalFrpcCommand.ExecuteAsync(null);

        Assert.Equal(1, localFrpc.ApplyCount);
        Assert.Equal("Node-Alpha-HK", localFrpc.LastApplyRequest?.Node.Name);
        Assert.Equal(["web-dev"], localFrpc.LastApplyRequest?.EnabledTunnels.Select(tunnel => tunnel.Name).ToArray());
    }

    [Fact]
    public async Task SelectedClientNodeName_ShouldRefreshLocalFrpcSummary()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中"),
            new("db-dev", TunnelProtocol.Tcp, "Node-Alpha-HK", "127.0.0.1", 3306, "13306", FrpNexusStatus.Stopped, "已停用"),
            new("ssh-dev", TunnelProtocol.Tcp, "Node-Beta-SG", "127.0.0.1", 22, "60022", FrpNexusStatus.Running, "运行中")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        localFrpc.MarkNodeRunning("Node-Beta-SG");
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            localFrpc,
            new FakeLocalFrpcConfigurationService(),
            new FakeRuntimeRecordService(),
            new FakeFilePickerService());
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedClientNodeName = "Node-Alpha-HK";

        Assert.Equal("未运行", viewModel.LocalFrpcStatusText);
        Assert.Equal("启用隧道 1 条", viewModel.LocalFrpcEnabledTunnelCountText);

        viewModel.SelectedClientNodeName = "Node-Beta-SG";

        Assert.Equal("运行中", viewModel.LocalFrpcStatusText);
        Assert.Equal("启用隧道 1 条", viewModel.LocalFrpcEnabledTunnelCountText);
    }

    [Fact]
    public async Task LoadTunnelsAsync_ShouldShowSavedLocalFrpcPaths()
    {
        var config = new FakeLocalFrpcConfigurationService
        {
            FrpcBinaryPath = CreateExistingTempFile("frpc.exe")
        };
        config.NodeConfigPaths["Node-Alpha-HK"] = CreateTempConfigPath("alpha.frpc.toml");
        var viewModel = CreateViewModel(new FakeTunnelManagementService([]), configurationService: config);

        await viewModel.LoadTunnelsAsync();

        Assert.Equal(config.FrpcBinaryPath, viewModel.LocalFrpcBinaryPath);
        Assert.Equal(config.NodeConfigPaths["Node-Alpha-HK"], viewModel.LocalFrpcConfigPath);
        Assert.EndsWith("Node-Alpha-HK.frpc.toml", viewModel.LocalFrpcSuggestedConfigPath);
    }

    [Fact]
    public async Task SelectedClientNodeName_ShouldRefreshLocalFrpcConfigPathForNode()
    {
        var config = new FakeLocalFrpcConfigurationService();
        config.NodeConfigPaths["Node-Alpha-HK"] = CreateTempConfigPath("alpha.frpc.toml");
        config.NodeConfigPaths["Node-Beta-SG"] = CreateTempConfigPath("beta.frpc.toml");
        var viewModel = CreateViewModel(new FakeTunnelManagementService([]), configurationService: config);
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedClientNodeName = "Node-Beta-SG";
        await viewModel.LoadTunnelsAsync();

        Assert.Equal(config.NodeConfigPaths["Node-Beta-SG"], viewModel.LocalFrpcConfigPath);
    }

    [Fact]
    public async Task StartLocalFrpcCommand_ShouldPassSavedBinaryAndConfigPaths()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        var config = new FakeLocalFrpcConfigurationService
        {
            FrpcBinaryPath = CreateExistingTempFile("frpc.exe")
        };
        config.NodeConfigPaths["Node-Alpha-HK"] = CreateTempConfigPath("alpha.frpc.toml");
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            localFrpc,
            config,
            new FakeRuntimeRecordService(),
            new FakeFilePickerService());
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedClientNodeName = "Node-Alpha-HK";
        await viewModel.StartLocalFrpcCommand.ExecuteAsync(null);

        Assert.Equal(config.FrpcBinaryPath, localFrpc.LastApplyRequest?.FrpcBinaryPath);
        Assert.Equal(config.NodeConfigPaths["Node-Alpha-HK"], localFrpc.LastApplyRequest?.FrpcConfigPath);
    }

    [Fact]
    public async Task StartLocalFrpcCommand_ShouldApplyWithoutManagementPort()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        var config = new FakeLocalFrpcConfigurationService
        {
            FrpcBinaryPath = CreateExistingTempFile("frpc.exe")
        };
        config.NodeConfigPaths["Node-Alpha-HK"] = CreateTempConfigPath("alpha.frpc.toml");
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            localFrpc,
            config,
            new FakeRuntimeRecordService(),
            new FakeFilePickerService());
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedClientNodeName = "Node-Alpha-HK";
        await viewModel.StartLocalFrpcCommand.ExecuteAsync(null);

        Assert.Equal(1, localFrpc.ApplyCount);
        Assert.Equal(config.FrpcBinaryPath, localFrpc.LastApplyRequest?.FrpcBinaryPath);
        Assert.Equal(config.NodeConfigPaths["Node-Alpha-HK"], localFrpc.LastApplyRequest?.FrpcConfigPath);
    }

    [Fact]
    public async Task StartLocalFrpcCommand_ShouldRecordRunningWithoutManagementPort()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        var runtimeRecords = new FakeRuntimeRecordService();
        var config = new FakeLocalFrpcConfigurationService
        {
            FrpcBinaryPath = CreateExistingTempFile("frpc.exe")
        };
        config.NodeConfigPaths["Node-Alpha-HK"] = CreateTempConfigPath("alpha.frpc.toml");
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            localFrpc,
            config,
            runtimeRecords,
            new FakeFilePickerService());
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedClientNodeName = "Node-Alpha-HK";
        await viewModel.StartLocalFrpcCommand.ExecuteAsync(null);

        Assert.Equal(1, localFrpc.ApplyCount);
        var runtimeRecord = Assert.Single(runtimeRecords.SavedRecords);
        Assert.Equal(FrpNexusStatus.Running, runtimeRecord.Status);
        Assert.Equal("-", runtimeRecord.ListenAddress);
    }

    [Fact]
    public async Task StartLocalFrpcCommand_WhenSelectedBinaryMissing_ShouldShowPromptWithoutStarting()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        var runtimeRecords = new FakeRuntimeRecordService();
        var config = new FakeLocalFrpcConfigurationService
        {
            FrpcBinaryPath = Path.Combine(Path.GetTempPath(), "FrpNexusTests", "missing-frpc.exe")
        };
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            localFrpc,
            config,
            runtimeRecords,
            new FakeFilePickerService());
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedClientNodeName = "Node-Alpha-HK";
        await viewModel.StartLocalFrpcCommand.ExecuteAsync(null);

        Assert.Equal(0, localFrpc.ApplyCount);
        Assert.Contains("本地 frpc 核心文件不存在", viewModel.RuntimeStatusText);
        var runtimeRecord = Assert.Single(runtimeRecords.SavedRecords);
        Assert.Equal(FrpNexusStatus.Error, runtimeRecord.Status);
        Assert.Contains("本地 frpc 核心文件不存在", runtimeRecord.Uptime);
    }

    [Fact]
    public async Task StartLocalFrpcCommand_WhenWindowsBinaryIsNotExe_ShouldShowPromptAndRecordError()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService();
        var runtimeRecords = new FakeRuntimeRecordService();
        var config = new FakeLocalFrpcConfigurationService
        {
            FrpcBinaryPath = CreateExistingTempFile("frpc")
        };
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            localFrpc,
            config,
            runtimeRecords,
            new FakeFilePickerService());
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedClientNodeName = "Node-Alpha-HK";
        await viewModel.StartLocalFrpcCommand.ExecuteAsync(null);

        Assert.Equal(0, localFrpc.ApplyCount);
        Assert.Contains("Windows 版 frpc.exe", viewModel.RuntimeStatusText);
        var runtimeRecord = Assert.Single(runtimeRecords.SavedRecords);
        Assert.Equal("local-frpc:Node-Alpha-HK", runtimeRecord.Name);
        Assert.Equal(FrpNexusStatus.Error, runtimeRecord.Status);
        Assert.Contains("Windows 版 frpc.exe", runtimeRecord.Uptime);
    }

    [Fact]
    public async Task StartLocalFrpcCommand_WhenProcessServiceReturnsError_ShouldRecordRuntimeError()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中")
        ]);
        var localFrpc = new FakeLocalFrpcProcessService
        {
            NextApplyResultStatus = FrpNexusStatus.Error,
            NextApplyResultMessage = "当前系统无法运行所选 frpc 核心，请选择 Windows x64 版 frpc.exe。"
        };
        var runtimeRecords = new FakeRuntimeRecordService();
        var config = new FakeLocalFrpcConfigurationService
        {
            FrpcBinaryPath = CreateExistingTempFile("frpc.exe")
        };
        config.NodeConfigPaths["Node-Alpha-HK"] = CreateTempConfigPath("alpha.frpc.toml");
        var viewModel = new TunnelsPageViewModel(
            service,
            new FakeNodeManagementService(CreateNodeOptions()),
            localFrpc,
            config,
            runtimeRecords,
            new FakeFilePickerService());
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedClientNodeName = "Node-Alpha-HK";
        await viewModel.StartLocalFrpcCommand.ExecuteAsync(null);

        Assert.Equal(1, localFrpc.ApplyCount);
        var runtimeRecord = Assert.Single(runtimeRecords.SavedRecords);
        Assert.Equal("local-frpc:Node-Alpha-HK", runtimeRecord.Name);
        Assert.Equal("frpc", runtimeRecord.ProcessKind);
        Assert.Equal(FrpNexusStatus.Error, runtimeRecord.Status);
        Assert.Contains("Windows x64 版 frpc.exe", runtimeRecord.Uptime);
    }

    [Fact]
    public async Task SelectLocalFrpcPathCommands_ShouldSavePickedPaths()
    {
        var binaryPath = CreateExistingTempFile("picked-frpc.exe");
        var configPath = CreateTempConfigPath("picked.frpc.toml");
        var picker = new FakeFilePickerService
        {
            LocalFrpcBinaryPath = binaryPath,
            LocalFrpcConfigPath = configPath
        };
        var config = new FakeLocalFrpcConfigurationService();
        var viewModel = new TunnelsPageViewModel(
            new FakeTunnelManagementService([]),
            new FakeNodeManagementService(CreateNodeOptions()),
            new FakeLocalFrpcProcessService(),
            config,
            new FakeRuntimeRecordService(),
            picker);
        await viewModel.LoadTunnelsAsync();

        await viewModel.SelectLocalFrpcBinaryCommand.ExecuteAsync(null);
        await viewModel.SelectLocalFrpcConfigCommand.ExecuteAsync(null);

        Assert.Equal(binaryPath, config.FrpcBinaryPath);
        Assert.Equal(configPath, config.NodeConfigPaths[viewModel.SelectedClientNodeName]);
        Assert.Equal(binaryPath, viewModel.LocalFrpcBinaryPath);
        Assert.Equal(configPath, viewModel.LocalFrpcConfigPath);
    }

    [Fact]
    public void GenerateClientToml_ShouldIncludeMultipleProxiesWithoutWebServer()
    {
        var service = new Arturia.FrpNexus.Application.Configuration.TomlConfigurationService();
        var node = CreateNodeOptions().Single(item => item.Name == "Node-Alpha-HK");
        var toml = service.GenerateClientToml(node,
        [
            new("web-dev", TunnelProtocol.Http, node.Name, "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中"),
            new("ssh-dev", TunnelProtocol.Tcp, node.Name, "127.0.0.1", 22, "60022", FrpNexusStatus.Running, "运行中")
        ]);

        Assert.Contains("serverAddr = \"10.0.0.1\"", toml);
        Assert.Contains("serverPort = 7000", toml);
        Assert.DoesNotContain("[webServer]", toml);
        Assert.DoesNotContain("addr = \"127.0.0.1\"", toml);
        Assert.DoesNotContain("port = 7400", toml);
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

    [Fact]
    public async Task Dispose_ShouldStopLocalFrpcStatusPolling()
    {
        var viewModel = CreateViewModel(new FakeTunnelManagementService([]));

        Assert.True(viewModel.IsLocalFrpcStatusPollingActiveForTest);

        viewModel.Dispose();
        await WaitUntilAsync(() => !viewModel.IsLocalFrpcStatusPollingActiveForTest);

        Assert.False(viewModel.IsLocalFrpcStatusPollingActiveForTest);
    }

    private static TunnelsPageViewModel CreateViewModel(
        ITunnelManagementService tunnelService,
        IReadOnlyList<NodeProfile>? nodes = null,
        ILocalFrpcConfigurationService? configurationService = null,
        IFilePickerService? filePickerService = null)
    {
        return new TunnelsPageViewModel(
            tunnelService,
            new FakeNodeManagementService(nodes ?? CreateNodeOptions()),
            new FakeLocalFrpcProcessService(),
            configurationService ?? new FakeLocalFrpcConfigurationService(),
            new FakeRuntimeRecordService(),
            filePickerService ?? new FakeFilePickerService());
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

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }
    }

    private static string CreateExistingTempFile(string fileName)
    {
        var path = Path.Combine(Path.GetTempPath(), "FrpNexusTests", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
        return path;
    }

    private static string CreateTempConfigPath(string fileName)
    {
        var path = Path.Combine(Path.GetTempPath(), "FrpNexusTests", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
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
        private readonly Dictionary<string, LocalFrpcProcessSnapshot> _snapshots = new(System.StringComparer.OrdinalIgnoreCase);

        public FrpNexusStatus NextApplyResultStatus { get; set; } = FrpNexusStatus.Running;

        public string NextApplyResultMessage { get; set; } = "本地 frpc 已按节点应用配置。";

        public int ApplyCount { get; private set; }

        public int StopNodeCount { get; private set; }

        public LocalFrpcProcessRequest? LastApplyRequest { get; private set; }

        public string? LastStoppedNodeName { get; private set; }

        public void MarkNodeRunning(string nodeName)
        {
            _runningNodes.Add(nodeName);
            _snapshots[nodeName] = new LocalFrpcProcessSnapshot(
                nodeName,
                FrpNexusStatus.Running,
                "本地 frpc 正在运行。",
                4321,
                $"{nodeName}.frpc.toml");
        }

        public void MarkNodeExited(string nodeName)
        {
            _runningNodes.Remove(nodeName);
            _snapshots[nodeName] = new LocalFrpcProcessSnapshot(
                nodeName,
                FrpNexusStatus.Error,
                "本地 frpc 已退出。",
                ConfigPath: $"{nodeName}.frpc.toml",
                ExitCode: 1);
        }

        public void MarkNodeUnmanaged(string nodeName)
        {
            _runningNodes.Remove(nodeName);
            _snapshots[nodeName] = new LocalFrpcProcessSnapshot(
                nodeName,
                FrpNexusStatus.Warning,
                "检测到外部 frpc 正在使用当前配置，但它不是 FrpNexus 启动的进程。",
                9876,
                $"{nodeName}.frpc.toml",
                IsManaged: false);
        }

        public Task<LocalFrpcProcessResult> ApplyNodeTunnelsAsync(LocalFrpcProcessRequest request, CancellationToken cancellationToken = default)
        {
            ApplyCount++;
            LastApplyRequest = request;
            if (NextApplyResultStatus == FrpNexusStatus.Running)
            {
                MarkNodeRunning(request.Node.Name);
            }
            else
            {
                _runningNodes.Remove(request.Node.Name);
                _snapshots[request.Node.Name] = new LocalFrpcProcessSnapshot(
                    request.Node.Name,
                    NextApplyResultStatus,
                    NextApplyResultMessage);
            }

            return Task.FromResult(new LocalFrpcProcessResult(
                request.Node.Name,
                NextApplyResultStatus,
                DateTimeOffset.UtcNow,
                NextApplyResultMessage));
        }

        public Task<LocalFrpcProcessResult> StopNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            StopNodeCount++;
            LastStoppedNodeName = nodeName;
            _runningNodes.Remove(nodeName);
            _snapshots.Remove(nodeName);
            return Task.FromResult(new LocalFrpcProcessResult(
                nodeName,
                FrpNexusStatus.Stopped,
                DateTimeOffset.UtcNow,
                "该节点本地 frpc 已停止。"));
        }

        public LocalFrpcProcessSnapshot GetNodeStatus(string nodeName, string? expectedConfigPath = null)
        {
            if (_snapshots.TryGetValue(nodeName, out var snapshot))
            {
                return snapshot;
            }

            return _runningNodes.Contains(nodeName)
                ? new LocalFrpcProcessSnapshot(nodeName, FrpNexusStatus.Running, "本地 frpc 正在运行。", 4321, expectedConfigPath)
                : new LocalFrpcProcessSnapshot(nodeName, FrpNexusStatus.Stopped, "本地 frpc 未运行。");
        }
        public IReadOnlyList<LocalFrpcProcessSnapshot> ListManagedSessions()
        {
            return _snapshots.Values
                .Where(snapshot => snapshot.IsManaged && snapshot.Status == FrpNexusStatus.Running)
                .ToArray();
        }
    }
    private sealed class FakeRuntimeRecordService : IRuntimeRecordService
    {
        private readonly List<RuntimeProcess> _records = [];

        public IReadOnlyList<RuntimeProcess> SavedRecords => _records;

        public Task<IReadOnlyList<RuntimeProcess>> ListRuntimeProcessesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RuntimeProcess>>(_records);
        }

        public Task<RuntimeProcess?> GetRuntimeProcessAsync(string processName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_records.FirstOrDefault(process => process.Name == processName));
        }

        public Task SaveRuntimeProcessAsync(RuntimeProcess process, CancellationToken cancellationToken = default)
        {
            _records.RemoveAll(item => string.Equals(item.Name, process.Name, StringComparison.OrdinalIgnoreCase));
            _records.Add(process);
            return Task.CompletedTask;
        }

        public Task DeleteRuntimeProcessAsync(string processName, CancellationToken cancellationToken = default)
        {
            _records.RemoveAll(process => string.Equals(process.Name, processName, StringComparison.OrdinalIgnoreCase));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLocalFrpcConfigurationService : ILocalFrpcConfigurationService
    {
        public string FrpcBinaryPath { get; set; } = string.Empty;

        public Dictionary<string, string> NodeConfigPaths { get; } = new(System.StringComparer.OrdinalIgnoreCase);

        public Task<LocalFrpcConfigurationSnapshot> GetConfigurationAsync(
            string nodeName,
            CancellationToken cancellationToken = default)
        {
            var suggestedPath = GetDefaultNodeConfigPath(nodeName);
            var configPath = NodeConfigPaths.TryGetValue(nodeName, out var savedPath)
                ? savedPath
                : suggestedPath;
            return Task.FromResult(new LocalFrpcConfigurationSnapshot(
                FrpcBinaryPath,
                configPath,
                suggestedPath));
        }

        public Task SaveFrpcBinaryPathAsync(
            string frpcBinaryPath,
            CancellationToken cancellationToken = default)
        {
            FrpcBinaryPath = frpcBinaryPath;
            return Task.CompletedTask;
        }

        public Task SaveNodeConfigPathAsync(
            string nodeName,
            string frpcConfigPath,
            CancellationToken cancellationToken = default)
        {
            NodeConfigPaths[nodeName] = frpcConfigPath;
            return Task.CompletedTask;
        }

        public string GetDefaultNodeConfigPath(string nodeName)
        {
            return Path.Combine(Path.GetTempPath(), "FrpNexusTests", $"{nodeName}.frpc.toml");
        }
    }

    private sealed class FakeFilePickerService : IFilePickerService
    {
        public string? FrpBinaryPath { get; set; }

        public string? LocalFrpcBinaryPath { get; set; }

        public string? LocalFrpcConfigPath { get; set; }

        public Task<string?> PickFrpBinaryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(FrpBinaryPath);
        }

        public Task<string?> PickLocalFrpcBinaryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(LocalFrpcBinaryPath);
        }

        public Task<string?> PickLocalFrpcConfigPathAsync(
            string suggestedFileName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(LocalFrpcConfigPath);
        }

        public Task<string?> PickFrpDownloadDirectoryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }
    }
}
