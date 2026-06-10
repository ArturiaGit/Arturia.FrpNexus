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
            CreateNode("node-a", connectionStatus: FrpNexusStatus.Online),
            CreateNode("node-b", connectionStatus: FrpNexusStatus.Online)
        ]);
        var sessions = new FakeNodeConnectionSessionService();
        sessions.SetOnline("node-b");
        var viewModel = CreateViewModel(nodeService: nodes, sessionService: sessions);

        await viewModel.LoadDashboardAsync();

        Assert.Equal("2", viewModel.Metrics[0].Value);
        Assert.Equal("1", viewModel.Metrics[1].Value);
        Assert.Equal(FrpNexusStatus.Offline, viewModel.RecentNodes.Single(row => row.Name == "node-a").ConnectionStatus);
        Assert.Equal(FrpNexusStatus.Online, viewModel.RecentNodes.Single(row => row.Name == "node-b").ConnectionStatus);
    }

    [Fact]
    public async Task RuntimeAndTunnels_ShouldDriveActiveTunnelMetricButNotCurrentFrpProcessMetric()
    {
        var nodes = new FakeNodeManagementService(
        [
            CreateNode("node-a", frpStatus: FrpNexusStatus.Running, uptime: "00:13")
        ]);
        var runtime = new FakeRuntimeRecordService(
        [
            new("frpc-web", "node-a", "frpc", FrpNexusStatus.Running, "2048", "00:13", "127.0.0.1:8080"),
            new("frpc-db", "node-a", "frpc", FrpNexusStatus.Stopped, "-", "-", "127.0.0.1:3306")
        ]);
        var tunnels = new FakeTunnelManagementService(
        [
            new("web", TunnelProtocol.Http, "node-a", "127.0.0.1", 8080, "example.com", FrpNexusStatus.Running, "running"),
            new("udp", TunnelProtocol.Udp, "node-a", "127.0.0.1", 7777, "7777", FrpNexusStatus.Error, "port conflict")
        ]);
        var viewModel = CreateViewModel(nodeService: nodes, tunnelService: tunnels, runtimeService: runtime);

        await viewModel.LoadDashboardAsync();

        Assert.Equal("0", viewModel.Metrics[2].Value);
        Assert.Equal("1", viewModel.Metrics[3].Value);
        Assert.NotEqual("00:13", viewModel.RecentNodes.Single().Uptime);
    }

    [Fact]
    public async Task RunningProcessMetric_ShouldIgnoreHistoricalRuntimeRecords()
    {
        var runtime = new FakeRuntimeRecordService(
        [
            new("frps-main", "node-a", "frps", FrpNexusStatus.Running, "1001", "01:00", "0.0.0.0:7000"),
            new("frpc-web", "node-a", "frpc", FrpNexusStatus.Running, "1002", "00:30", "127.0.0.1:8080"),
            new("frpc-db", "node-a", "frpc", FrpNexusStatus.Stopped, "-", "-", "127.0.0.1:3306")
        ]);
        var viewModel = CreateViewModel(runtimeService: runtime, tunnelService: new FakeTunnelManagementService([]));

        await viewModel.LoadDashboardAsync();

        Assert.Equal("0", viewModel.Metrics[2].Value);
        Assert.Equal("0", viewModel.Metrics[3].Value);
    }

    [Fact]
    public async Task RunningProcessMetric_ShouldUseKnownManagedLocalFrpcAndRemoteFrpsSnapshots()
    {
        var localFrpc = new FakeLocalFrpcProcessService(
        [
            new("node-a", FrpNexusStatus.Running, "running", ProcessId: 1001, IsManaged: true),
            new("node-b", FrpNexusStatus.Running, "external", ProcessId: 1002, IsManaged: false),
            new("node-c", FrpNexusStatus.Stopped, "stopped", ProcessId: null, IsManaged: true)
        ]);
        var lifecycle = new FakeFrpLifecycleStateService(
        [
            new("node-a", true, FrpNexusStatus.Running),
            new("node-b", false, FrpNexusStatus.Running),
            new("node-c", true, FrpNexusStatus.Stopped)
        ]);
        var runtime = new FakeRuntimeRecordService(
        [
            new("frps-old-1", "node-a", "frps", FrpNexusStatus.Running, "2001", "01:00", "-"),
            new("frps-old-2", "node-a", "frps", FrpNexusStatus.Running, "2002", "01:30", "-")
        ]);
        var viewModel = CreateViewModel(
            runtimeService: runtime,
            localFrpcProcessService: localFrpc,
            frpLifecycleStateService: lifecycle);

        await viewModel.LoadDashboardAsync();

        Assert.Equal("2", viewModel.Metrics[2].Value);
    }

    [Fact]
    public async Task WarningAndErrorRecords_ShouldCreateIncidents()
    {
        var tunnels = new FakeTunnelManagementService(
        [
            new("udp", TunnelProtocol.Udp, "node-a", "127.0.0.1", 7777, "7777", FrpNexusStatus.Error, "port conflict")
        ]);
        var deployments = new FakeDeploymentRecordService(
        [
            new("upload-core", "node-a", "permission denied", FrpNexusStatus.Warning, new DateTimeOffset(2026, 6, 6, 12, 0, 0, TimeSpan.Zero))
        ]);
        var runtime = new FakeRuntimeRecordService(
        [
            new(
                "local-frpc:node-a",
                "node-a",
                "frpc",
                FrpNexusStatus.Error,
                "-",
                "current system needs Windows frpc.exe",
                "-")
        ]);
        var viewModel = CreateViewModel(tunnelService: tunnels, runtimeService: runtime, deploymentService: deployments);

        await viewModel.LoadDashboardAsync();

        Assert.True(viewModel.HasIncidents);
        Assert.Equal(3, viewModel.Incidents.Count);
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

        Assert.False(string.IsNullOrWhiteSpace(viewModel.StatusText));
        Assert.Equal("0", viewModel.Metrics[0].Value);
    }

    private static DashboardPageViewModel CreateViewModel(
        INodeManagementService? nodeService = null,
        ITunnelManagementService? tunnelService = null,
        IRuntimeRecordService? runtimeService = null,
        IDeploymentRecordService? deploymentService = null,
        INodeConnectionSessionService? sessionService = null,
        ILocalFrpcProcessService? localFrpcProcessService = null,
        IFrpLifecycleStateService? frpLifecycleStateService = null,
        INavigationRequestService? navigationService = null)
    {
        return new DashboardPageViewModel(
            nodeService ?? new FakeNodeManagementService([]),
            tunnelService ?? new FakeTunnelManagementService([]),
            runtimeService ?? new FakeRuntimeRecordService([]),
            deploymentService ?? new FakeDeploymentRecordService([]),
            sessionService ?? new FakeNodeConnectionSessionService(),
            localFrpcProcessService ?? new FakeLocalFrpcProcessService([]),
            frpLifecycleStateService ?? new FakeFrpLifecycleStateService([]),
            navigationService ?? new FakeNavigationRequestService());
    }

    private static NodeProfile CreateNode(
        string name,
        FrpNexusStatus connectionStatus = FrpNexusStatus.Offline,
        FrpNexusStatus frpStatus = FrpNexusStatus.Stopped,
        string uptime = "-")
    {
        return new NodeProfile(
            name,
            "10.0.0.1",
            22,
            "root",
            "session-password",
            "Ubuntu",
            connectionStatus,
            frpStatus,
            "-",
            uptime,
            "/opt/frp/frpc.toml");
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

        public Task ReplaceRuntimeProcessesForNodeAsync(
            string nodeName,
            IReadOnlyList<RuntimeProcess> replacementProcesses,
            CancellationToken cancellationToken = default)
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

    private sealed class FakeLocalFrpcProcessService(IReadOnlyList<LocalFrpcProcessSnapshot> snapshots) : ILocalFrpcProcessService
    {
        public Task<LocalFrpcProcessResult> ApplyNodeTunnelsAsync(
            LocalFrpcProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LocalFrpcProcessResult(
                request.Node.Name,
                FrpNexusStatus.Running,
                DateTimeOffset.UtcNow,
                "running"));
        }

        public Task<LocalFrpcProcessResult> StopNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LocalFrpcProcessResult(
                nodeName,
                FrpNexusStatus.Stopped,
                DateTimeOffset.UtcNow,
                "stopped"));
        }

        public LocalFrpcProcessSnapshot GetNodeStatus(string nodeName, string? expectedConfigPath = null)
        {
            return snapshots.FirstOrDefault(snapshot => string.Equals(snapshot.NodeName, nodeName, StringComparison.OrdinalIgnoreCase))
                ?? new LocalFrpcProcessSnapshot(nodeName, FrpNexusStatus.Stopped, "not running");
        }

        public IReadOnlyList<LocalFrpcProcessSnapshot> ListManagedSessions()
        {
            return snapshots;
        }
    }

    private sealed class FakeFrpLifecycleStateService(IReadOnlyList<RemoteFrpsLifecycleSnapshot> snapshots) : IFrpLifecycleStateService
    {
        public IReadOnlyList<RemoteFrpsLifecycleSnapshot> ListRemoteFrpsSnapshots()
        {
            return snapshots;
        }

        public void UpdateRemoteFrpsState(string nodeName, bool isSshOnline, FrpNexusStatus frpsStatus)
        {
        }

        public void RemoveRemoteFrpsState(string nodeName)
        {
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
            return Task.FromResult(new NodeConnectionSessionResult(node.Name, NodeConnectionSessionState.Online, DateTimeOffset.UtcNow, "connected"));
        }

        public Task<NodeConnectionSessionResult> DisconnectAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            _onlineNodes.Remove(nodeName);
            return Task.FromResult(new NodeConnectionSessionResult(nodeName, NodeConnectionSessionState.Disconnected, null, "disconnected"));
        }

        public NodeConnectionSessionSnapshot GetSessionStatus(string nodeName)
        {
            return _onlineNodes.Contains(nodeName)
                ? new NodeConnectionSessionSnapshot(nodeName, NodeConnectionSessionState.Online, DateTimeOffset.UtcNow, "connected")
                : new NodeConnectionSessionSnapshot(nodeName, NodeConnectionSessionState.Offline, null, "offline");
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
                    "connected"))
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
