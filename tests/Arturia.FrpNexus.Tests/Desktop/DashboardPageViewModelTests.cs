using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.Services;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class DashboardPageViewModelTests
{
    [Fact]
    public async Task EmptyData_ShouldShowZeroMetricsAndEmptyStates()
    {
        var viewModel = CreateViewModel();

        await viewModel.LoadDashboardAsync();

        Assert.Equal(["0", "0", "0", "0"], viewModel.Metrics.Select(metric => metric.Value).ToArray());
        Assert.False(viewModel.HasRecentNodes);
        Assert.False(viewModel.HasIncidents);
        Assert.False(viewModel.HasLogs);
    }

    [Fact]
    public async Task LoadDashboardAsync_ShouldUseCurrentSessionStatusForOnlineNodes()
    {
        var nodes = new FakeNodeManagementService(
        [
            new("node-a", "10.0.0.1", 22, "root", "会话密码", "Ubuntu", FrpNexusStatus.Online, FrpNexusStatus.Stopped, "-", "-", "/opt/frp/frpc.toml"),
            new("node-b", "10.0.0.2", 22, "root", "会话密码", "Ubuntu", FrpNexusStatus.Online, FrpNexusStatus.Stopped, "-", "-", "/opt/frp/frpc.toml")
        ]);
        var sessions = new FakeNodeConnectionSessionService();
        sessions.SetOnline("node-b");
        var viewModel = CreateViewModel(nodeService: nodes, sessionService: sessions);

        await viewModel.LoadDashboardAsync();

        Assert.Equal("2", viewModel.Metrics.Single(metric => metric.Label == "节点总数").Value);
        Assert.Equal("1", viewModel.Metrics.Single(metric => metric.Label == "在线节点").Value);
        Assert.Equal(FrpNexusStatus.Offline, viewModel.RecentNodes.Single(row => row.Name == "node-a").ConnectionStatus);
        Assert.Equal(FrpNexusStatus.Online, viewModel.RecentNodes.Single(row => row.Name == "node-b").ConnectionStatus);
    }

    [Fact]
    public async Task RuntimeAndTunnels_ShouldDriveMetricsAndUptime()
    {
        var nodes = new FakeNodeManagementService(
        [
            new("node-a", "10.0.0.1", 22, "root", "会话密码", "Ubuntu", FrpNexusStatus.Offline, FrpNexusStatus.Stopped, "-", "-", "/opt/frp/frpc.toml")
        ]);
        var runtime = new FakeRuntimeRecordService(
        [
            new("frpc-web", "node-a", "frpc", FrpNexusStatus.Running, "2048", "00:13", "127.0.0.1:8080"),
            new("frpc-db", "node-a", "frpc", FrpNexusStatus.Stopped, "-", "-", "127.0.0.1:3306")
        ]);
        var tunnels = new FakeTunnelManagementService(
        [
            new("web", TunnelProtocol.Http, "node-a", "127.0.0.1", 8080, "example.com", FrpNexusStatus.Running, "运行中"),
            new("udp", TunnelProtocol.Udp, "node-a", "127.0.0.1", 7777, "7777", FrpNexusStatus.Error, "端口占用")
        ]);
        var viewModel = CreateViewModel(nodeService: nodes, tunnelService: tunnels, runtimeService: runtime);

        await viewModel.LoadDashboardAsync();

        Assert.Equal("1", viewModel.Metrics.Single(metric => metric.Label == "FRP 进程").Value);
        Assert.Equal("1", viewModel.Metrics.Single(metric => metric.Label == "活跃隧道").Value);
        Assert.Equal("00:13", viewModel.RecentNodes.Single().Uptime);
    }

    [Fact]
    public async Task RunningProcessMetric_ShouldUseRuntimeRecordsEvenWhenTunnelsAreEmpty()
    {
        var runtime = new FakeRuntimeRecordService(
        [
            new("frps-main", "node-a", "frps", FrpNexusStatus.Running, "1001", "01:00", "0.0.0.0:7000"),
            new("frpc-web", "node-a", "frpc", FrpNexusStatus.Running, "1002", "00:30", "127.0.0.1:8080"),
            new("frpc-db", "node-a", "frpc", FrpNexusStatus.Stopped, "-", "-", "127.0.0.1:3306")
        ]);
        var viewModel = CreateViewModel(runtimeService: runtime, tunnelService: new FakeTunnelManagementService([]));

        await viewModel.LoadDashboardAsync();

        Assert.Equal("2", viewModel.Metrics.Single(metric => metric.Label == "FRP 进程").Value);
        Assert.Equal("0", viewModel.Metrics.Single(metric => metric.Label == "活跃隧道").Value);
    }

    [Fact]
    public async Task WarningAndErrorRecords_ShouldCreateIncidents()
    {
        var tunnels = new FakeTunnelManagementService(
        [
            new("udp", TunnelProtocol.Udp, "node-a", "127.0.0.1", 7777, "7777", FrpNexusStatus.Error, "端口占用")
        ]);
        var deployments = new FakeDeploymentRecordService(
        [
            new("上传核心", "node-a", "权限不足", FrpNexusStatus.Warning, new DateTimeOffset(2026, 6, 6, 12, 0, 0, TimeSpan.Zero))
        ]);
        var runtime = new FakeRuntimeRecordService(
        [
            new(
                "local-frpc:node-a",
                "node-a",
                "frpc",
                FrpNexusStatus.Error,
                "-",
                "当前系统需要选择 Windows 版 frpc.exe，请重新选择核心文件。",
                "-")
        ]);
        var viewModel = CreateViewModel(tunnelService: tunnels, runtimeService: runtime, deploymentService: deployments);

        await viewModel.LoadDashboardAsync();

        Assert.True(viewModel.HasIncidents);
        Assert.Contains(viewModel.Incidents, incident => incident.Message.Contains("权限不足", StringComparison.Ordinal));
        Assert.Contains(viewModel.Incidents, incident => incident.Message.Contains("Windows 版 frpc.exe", StringComparison.Ordinal));
        Assert.Contains(viewModel.Incidents, incident => incident.Message.Contains("隧道 udp 异常", StringComparison.Ordinal));
    }

    [Fact]
    public void QuickActions_ShouldRequestNavigation()
    {
        var navigation = new FakeNavigationRequestService();
        var viewModel = CreateViewModel(navigationService: navigation);

        viewModel.NavigateToNodesCommand.Execute(null);
        viewModel.NavigateToTunnelsCommand.Execute(null);
        viewModel.NavigateToConfigurationsCommand.Execute(null);

        Assert.Equal(["nodes", "tunnels", "configurations"], navigation.RequestedKeys);
    }

    [Fact]
    public async Task ServiceFailure_ShouldShowRecoverableStatusText()
    {
        var viewModel = CreateViewModel(nodeService: new ThrowingNodeManagementService());

        await viewModel.LoadDashboardAsync();

        Assert.Contains("节点概览加载", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Equal("0", viewModel.Metrics.Single(metric => metric.Label == "节点总数").Value);
    }

    private static DashboardPageViewModel CreateViewModel(
        INodeManagementService? nodeService = null,
        ITunnelManagementService? tunnelService = null,
        IRuntimeRecordService? runtimeService = null,
        IDeploymentRecordService? deploymentService = null,
        INodeConnectionSessionService? sessionService = null,
        INavigationRequestService? navigationService = null)
    {
        return new DashboardPageViewModel(
            nodeService ?? new FakeNodeManagementService([]),
            tunnelService ?? new FakeTunnelManagementService([]),
            runtimeService ?? new FakeRuntimeRecordService([]),
            deploymentService ?? new FakeDeploymentRecordService([]),
            sessionService ?? new FakeNodeConnectionSessionService(),
            navigationService ?? new FakeNavigationRequestService());
    }

    private sealed class FakeNodeManagementService(IReadOnlyList<NodeProfile> nodes) : INodeManagementService
    {
        public Task<IReadOnlyList<NodeProfile>> ListNodesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(nodes);
        }

        public Task<NodeProfile?> GetNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(nodes.FirstOrDefault(node => node.Name == nodeName));
        }

        public Task SaveNodeAsync(NodeProfile node, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteNodeAsync(string nodeName, CancellationToken cancellationToken = default)
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

    private sealed class ThrowingNodeManagementService : INodeManagementService
    {
        public Task<IReadOnlyList<NodeProfile>> ListNodesAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("database is unavailable");
        }

        public Task<NodeProfile?> GetNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<NodeProfile?>(null);
        }

        public Task SaveNodeAsync(NodeProfile node, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteNodeAsync(string nodeName, CancellationToken cancellationToken = default)
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

        public Task<TunnelProfile?> GetTunnelAsync(string tunnelName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(tunnels.FirstOrDefault(tunnel => tunnel.Name == tunnelName));
        }

        public Task SaveTunnelAsync(TunnelProfile tunnel, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteTunnelAsync(string tunnelName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRuntimeRecordService(IReadOnlyList<RuntimeProcess> processes) : IRuntimeRecordService
    {
        public Task<IReadOnlyList<RuntimeProcess>> ListRuntimeProcessesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(processes);
        }

        public Task<RuntimeProcess?> GetRuntimeProcessAsync(string processName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(processes.FirstOrDefault(process => process.Name == processName));
        }

        public Task SaveRuntimeProcessAsync(RuntimeProcess process, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteRuntimeProcessAsync(string processName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDeploymentRecordService(IReadOnlyList<DeploymentRecord> records) : IDeploymentRecordService
    {
        public Task<IReadOnlyList<DeploymentRecord>> ListDeploymentRecordsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(records);
        }

        public Task<DeploymentRecord?> GetDeploymentRecordAsync(string stepName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(records.FirstOrDefault(record => record.StepName == stepName));
        }

        public Task SaveDeploymentRecordAsync(DeploymentRecord record, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteDeploymentRecordAsync(string stepName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNodeConnectionSessionService : INodeConnectionSessionService
    {
        private readonly HashSet<string> _onlineNodes = new(StringComparer.OrdinalIgnoreCase);

        public void SetOnline(string nodeName)
        {
            _onlineNodes.Add(nodeName);
        }

        public Task<NodeConnectionSessionResult> ConnectAsync(NodeProfile node, SshCredentialReference credential, CancellationToken cancellationToken = default)
        {
            SetOnline(node.Name);
            return Task.FromResult(new NodeConnectionSessionResult(node.Name, NodeConnectionSessionState.Online, DateTimeOffset.UtcNow, "已连接"));
        }

        public Task<NodeConnectionSessionResult> DisconnectAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            _onlineNodes.Remove(nodeName);
            return Task.FromResult(new NodeConnectionSessionResult(nodeName, NodeConnectionSessionState.Disconnected, null, "已断开"));
        }

        public NodeConnectionSessionSnapshot GetSessionStatus(string nodeName)
        {
            return _onlineNodes.Contains(nodeName)
                ? new NodeConnectionSessionSnapshot(nodeName, NodeConnectionSessionState.Online, DateTimeOffset.UtcNow, "已连接")
                : new NodeConnectionSessionSnapshot(nodeName, NodeConnectionSessionState.Offline, null, "尚未连接");
        }

        public SshCredentialReference? GetConnectedCredential(string nodeName)
        {
            return null;
        }

        public IReadOnlyList<NodeConnectionSessionSnapshot> ListActiveSessions()
        {
            return _onlineNodes
                .Select(nodeName => new NodeConnectionSessionSnapshot(
                    nodeName,
                    NodeConnectionSessionState.Online,
                    DateTimeOffset.UtcNow,
                    "已连接"))
                .ToArray();
        }
    }

    private sealed class FakeNavigationRequestService : INavigationRequestService
    {
        public event EventHandler<string>? NavigationRequested;

        public List<string> RequestedKeys { get; } = [];

        public void RequestNavigation(string pageKey)
        {
            RequestedKeys.Add(pageKey);
            NavigationRequested?.Invoke(this, pageKey);
        }
    }
}
