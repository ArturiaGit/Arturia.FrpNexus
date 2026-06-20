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
    public async Task LocalWarningAndErrorLogs_ShouldPopulateSystemLogsAndIncidents()
    {
        var localLogs = new FakeLocalApplicationLogService(
        [
            new("2026-06-15 21:49:33.209", "INFO", "客户端", "FrpNexus", "application started", FrpNexusStatus.Ready),
            new("2026-06-15 21:50:33.209", "WARN", "客户端", "FrpNexus", "disk warning", FrpNexusStatus.Warning),
            new("2026-06-15 21:51:33.209", "ERROR", "客户端", "FrpNexus", "local failure", FrpNexusStatus.Error)
        ]);
        var viewModel = CreateViewModel(localLogService: localLogs);

        await viewModel.LoadDashboardAsync();

        Assert.True(viewModel.HasLogs);
        Assert.Contains(viewModel.Logs, log => log.Message == "local failure");
        Assert.True(viewModel.HasIncidents);
        Assert.Contains(viewModel.Incidents, incident => incident.Message.Contains("local failure", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnlineSessionWithRunningFrpProcess_ShouldReadRemoteLogs()
    {
        var nodes = new FakeNodeManagementService([CreateNode("node-a")]);
        var sessions = new FakeNodeConnectionSessionService();
        sessions.SetOnline("node-a", CreateCredential());
        var remoteRuntime = new FakeRemoteRuntimeService(
        [
            new("frpc-web", "node-a", "frpc", FrpNexusStatus.Running, "2048", "00:13", "127.0.0.1:8080"),
            new("other", "node-a", "nginx", FrpNexusStatus.Running, "100", "00:13", "127.0.0.1:80"),
            new("frps-stopped", "node-a", "frps", FrpNexusStatus.Stopped, "-", "-", "-")
        ]);
        var remoteLogs = new FakeRemoteLogService(
        [
            new("2026-06-15 22:00:00.000", "ERROR", "node-a", "frpc", "remote frpc failure", FrpNexusStatus.Error)
        ]);
        var viewModel = CreateViewModel(
            nodeService: nodes,
            sessionService: sessions,
            remoteRuntimeService: remoteRuntime,
            remoteLogService: remoteLogs);

        await viewModel.LoadDashboardAsync();

        Assert.NotEmpty(remoteLogs.Requests);
        Assert.All(remoteLogs.Requests, request => Assert.Equal("/tmp/frpnexus-frpc.log", request.LogPath));
        Assert.All(remoteLogs.Requests, request => Assert.Equal("frpc", request.ProcessName));
        Assert.Contains(viewModel.Logs, log => log.Message == "remote frpc failure");
    }

    [Fact]
    public async Task RemoteLogFailure_ShouldKeepLocalLogsAndShowRecoverableStatusText()
    {
        var nodes = new FakeNodeManagementService([CreateNode("node-a")]);
        var sessions = new FakeNodeConnectionSessionService();
        sessions.SetOnline("node-a", CreateCredential());
        var remoteRuntime = new FakeRemoteRuntimeService(
        [
            new("frps-main", "node-a", "frps", FrpNexusStatus.Running, "7000", "00:13", "0.0.0.0:7000")
        ]);
        var localLogs = new FakeLocalApplicationLogService(
        [
            new("2026-06-15 21:49:33.209", "WARN", "客户端", "FrpNexus", "local warning", FrpNexusStatus.Warning)
        ]);
        var remoteLogs = new ThrowingRemoteLogService();
        var viewModel = CreateViewModel(
            nodeService: nodes,
            sessionService: sessions,
            remoteRuntimeService: remoteRuntime,
            localLogService: localLogs,
            remoteLogService: remoteLogs);

        await viewModel.LoadDashboardAsync();

        Assert.Contains(viewModel.Logs, log => log.Message == "local warning");
        Assert.Contains("远程日志", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnlineSessionWithoutCredential_ShouldSkipRemoteLogRead()
    {
        var nodes = new FakeNodeManagementService([CreateNode("node-a")]);
        var sessions = new FakeNodeConnectionSessionService();
        sessions.SetOnline("node-a");
        var remoteLogs = new FakeRemoteLogService([]);
        var viewModel = CreateViewModel(
            nodeService: nodes,
            sessionService: sessions,
            remoteLogService: remoteLogs);

        await viewModel.LoadDashboardAsync();

        Assert.Empty(remoteLogs.Requests);
    }

    [Fact]
    public async Task DashboardLogsAndIncidents_ShouldBeCapped()
    {
        var localLogs = new FakeLocalApplicationLogService(Enumerable
            .Range(1, 12)
            .Select(index => new LogEntry(
                $"2026-06-15 21:{index:00}:00.000",
                "ERROR",
                "客户端",
                "FrpNexus",
                $"failure {index}",
                FrpNexusStatus.Error))
            .ToArray());
        var viewModel = CreateViewModel(localLogService: localLogs);

        await viewModel.LoadDashboardAsync();

        Assert.Equal(8, viewModel.Logs.Count);
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
        INavigationRequestService? navigationService = null,
        ILocalApplicationLogService? localLogService = null,
        IRemoteLogService? remoteLogService = null,
        IRemoteRuntimeService? remoteRuntimeService = null)
    {
        return new DashboardPageViewModel(
            nodeService ?? new FakeNodeManagementService([]),
            tunnelService ?? new FakeTunnelManagementService([]),
            runtimeService ?? new FakeRuntimeRecordService([]),
            deploymentService ?? new FakeDeploymentRecordService([]),
            sessionService ?? new FakeNodeConnectionSessionService(),
            localFrpcProcessService ?? new FakeLocalFrpcProcessService([]),
            frpLifecycleStateService ?? new FakeFrpLifecycleStateService([]),
            navigationService ?? new FakeNavigationRequestService(),
            localLogService ?? new FakeLocalApplicationLogService([]),
            remoteLogService ?? new FakeRemoteLogService([]),
            remoteRuntimeService ?? new FakeRemoteRuntimeService([]));
    }

    private static SshCredentialReference CreateCredential()
    {
        return new SshCredentialReference(SshAuthenticationMode.SessionPassword, null, "session-password", null);
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

        public void UpdateRemoteFrpsState(
            string nodeName,
            bool isSshOnline,
            FrpNexusStatus frpsStatus,
            string configPath = "")
        {
        }

        public void RemoveRemoteFrpsState(string nodeName)
        {
        }
    }

    private sealed class FakeNodeConnectionSessionService : INodeConnectionSessionService
    {
        private readonly HashSet<string> _onlineNodes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SshCredentialReference> _credentials = new(StringComparer.OrdinalIgnoreCase);

        public void SetOnline(string nodeName, SshCredentialReference? credential = null)
        {
            _onlineNodes.Add(nodeName);
            if (credential is not null)
            {
                _credentials[nodeName] = credential;
            }
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
            return _credentials.GetValueOrDefault(nodeName);
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

    private sealed class FakeLocalApplicationLogService(IReadOnlyList<LogEntry> logs) : ILocalApplicationLogService
    {
        public string CurrentLogDirectory => @"D:\FrpNexus\logs";

        public Task<IReadOnlyList<LogEntry>> ReadRecentLogsAsync(
            int lineCount = 200,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<LogEntry>>(logs.TakeLast(lineCount).ToArray());
        }
    }

    private sealed class FakeRemoteLogService(IReadOnlyList<LogEntry> logs) : IRemoteLogService
    {
        public List<RemoteLogReadRequest> Requests { get; } = [];

        public Task<IReadOnlyList<LogEntry>> ReadRecentLogsAsync(
            RemoteLogReadRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult<IReadOnlyList<LogEntry>>(logs);
        }

        public async IAsyncEnumerable<LogEntry> StreamLogsAsync(
            RemoteLogReadRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            foreach (var log in logs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return log;
                await Task.Yield();
            }
        }
    }

    private sealed class ThrowingRemoteLogService : IRemoteLogService
    {
        public Task<IReadOnlyList<LogEntry>> ReadRecentLogsAsync(
            RemoteLogReadRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("permission denied");
        }

        public async IAsyncEnumerable<LogEntry> StreamLogsAsync(
            RemoteLogReadRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw new InvalidOperationException("permission denied");
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }
    }

    private sealed class FakeRemoteRuntimeService(IReadOnlyList<RuntimeProcess> processes) : IRemoteRuntimeService
    {
        public Task<IReadOnlyList<RuntimeProcess>> GetProcessesAsync(
            RemoteRuntimeQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RuntimeProcess>>(processes
                .Where(process => string.Equals(process.NodeName, request.Node.Name, StringComparison.OrdinalIgnoreCase))
                .ToArray());
        }

        public Task<RemoteRuntimeCommandResult> StartAsync(
            RemoteRuntimeCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateCommandResult(request));
        }

        public Task<RemoteRuntimeCommandResult> StopAsync(
            RemoteRuntimeCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateCommandResult(request));
        }

        public Task<RemoteRuntimeCommandResult> RestartAsync(
            RemoteRuntimeCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateCommandResult(request));
        }

        private static RemoteRuntimeCommandResult CreateCommandResult(RemoteRuntimeCommandRequest request)
        {
            return new RemoteRuntimeCommandResult(
                request.Node.Name,
                request.ProcessName,
                FrpNexusStatus.Running,
                DateTimeOffset.UtcNow,
                "ok");
        }
    }
}
