using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed partial class DashboardPageViewModel : PageViewModel
{
    private readonly INodeManagementService _nodeManagementService;
    private readonly ITunnelManagementService _tunnelManagementService;
    private readonly IRuntimeRecordService _runtimeRecordService;
    private readonly IDeploymentRecordService _deploymentRecordService;
    private readonly INodeConnectionSessionService _nodeConnectionSessionService;
    private readonly INavigationRequestService _navigationRequestService;

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
        INavigationRequestService navigationRequestService)
        : base("仪表盘概览", "查看节点、隧道、运行状态和近期告警")
    {
        _nodeManagementService = nodeManagementService;
        _tunnelManagementService = tunnelManagementService;
        _runtimeRecordService = runtimeRecordService;
        _deploymentRecordService = deploymentRecordService;
        _nodeConnectionSessionService = nodeConnectionSessionService;
        _navigationRequestService = navigationRequestService;

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

        RefreshMetrics(nodes, tunnels, processes);
        RefreshRecentNodes(nodes, processes);
        RefreshIncidents(nodes, tunnels, processes, deployments);
        RefreshLogs(tunnels, processes, deployments);

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
        var runningProcesses = processes.Count(process => process.Status == FrpNexusStatus.Running);
        var activeTunnels = tunnels.Count(tunnel =>
            tunnel.Status is FrpNexusStatus.Running or FrpNexusStatus.Online or FrpNexusStatus.Ready);

        SetMetrics(nodes.Count, onlineNodes, runningProcesses, activeTunnels);
    }

    private void SetMetrics(int nodeCount, int onlineNodeCount, int runningProcessCount, int activeTunnelCount)
    {
        Metrics.Clear();
        Metrics.Add(new("节点总数", nodeCount.ToString("N0"), "dns", FrpNexusStatus.Ready));
        Metrics.Add(new("在线节点", onlineNodeCount.ToString("N0"), "check_circle", FrpNexusStatus.Online));
        Metrics.Add(new("FRP 进程", runningProcessCount.ToString("N0"), "memory", FrpNexusStatus.Running));
        Metrics.Add(new("活跃隧道", activeTunnelCount.ToString("N0"), "rebase_edit", FrpNexusStatus.Ready));
    }

    private void RefreshRecentNodes(
        IReadOnlyList<NodeProfile> nodes,
        IReadOnlyList<RuntimeProcess> processes)
    {
        var processByNode = processes
            .Where(process => process.Status == FrpNexusStatus.Running)
            .GroupBy(process => process.NodeName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var rows = nodes
            .OrderByDescending(node => node.LastConnectionTestedAt.HasValue)
            .ThenByDescending(node => node.LastConnectionTestedAt)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .Select(node =>
            {
                var session = _nodeConnectionSessionService.GetSessionStatus(node.Name);
                processByNode.TryGetValue(node.Name, out var process);
                return new DashboardNodeRowViewModel(node, session, process);
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
        IReadOnlyList<DeploymentRecord> deployments)
    {
        var incidents = new List<(DateTimeOffset UpdatedAt, IncidentViewModel Incident)>();

        incidents.AddRange(deployments
            .Where(record => IsIncidentStatus(record.Status))
            .Select(record => (
                record.UpdatedAt,
                new IncidentViewModel(
                    ToStatusTitle(record.Status),
                    FormatRelativeTime(record.UpdatedAt),
                    $"{record.NodeName}：{record.StepName}，{record.Description}",
                    record.Status))));

        incidents.AddRange(processes
            .Where(process => IsIncidentStatus(process.Status))
            .Select(process => (
                DateTimeOffset.MinValue,
                new IncidentViewModel(
                    ToStatusTitle(process.Status),
                    "本地记录",
                    $"{process.NodeName}：{process.Name} {ProcessStatusText(process)}",
                    process.Status))));

        incidents.AddRange(tunnels
            .Where(tunnel => IsIncidentStatus(tunnel.Status))
            .Select(tunnel => (
                DateTimeOffset.MinValue,
                new IncidentViewModel(
                    ToStatusTitle(tunnel.Status),
                    "本地记录",
                    $"{tunnel.NodeName}：隧道 {tunnel.Name} {ToTunnelStatusText(tunnel.Status)}",
                    tunnel.Status))));

        incidents.AddRange(nodes
            .Where(node => IsIncidentStatus(node.FrpStatus))
            .Select(node => (
                node.LastConnectionTestedAt ?? DateTimeOffset.MinValue,
                new IncidentViewModel(
                    ToStatusTitle(node.FrpStatus),
                    node.LastConnectionTestedAt.HasValue ? FormatRelativeTime(node.LastConnectionTestedAt.Value) : "本地记录",
                    $"{node.Name}：FRP 状态 {ToFrpStatusText(node.FrpStatus)}",
                    node.FrpStatus))));

        var topIncidents = incidents
            .OrderByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.Incident.Message, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .Select(item => item.Incident)
            .ToArray();

        Incidents.Clear();
        foreach (var incident in topIncidents)
        {
            Incidents.Add(incident);
        }

        HasIncidents = Incidents.Count > 0;
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
        NodeConnectionSessionSnapshot session,
        RuntimeProcess? runningProcess)
    {
        Name = node.Name;
        Host = node.Host;
        Uptime = ResolveUptime(node, runningProcess);
        ConnectionStatus = ToStatus(session.State);
    }

    public string Name { get; }

    public string Host { get; }

    public string Uptime { get; }

    public FrpNexusStatus ConnectionStatus { get; }

    private static string ResolveUptime(NodeProfile node, RuntimeProcess? runningProcess)
    {
        if (runningProcess is not null && !string.IsNullOrWhiteSpace(runningProcess.Uptime) && runningProcess.Uptime != "-")
        {
            return runningProcess.Uptime;
        }

        return node.FrpStatus == FrpNexusStatus.Running && !string.IsNullOrWhiteSpace(node.Uptime) && node.Uptime != "-"
            ? node.Uptime
            : "未运行";
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
