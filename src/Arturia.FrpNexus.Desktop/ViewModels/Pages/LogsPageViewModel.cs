using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed partial class LogsPageViewModel : PageViewModel
{
    private const string AllNodesFilter = "全部节点";
    private const string AllProcessesFilter = "全部进程";
    private const string AllLevelsFilter = "全部级别";

    private readonly INodeManagementService _nodeManagementService;
    private readonly IRemoteLogService _remoteLogService;

    private bool _hasAttemptedNodeFilterLoad;

    [ObservableProperty]
    private string _selectedNodeName = "Web-Server-HK";

    [ObservableProperty]
    private string _processName = "frpc";

    [ObservableProperty]
    private string _remoteLogPath = "/tmp/frpnexus-frpc.log";

    [ObservableProperty]
    private string _selectedSshAuthenticationMode = "SessionPassword";

    [ObservableProperty]
    private string _sshPrivateKeyPath = string.Empty;

    [ObservableProperty]
    private string _sshSessionPassword = string.Empty;

    [ObservableProperty]
    private string _sshPrivateKeyPassphrase = string.Empty;

    [ObservableProperty]
    private string _statusText = "当前显示静态日志样例。";

    [ObservableProperty]
    private bool _isReadingRemoteLogs;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedNodeFilter = AllNodesFilter;

    [ObservableProperty]
    private string _selectedProcessFilter = AllProcessesFilter;

    [ObservableProperty]
    private string _selectedLevelFilter = AllLevelsFilter;

    [ObservableProperty]
    private bool _isAutoRefreshEnabled = true;

    [ObservableProperty]
    private bool _isRemoteCredentialsVisible;

    public LogsPageViewModel(INodeManagementService nodeManagementService, IRemoteLogService remoteLogService)
        : base("日志", "筛选、搜索并查看远程 FRP 日志输出")
    {
        _nodeManagementService = nodeManagementService;
        _remoteLogService = remoteLogService;
        NodeFilterOptions = [AllNodesFilter];
        ProcessFilterOptions = [AllProcessesFilter, "frpc", "frps", "nexus_daemon"];
        LevelFilterOptions = [AllLevelsFilter, "INFO", "WARN", "ERROR", "DEBUG"];
        VisibleLogs = [];
        Logs =
        [
            new("2026-06-04 14:32:01.102", "INFO", "Web-Server-HK", "frpc", "FrpNexus client daemon started successfully. Version: V2.4.0", FrpNexusStatus.Ready),
            new("2026-06-04 14:32:01.150", "INFO", "Web-Server-HK", "frpc", "Reading configuration from /etc/frp/frpc.toml", FrpNexusStatus.Ready),
            new("2026-06-04 14:32:02.045", "INFO", "Web-Server-HK", "frpc", "Connection to control server established.", FrpNexusStatus.Online),
            new("2026-06-04 14:35:12.880", "WARN", "DB-Node-SH", "frpc", "Proxy [db_backup_sync] connection timeout. Retrying in 5 seconds...", FrpNexusStatus.Warning),
            new("2026-06-04 14:35:17.882", "WARN", "DB-Node-SH", "frpc", "Proxy [db_backup_sync] connection timeout. Retrying in 10 seconds...", FrpNexusStatus.Warning),
            new("2026-06-04 14:35:28.105", "ERROR", "DB-Node-SH", "frpc", "Failed to establish proxy [db_backup_sync]. Reason: remote server closed connection unexpectedly. EOF.", FrpNexusStatus.Error),
            new("2026-06-04 14:36:00.001", "INFO", "Web-Server-HK", "frpc", "Heartbeat sent to control server. Latency: 42ms.", FrpNexusStatus.Ready)
        ];
        RefreshFilterOptionsFromLogs();
        ApplyFilters();
    }

    public ObservableCollection<LogEntry> Logs { get; }

    public ObservableCollection<LogEntry> VisibleLogs { get; }

    public ObservableCollection<string> NodeFilterOptions { get; }

    public ObservableCollection<string> ProcessFilterOptions { get; }

    public ObservableCollection<string> LevelFilterOptions { get; }

    public string TerminalConnectionText =>
        SelectedNodeFilter == AllNodesFilter
            ? "[Connected: 全部节点]"
            : $"[Connected: {SelectedNodeFilter}]";

    public string LinesText => $"Lines: {VisibleLogs.Count:N0}";

    [RelayCommand]
    private async Task ReadRemoteLogsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureNodeFilterOptionsLoadedAsync(cancellationToken);

        var request = await TryCreateRequestAsync(cancellationToken);
        if (request is null)
        {
            return;
        }

        IsReadingRemoteLogs = true;
        StatusText = $"正在读取 {request.Node.Name} 的远程日志...";

        try
        {
            var logs = await _remoteLogService.ReadRecentLogsAsync(request, cancellationToken);
            Logs.Clear();
            foreach (var log in logs)
            {
                Logs.Add(log);
            }

            RefreshFilterOptionsFromLogs();
            ApplyFilters();
            StatusText = $"已读取 {Logs.Count} 行远程日志。";
            IsRemoteCredentialsVisible = false;
        }
        catch (OperationCanceledException)
        {
            StatusText = "远程日志读取已取消。";
        }
        catch (Exception ex)
        {
            StatusText = ViewModelErrorText.ForUser("远程日志读取", ex);
        }
        finally
        {
            ClearSessionSecrets();
            IsReadingRemoteLogs = false;
        }
    }

    [RelayCommand]
    private void ClearLogs()
    {
        Logs.Clear();
        ApplyFilters();
        StatusText = "日志面板已清空。";
    }

    [RelayCommand]
    private void ToggleRemoteCredentials()
    {
        IsRemoteCredentialsVisible = !IsRemoteCredentialsVisible;
    }

    private async Task EnsureNodeFilterOptionsLoadedAsync(CancellationToken cancellationToken)
    {
        if (_hasAttemptedNodeFilterLoad)
        {
            return;
        }

        _hasAttemptedNodeFilterLoad = true;

        try
        {
            var nodes = await _nodeManagementService.ListNodesAsync(cancellationToken);
            var names = nodes
                .Select(node => node.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            ReplaceFilterOptions(NodeFilterOptions, AllNodesFilter, names);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            RefreshNodeFilterOptionsFromLogs();
        }
    }

    private async Task<RemoteLogReadRequest?> TryCreateRequestAsync(CancellationToken cancellationToken)
    {
        if (SelectedNodeFilter != AllNodesFilter)
        {
            SelectedNodeName = SelectedNodeFilter;
        }

        NodeProfile? node;
        try
        {
            node = await _nodeManagementService.GetNodeAsync(SelectedNodeName, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            StatusText = "节点资料读取已取消。";
            return null;
        }
        catch (Exception ex)
        {
            StatusText = ViewModelErrorText.ForUser("节点资料读取", ex);
            return null;
        }

        if (node is null)
        {
            StatusText = $"未找到节点 {SelectedNodeName}，请先在节点页保存节点资料。";
            return null;
        }

        if (!TryParseAuthenticationMode(SelectedSshAuthenticationMode, out var mode))
        {
            StatusText = "请选择有效的 SSH 认证方式。";
            return null;
        }

        if (mode == SshAuthenticationMode.SessionPassword && string.IsNullOrWhiteSpace(SshSessionPassword))
        {
            StatusText = "请输入本次会话使用的 SSH 密码，密码不会保存到 SQLite。";
            return null;
        }

        if (mode == SshAuthenticationMode.PrivateKey && string.IsNullOrWhiteSpace(SshPrivateKeyPath))
        {
            StatusText = "请输入私钥文件路径，私钥内容和 passphrase 不会保存到 SQLite。";
            return null;
        }

        var credential = new SshCredentialReference(
            mode,
            string.IsNullOrWhiteSpace(SshPrivateKeyPath) ? null : SshPrivateKeyPath.Trim(),
            string.IsNullOrWhiteSpace(SshSessionPassword) ? null : SshSessionPassword,
            string.IsNullOrWhiteSpace(SshPrivateKeyPassphrase) ? null : SshPrivateKeyPassphrase);

        return new RemoteLogReadRequest(
            node,
            credential,
            string.IsNullOrWhiteSpace(ProcessName) ? "frpc" : ProcessName.Trim(),
            string.IsNullOrWhiteSpace(RemoteLogPath) ? "/tmp/frpnexus-frpc.log" : RemoteLogPath.Trim());
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedNodeFilterChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && value != AllNodesFilter)
        {
            SelectedNodeName = value;
        }

        ApplyFilters();
        OnPropertyChanged(nameof(TerminalConnectionText));
    }

    partial void OnSelectedProcessFilterChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && value != AllProcessesFilter)
        {
            ProcessName = value;
        }

        ApplyFilters();
    }

    partial void OnSelectedLevelFilterChanged(string value)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filteredLogs = Logs.Where(IsVisibleLog).ToArray();

        VisibleLogs.Clear();
        foreach (var log in filteredLogs)
        {
            VisibleLogs.Add(log);
        }

        OnPropertyChanged(nameof(LinesText));
    }

    private bool IsVisibleLog(LogEntry log)
    {
        if (SelectedNodeFilter != AllNodesFilter
            && !string.Equals(log.NodeName, SelectedNodeFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (SelectedProcessFilter != AllProcessesFilter
            && !string.Equals(log.ProcessName, SelectedProcessFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (SelectedLevelFilter != AllLevelsFilter
            && !string.Equals(log.Level, SelectedLevelFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        var query = SearchText.Trim();
        return Contains(log.Timestamp, query)
            || Contains(log.Level, query)
            || Contains(log.NodeName, query)
            || Contains(log.ProcessName, query)
            || Contains(log.Message, query);
    }

    private void RefreshFilterOptionsFromLogs()
    {
        RefreshNodeFilterOptionsFromLogs();
        ReplaceFilterOptions(
            ProcessFilterOptions,
            AllProcessesFilter,
            Logs.Select(log => log.ProcessName)
                .Concat(["frpc", "frps", "nexus_daemon"])
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private void RefreshNodeFilterOptionsFromLogs()
    {
        ReplaceFilterOptions(
            NodeFilterOptions,
            AllNodesFilter,
            Logs.Select(log => log.NodeName).Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static void ReplaceFilterOptions(ObservableCollection<string> options, string allOption, IEnumerable<string> values)
    {
        var selectedValues = values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        options.Clear();
        options.Add(allOption);
        foreach (var value in selectedValues)
        {
            options.Add(value);
        }
    }

    private static bool Contains(string value, string query)
    {
        return value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseAuthenticationMode(string value, out SshAuthenticationMode mode)
    {
        if (string.Equals(value, "PrivateKey", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "私钥文件", StringComparison.OrdinalIgnoreCase))
        {
            mode = SshAuthenticationMode.PrivateKey;
            return true;
        }

        if (string.Equals(value, "SshAgent", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "SSH Agent", StringComparison.OrdinalIgnoreCase))
        {
            mode = SshAuthenticationMode.SshAgent;
            return true;
        }

        if (string.Equals(value, "SessionPassword", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "会话密码", StringComparison.OrdinalIgnoreCase))
        {
            mode = SshAuthenticationMode.SessionPassword;
            return true;
        }

        mode = SshAuthenticationMode.SessionPassword;
        return false;
    }

    private void ClearSessionSecrets()
    {
        SshSessionPassword = string.Empty;
        SshPrivateKeyPassphrase = string.Empty;
    }
}
