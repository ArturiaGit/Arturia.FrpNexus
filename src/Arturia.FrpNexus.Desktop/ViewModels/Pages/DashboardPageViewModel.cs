using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Logging;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed partial class DashboardPageViewModel : PageViewModel
{
    private const string FrpcLogPath = "/tmp/frpnexus-frpc.log";
    private const string FrpsLogPath = "/tmp/frpnexus-frps.log";
    private const int DashboardLogLineCount = 20;
    private const int DashboardLogDisplayLimit = 8;
    private const int DashboardIncidentDisplayLimit = 3;

    private readonly INodeManagementService _nodeManagementService;
    private readonly ITunnelManagementService _tunnelManagementService;
    private readonly IRuntimeRecordService _runtimeRecordService;
    private readonly IDeploymentRecordService _deploymentRecordService;
    private readonly INodeConnectionSessionService _nodeConnectionSessionService;
    private readonly ILocalFrpcProcessService _localFrpcProcessService;
    private readonly IFrpLifecycleStateService _frpLifecycleStateService;
    private readonly INavigationRequestService _navigationRequestService;
    private readonly ILocalApplicationLogService _localApplicationLogService;
    private readonly IRemoteLogService _remoteLogService;
    private readonly IRemoteRuntimeService _remoteRuntimeService;

    [ObservableProperty]
    private string _statusText = "正在加载仪表盘数据...";

    [ObservableProperty]
    private bool _hasRecentNodes;

    [ObservableProperty]
    private bool _hasIncidents;

    [ObservableProperty]
    private bool _hasLogs;

    public DashboardPageViewModel(
        INodeManagementService nodeManagementService,
        ITunnelManagementService tunnelManagementService,
        IRuntimeRecordService runtimeRecordService,
        IDeploymentRecordService deploymentRecordService,
        INodeConnectionSessionService nodeConnectionSessionService,
        ILocalFrpcProcessService localFrpcProcessService,
        IFrpLifecycleStateService frpLifecycleStateService,
        INavigationRequestService navigationRequestService,
        ILocalApplicationLogService localApplicationLogService,
        IRemoteLogService remoteLogService,
        IRemoteRuntimeService remoteRuntimeService)
        : base("仪表盘概览", "查看节点、隧道、运行状态和近期告警")
    {
        _nodeManagementService = nodeManagementService;
        _tunnelManagementService = tunnelManagementService;
        _runtimeRecordService = runtimeRecordService;
        _deploymentRecordService = deploymentRecordService;
        _nodeConnectionSessionService = nodeConnectionSessionService;
        _localFrpcProcessService = localFrpcProcessService;
        _frpLifecycleStateService = frpLifecycleStateService;
        _navigationRequestService = navigationRequestService;
        _localApplicationLogService = localApplicationLogService;
        _remoteLogService = remoteLogService;
        _remoteRuntimeService = remoteRuntimeService;

        Metrics = [];
        RecentNodes = [];
        Incidents = [];
        Logs = [];
        SetMetrics(0, 0, 0, 0);

        _ = LoadDashboardAsync();
    }

    public ObservableCollection<MetricTileViewModel> Metrics { get; }

    public ObservableCollection<DashboardNodeRowViewModel> RecentNodes { get; }

    public ObservableCollection<IncidentViewModel> Incidents { get; }

    public ObservableCollection<LogEntry> Logs { get; }

    public Task LoadDashboardAsync(CancellationToken cancellationToken = default)
    {
        return LoadDashboardCoreAsync(cancellationToken);
    }

    [RelayCommand]
    private void NavigateToNodes()
    {
        _navigationRequestService.RequestNavigation("nodes");
    }

    [RelayCommand]
    private void NavigateToTunnels()
    {
        _navigationRequestService.RequestNavigation("tunnels");
    }

    [RelayCommand]
    private void NavigateToConfigurations()
    {
        _navigationRequestService.RequestNavigation("configurations");
    }

    private async Task LoadDashboardCoreAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<NodeProfile> nodes = [];
        IReadOnlyList<TunnelProfile> tunnels = [];
        IReadOnlyList<RuntimeProcess> processes = [];
        IReadOnlyList<DeploymentRecord> deployments = [];
        var failureMessages = new List<string>();

        try
        {
            nodes = await _nodeManagementService.ListNodesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            StatusText = "仪表盘加载已取消。";
            return;
        }
        catch (Exception ex)
        {
            failureMessages.Add(ViewModelErrorText.ForUser("节点概览加载", ex));
        }

        try
        {
            tunnels = await _tunnelManagementService.ListTunnelsAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            StatusText = "仪表盘加载已取消。";
            return;
        }
        catch (Exception ex)
        {
            failureMessages.Add(ViewModelErrorText.ForUser("隧道概览加载", ex));
        }

        try
        {
            processes = await _runtimeRecordService.ListRuntimeProcessesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            StatusText = "仪表盘加载已取消。";
            return;
        }
        catch (Exception ex)
        {
            failureMessages.Add(ViewModelErrorText.ForUser("运行概览加载", ex));
        }

        try
        {
            deployments = await _deploymentRecordService.ListDeploymentRecordsAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            StatusText = "仪表盘加载已取消。";
            return;
        }
        catch (Exception ex)
        {
            failureMessages.Add(ViewModelErrorText.ForUser("部署概览加载", ex));
        }

        var dashboardLogs = await LoadDashboardLogsAsync(nodes, failureMessages, cancellationToken);

        RefreshMetrics(nodes, tunnels, processes);
        RefreshRecentNodes(nodes);
        RefreshIncidents(nodes, tunnels, processes, deployments, dashboardLogs);
        RefreshLogs(dashboardLogs);

        StatusText = failureMessages.Count == 0
            ? "仪表盘数据已联动本地记录和当前 SSH 会话。"
            : string.Join(" ", failureMessages);
    }

    private void RefreshMetrics(
        IReadOnlyList<NodeProfile> nodes,
        IReadOnlyList<TunnelProfile> tunnels,
        IReadOnlyList<RuntimeProcess> processes)
    {
        var onlineNodes = nodes.Count(node =>
            _nodeConnectionSessionService.GetSessionStatus(node.Name).State == NodeConnectionSessionState.Online);
        var runningProcesses = CountKnownRunningFrpProcesses();
        var activeTunnels = tunnels.Count(tunnel =>
            tunnel.Status is FrpNexusStatus.Running or FrpNexusStatus.Online or FrpNexusStatus.Ready);

        SetMetrics(nodes.Count, onlineNodes, runningProcesses, activeTunnels);
    }

    private int CountKnownRunningFrpProcesses()
    {
        var localFrpcCount = _localFrpcProcessService
            .ListManagedSessions()
            .Count(session => session.Status == FrpNexusStatus.Running && session.IsManaged);

        var remoteFrpsCount = _frpLifecycleStateService
            .ListRemoteFrpsSnapshots()
            .Count(snapshot => snapshot.IsSshOnline && snapshot.FrpsStatus == FrpNexusStatus.Running);

        return localFrpcCount + remoteFrpsCount;
    }

    private void SetMetrics(int nodeCount, int onlineNodeCount, int runningProcessCount, int activeTunnelCount)
    {
        Metrics.Clear();
        Metrics.Add(new("节点总数", nodeCount.ToString("N0"), "dns", FrpNexusStatus.Ready));
        Metrics.Add(new("在线节点", onlineNodeCount.ToString("N0"), "check_circle", FrpNexusStatus.Online));
        Metrics.Add(new("FRP 进程", runningProcessCount.ToString("N0"), "memory", FrpNexusStatus.Running));
        Metrics.Add(new("活跃隧道", activeTunnelCount.ToString("N0"), "rebase_edit", FrpNexusStatus.Ready));
    }

    private void RefreshRecentNodes(IReadOnlyList<NodeProfile> nodes)
    {
        var rows = nodes
            .OrderByDescending(node => node.LastConnectionTestedAt.HasValue)
            .ThenByDescending(node => node.LastConnectionTestedAt)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .Select(node =>
            {
                var session = _nodeConnectionSessionService.GetSessionStatus(node.Name);
                return new DashboardNodeRowViewModel(node, session);
            })
            .ToArray();

        RecentNodes.Clear();
        foreach (var row in rows)
        {
            RecentNodes.Add(row);
        }

        HasRecentNodes = RecentNodes.Count > 0;
    }

    private void RefreshIncidents(
        IReadOnlyList<NodeProfile> nodes,
        IReadOnlyList<TunnelProfile> tunnels,
        IReadOnlyList<RuntimeProcess> processes,
        IReadOnlyList<DeploymentRecord> deployments,
        IReadOnlyList<LogEntry> dashboardLogs)
    {
        var incidents = new List<(bool HasTimestamp, DateTimeOffset UpdatedAt, int SourceOrder, IncidentViewModel Incident)>();
        var sourceOrder = 0;

        void AddIncident(
            bool hasTimestamp,
            DateTimeOffset updatedAt,
            IncidentViewModel incident)
        {
            incidents.Add((hasTimestamp, updatedAt, sourceOrder++, incident));
        }

        foreach (var record in deployments.Where(record => IsIncidentStatus(record.Status)))
        {
            AddIncident(
                hasTimestamp: true,
                record.UpdatedAt,
                new IncidentViewModel(
                    ToStatusTitle(record.Status),
                    FormatRelativeTime(record.UpdatedAt),
                    $"{record.NodeName}：{record.StepName}，{record.Description}",
                    record.Status));
        }

        foreach (var process in processes.Where(process => IsIncidentStatus(process.Status)))
        {
            AddIncident(
                hasTimestamp: false,
                DateTimeOffset.MinValue,
                new IncidentViewModel(
                    ToStatusTitle(process.Status),
                    "本地记录",
                    $"{process.NodeName}：{process.Name} {ProcessStatusText(process)}",
                    process.Status));
        }

        foreach (var tunnel in tunnels.Where(tunnel => IsIncidentStatus(tunnel.Status)))
        {
            AddIncident(
                hasTimestamp: false,
                DateTimeOffset.MinValue,
                new IncidentViewModel(
                    ToStatusTitle(tunnel.Status),
                    "本地记录",
                    $"{tunnel.NodeName}：隧道 {tunnel.Name} {ToTunnelStatusText(tunnel.Status)}",
                    tunnel.Status));
        }

        foreach (var node in nodes.Where(node => IsIncidentStatus(node.FrpStatus)))
        {
            AddIncident(
                node.LastConnectionTestedAt.HasValue,
                node.LastConnectionTestedAt ?? DateTimeOffset.MinValue,
                new IncidentViewModel(
                    ToStatusTitle(node.FrpStatus),
                    node.LastConnectionTestedAt.HasValue ? FormatRelativeTime(node.LastConnectionTestedAt.Value) : "本地记录",
                    $"{node.Name}：FRP 状态 {ToFrpStatusText(node.FrpStatus)}",
                    node.FrpStatus));
        }

        foreach (var log in dashboardLogs.Where(IsIncidentLog))
        {
            var hasTimestamp = LogTimestampParser.TryParseTimestamp(log.Timestamp, out var parsedAt);
            AddIncident(
                hasTimestamp,
                hasTimestamp ? parsedAt : DateTimeOffset.MinValue,
                new IncidentViewModel(
                    ToStatusTitle(log.Status),
                    FormatLogIncidentTime(log.Timestamp),
                    $"{log.NodeName} - {log.ProcessName}: {log.Message}",
                    log.Status));
        }

        var topIncidents = incidents
            .OrderByDescending(item => item.HasTimestamp)
            .ThenByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.SourceOrder)
            .Take(DashboardIncidentDisplayLimit)
            .Select(item => item.Incident)
            .ToArray();

        Incidents.Clear();
        foreach (var incident in topIncidents)
        {
            Incidents.Add(incident);
        }

        HasIncidents = Incidents.Count > 0;
    }

    private async Task<IReadOnlyList<LogEntry>> LoadDashboardLogsAsync(
        IReadOnlyList<NodeProfile> nodes,
        List<string> failureMessages,
        CancellationToken cancellationToken)
    {
        var logs = new List<LogEntry>();

        try
        {
            logs.AddRange(await _localApplicationLogService.ReadRecentLogsAsync(
                DashboardLogLineCount,
                cancellationToken));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            failureMessages.Add(ViewModelErrorText.ForUser("本地日志读取", ex));
        }

        var nodesByName = nodes.ToDictionary(node => node.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var session in _nodeConnectionSessionService
            .ListActiveSessions()
            .Where(session => session.State == NodeConnectionSessionState.Online)
            .OrderBy(session => session.NodeName, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!nodesByName.TryGetValue(session.NodeName, out var node))
            {
                continue;
            }

            var credential = _nodeConnectionSessionService.GetConnectedCredential(node.Name);
            if (credential is null)
            {
                continue;
            }

            IReadOnlyList<RuntimeProcess> remoteProcesses;
            try
            {
                remoteProcesses = await _remoteRuntimeService.GetProcessesAsync(
                    new RemoteRuntimeQueryRequest(node, credential),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failureMessages.Add(ViewModelErrorText.ForUser($"{node.Name} 远程日志目标刷新", ex));
                continue;
            }

            foreach (var process in remoteProcesses
                .Where(IsRunningFrpProcess)
                .OrderBy(process => process.ProcessKind, StringComparer.OrdinalIgnoreCase)
                .ThenBy(process => process.ProcessId, StringComparer.OrdinalIgnoreCase)
                .Take(4))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    logs.AddRange(await _remoteLogService.ReadRecentLogsAsync(
                        new RemoteLogReadRequest(
                            node,
                            credential,
                            NormalizeProcessKind(process.ProcessKind),
                            ResolveRemoteLogPath(process.ProcessKind),
                            DashboardLogLineCount),
                        cancellationToken));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failureMessages.Add(ViewModelErrorText.ForUser($"{node.Name} 远程日志读取", ex));
                }
            }
        }

        return OrderDashboardLogs(logs).Take(DashboardLogDisplayLimit).ToArray();
    }

    private void RefreshLogs(IReadOnlyList<LogEntry> dashboardLogs)
    {
        Logs.Clear();
        foreach (var log in dashboardLogs)
        {
            Logs.Add(log);
        }

        HasLogs = Logs.Count > 0;
    }

    private void RefreshLogs(
        IReadOnlyList<TunnelProfile> tunnels,
        IReadOnlyList<RuntimeProcess> processes,
        IReadOnlyList<DeploymentRecord> deployments)
    {
        var logs = deployments
            .OrderByDescending(record => record.UpdatedAt)
            .Take(4)
            .Select(record => new LogEntry(
                record.UpdatedAt.ToLocalTime().ToString("HH:mm:ss"),
                ToLogLevel(record.Status),
                record.NodeName,
                "deploy",
                $"{record.StepName} - {record.Description}",
                record.Status))
            .Concat(processes
                .Take(3)
                .Select(process => new LogEntry(
                    "--:--:--",
                    ToLogLevel(process.Status),
                    process.NodeName,
                    process.ProcessKind,
                    $"{process.Name} {ProcessStatusText(process)}",
                    process.Status)))
            .Concat(tunnels
                .Take(2)
                .Select(tunnel => new LogEntry(
                    "--:--:--",
                    ToLogLevel(tunnel.Status),
                    tunnel.NodeName,
                    tunnel.Protocol.ToString().ToLowerInvariant(),
                    $"{tunnel.Name} -> {tunnel.RemoteEndpoint}，{ToTunnelStatusText(tunnel.Status)}",
                    tunnel.Status)))
            .Take(8)
            .ToArray();

        Logs.Clear();
        foreach (var log in logs)
        {
            Logs.Add(log);
        }

        HasLogs = Logs.Count > 0;
    }

    private static bool IsIncidentStatus(FrpNexusStatus status)
    {
        return status is FrpNexusStatus.Error or FrpNexusStatus.Warning;
    }

    private static bool IsIncidentLog(LogEntry log)
    {
        return IsIncidentStatus(log.Status)
            || string.Equals(log.Level, "WARN", StringComparison.OrdinalIgnoreCase)
            || string.Equals(log.Level, "WARNING", StringComparison.OrdinalIgnoreCase)
            || string.Equals(log.Level, "ERR", StringComparison.OrdinalIgnoreCase)
            || string.Equals(log.Level, "ERROR", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRunningFrpProcess(RuntimeProcess process)
    {
        return process.Status == FrpNexusStatus.Running
            && (string.Equals(process.ProcessKind, "frpc", StringComparison.OrdinalIgnoreCase)
                || string.Equals(process.ProcessKind, "frps", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeProcessKind(string processKind)
    {
        return string.Equals(processKind, "frps", StringComparison.OrdinalIgnoreCase)
            ? "frps"
            : "frpc";
    }

    private static string ResolveRemoteLogPath(string processKind)
    {
        return string.Equals(processKind, "frps", StringComparison.OrdinalIgnoreCase)
            ? FrpsLogPath
            : FrpcLogPath;
    }

    private static IEnumerable<LogEntry> OrderDashboardLogs(IReadOnlyList<LogEntry> logs)
    {
        return logs
            .Select((log, index) => new
            {
                Log = log,
                Index = index,
                HasTimestamp = LogTimestampParser.TryParseTimestamp(log.Timestamp, out var parsedAt),
                ParsedAt = parsedAt
            })
            .OrderByDescending(item => item.HasTimestamp)
            .ThenByDescending(item => item.ParsedAt)
            .ThenBy(item => item.Index)
            .Select(item => item.Log);
    }

    private static string FormatLogIncidentTime(string timestamp)
    {
        return LogTimestampParser.TryParseTimestamp(timestamp, out var parsedAt)
            ? FormatRelativeTime(parsedAt)
            : timestamp;
    }

    private static string ToStatusTitle(FrpNexusStatus status)
    {
        return status switch
        {
            FrpNexusStatus.Error => "异常",
            FrpNexusStatus.Warning => "警告",
            _ => "提示"
        };
    }

    private static string ToFrpStatusText(FrpNexusStatus status)
    {
        return status switch
        {
            FrpNexusStatus.Running => "运行中",
            FrpNexusStatus.Stopped => "已停止",
            FrpNexusStatus.Warning => "警告",
            FrpNexusStatus.Error => "异常",
            _ => "未知"
        };
    }

    private static string ToTunnelStatusText(FrpNexusStatus status)
    {
        return status switch
        {
            FrpNexusStatus.Running => "运行中",
            FrpNexusStatus.Stopped => "已停止",
            FrpNexusStatus.Warning => "警告",
            FrpNexusStatus.Error => "异常",
            _ => "未刷新"
        };
    }

    private static string ProcessStatusText(RuntimeProcess process)
    {
        return process.Status switch
        {
            FrpNexusStatus.Running => $"运行中，PID {process.ProcessId}",
            FrpNexusStatus.Stopped => "已停止",
            FrpNexusStatus.Warning => HasRuntimeDetail(process.Uptime) ? $"警告：{process.Uptime}" : "警告",
            FrpNexusStatus.Error => HasRuntimeDetail(process.Uptime) ? $"异常：{process.Uptime}" : "异常",
            _ => "状态未知"
        };
    }

    private static bool HasRuntimeDetail(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Trim() != "-";
    }

    private static string ToLogLevel(FrpNexusStatus status)
    {
        return status switch
        {
            FrpNexusStatus.Error => "ERR",
            FrpNexusStatus.Warning => "WARN",
            FrpNexusStatus.Running or FrpNexusStatus.Online or FrpNexusStatus.Ready => "INFO",
            _ => "INFO"
        };
    }

    private static string FormatRelativeTime(DateTimeOffset time)
    {
        var localTime = time.ToLocalTime();
        var now = DateTimeOffset.Now;

        if (localTime.Date == now.Date)
        {
            return localTime.ToString("HH:mm");
        }

        if (localTime.Date == now.Date.AddDays(-1))
        {
            return "昨天 " + localTime.ToString("HH:mm");
        }

        return localTime.ToString("MM-dd HH:mm");
    }
}

public sealed partial class DashboardNodeRowViewModel : ObservableObject
{
    public DashboardNodeRowViewModel(
        NodeProfile node,
        NodeConnectionSessionSnapshot session)
    {
        Name = node.Name;
        Host = node.Host;
        Uptime = ResolveUptime(node, session);
        ConnectionStatus = ToStatus(session.State);
    }

    public string Name { get; }

    public string Host { get; }

    public string Uptime { get; }

    public FrpNexusStatus ConnectionStatus { get; }

    private static string ResolveUptime(NodeProfile node, NodeConnectionSessionSnapshot session)
    {
        if (session.State != NodeConnectionSessionState.Online)
        {
            return "未运行";
        }

        return node.FrpStatus == FrpNexusStatus.Running && !string.IsNullOrWhiteSpace(node.Uptime) && node.Uptime != "-"
            ? node.Uptime
            : "未刷新";
    }

    private static FrpNexusStatus ToStatus(NodeConnectionSessionState state)
    {
        return state switch
        {
            NodeConnectionSessionState.Online => FrpNexusStatus.Online,
            NodeConnectionSessionState.Connecting => FrpNexusStatus.Pending,
            NodeConnectionSessionState.Error => FrpNexusStatus.Error,
            _ => FrpNexusStatus.Offline
        };
    }
}
