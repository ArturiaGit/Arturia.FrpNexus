using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.Services;
using Arturia.FrpNexus.Desktop.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed partial class LogsPageViewModel : PageViewModel, IActivatablePageViewModel, IDisposable
{
    private const string AllNodesFilter = "全部节点";
    private const string AllProcessesFilter = "全部进程";
    private const string AllLevelsFilter = "全部级别";
    private const string LocalNodeFilter = "客户端";
    private const string LocalProcessFilter = "FrpNexus";
    private const string FrpcLogPath = "/tmp/frpnexus-frpc.log";
    private const string FrpsLogPath = "/tmp/frpnexus-frps.log";
    private static readonly TimeSpan DefaultAutoRefreshInterval = TimeSpan.FromSeconds(2);

    private readonly INodeManagementService _nodeManagementService;
    private readonly IRemoteLogService _remoteLogService;
    private readonly ILocalApplicationLogService _localApplicationLogService;
    private readonly INodeConnectionSessionService _nodeConnectionSessionService;
    private readonly IRemoteRuntimeService _remoteRuntimeService;
    private readonly TimeSpan _autoRefreshInterval;

    private bool _isShowingLocalLogs = true;
    private bool _isUpdatingRemoteFilters;
    private bool _isAutoReadingRemoteLogs;
    private bool _isPageActive;
    private bool _isDisposed;
    private int _isAutoRefreshTickRunning;
    private CancellationTokenSource? _autoRefreshCancellation;
    private Task? _autoRefreshTask;
    private RemoteProcessLogOption? _selectedRemoteProcessOption;
    private IReadOnlyList<RemoteProcessLogOption> _remoteProcessOptions = [];

    [ObservableProperty]
    private string _selectedNodeName = string.Empty;

    [ObservableProperty]
    private string _processName = LocalProcessFilter;

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
    private string _statusText = "点击刷新读取本地 FrpNexus 日志。";

    [ObservableProperty]
    private bool _isReadingRemoteLogs;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedNodeFilter = LocalNodeFilter;

    [ObservableProperty]
    private string _selectedProcessFilter = LocalProcessFilter;

    [ObservableProperty]
    private string _selectedLevelFilter = AllLevelsFilter;

    [ObservableProperty]
    private bool _isAutoRefreshEnabled = true;

    [ObservableProperty]
    private bool _isRemoteCredentialsVisible;

    [ObservableProperty]
    private bool _canReadRemoteLogs;

    public LogsPageViewModel(
        INodeManagementService nodeManagementService,
        IRemoteLogService remoteLogService,
        ILocalApplicationLogService localApplicationLogService,
        INodeConnectionSessionService nodeConnectionSessionService,
        IRemoteRuntimeService remoteRuntimeService)
        : this(
            nodeManagementService,
            remoteLogService,
            localApplicationLogService,
            nodeConnectionSessionService,
            remoteRuntimeService,
            null)
    {
    }

    public LogsPageViewModel(
        INodeManagementService nodeManagementService,
        IRemoteLogService remoteLogService,
        ILocalApplicationLogService localApplicationLogService,
        INodeConnectionSessionService nodeConnectionSessionService,
        IRemoteRuntimeService remoteRuntimeService,
        TimeSpan? autoRefreshInterval)
        : base("日志", "筛选、搜索并查看 FRP 与 FrpNexus 日志输出")
    {
        _nodeManagementService = nodeManagementService;
        _remoteLogService = remoteLogService;
        _localApplicationLogService = localApplicationLogService;
        _nodeConnectionSessionService = nodeConnectionSessionService;
        _remoteRuntimeService = remoteRuntimeService;
        _autoRefreshInterval = autoRefreshInterval.GetValueOrDefault(DefaultAutoRefreshInterval);
        NodeFilterOptions = [AllNodesFilter, LocalNodeFilter];
        ProcessFilterOptions = [AllProcessesFilter, LocalProcessFilter];
        LevelFilterOptions = [AllLevelsFilter, "INFO", "WARN", "ERROR", "DEBUG"];
        VisibleLogs = [];
        Logs = [];
        ApplyFilters();
    }

    public ObservableCollection<LogEntry> Logs { get; }

    public ObservableCollection<LogEntry> VisibleLogs { get; }

    public ObservableCollection<string> NodeFilterOptions { get; }

    public ObservableCollection<string> ProcessFilterOptions { get; }

    public ObservableCollection<string> LevelFilterOptions { get; }

    public string TerminalConnectionText =>
        IsAllOrInvalidFilter(SelectedNodeFilter, AllNodesFilter, NodeFilterOptions)
            ? "[Connected: 全部节点]"
            : $"[Connected: {SelectedNodeFilter}]";

    public string LinesText => $"Lines: {VisibleLogs.Count:N0}";

    public string LogFileText =>
        _isShowingLocalLogs
            ? $"File: {_localApplicationLogService.CurrentLogDirectory}"
            : $"File: {RemoteLogPath}";

    public string LogModeToggleText => IsRemoteLogMode ? "本地日志" : "远程日志";

    public string LogModeToggleTooltip =>
        IsRemoteLogMode ? "返回并读取本地 FrpNexus 日志" : "读取远程 FRP 日志";

    public Task RefreshForNavigationAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAutoRefreshEnabled)
        {
            return Task.CompletedTask;
        }

        return IsRemoteLogMode
            ? ReadRemoteLogsCoreAsync(clearLogsOnFailure: false, cancellationToken)
            : LoadLocalLogsAsync(cancellationToken);
    }

    public async Task OnActivatedAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            return;
        }

        _isPageActive = true;
        if (!IsAutoRefreshEnabled)
        {
            return;
        }

        await RefreshForNavigationIfIdleAsync(cancellationToken);
        StartAutoRefreshLoop();
    }

    public void OnDeactivated()
    {
        _isPageActive = false;
        StopAutoRefreshLoop();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        OnDeactivated();
    }

    partial void OnIsAutoRefreshEnabledChanged(bool value)
    {
        if (_isDisposed || !_isPageActive)
        {
            return;
        }

        if (!value)
        {
            StopAutoRefreshLoop();
            return;
        }

        _ = RefreshForNavigationAndStartLoopAsync();
    }

    private async Task RefreshForNavigationAndStartLoopAsync()
    {
        try
        {
            await RefreshForNavigationIfIdleAsync(CancellationToken.None);
            StartAutoRefreshLoop();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void StartAutoRefreshLoop()
    {
        if (_isDisposed
            || !_isPageActive
            || !IsAutoRefreshEnabled
            || _autoRefreshTask is { IsCompleted: false })
        {
            return;
        }

        _autoRefreshCancellation?.Dispose();
        _autoRefreshCancellation = new CancellationTokenSource();
        _autoRefreshTask = RunAutoRefreshLoopAsync(_autoRefreshCancellation.Token);
    }

    private void StopAutoRefreshLoop()
    {
        var cancellation = _autoRefreshCancellation;
        _autoRefreshCancellation = null;
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        cancellation.Dispose();
        _autoRefreshTask = null;
    }

    private async Task RunAutoRefreshLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(_autoRefreshInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (_isDisposed || !_isPageActive || !IsAutoRefreshEnabled)
                {
                    continue;
                }

                await RefreshForNavigationIfIdleAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task RefreshForNavigationIfIdleAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _isAutoRefreshTickRunning, 1) == 1)
        {
            return;
        }

        try
        {
            await RefreshForNavigationAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            Interlocked.Exchange(ref _isAutoRefreshTickRunning, 0);
        }
    }

    [RelayCommand]
    public async Task LoadLocalLogsAsync(CancellationToken cancellationToken = default)
    {
        IsReadingRemoteLogs = true;
        StatusText = "正在读取本地 FrpNexus 日志...";

        try
        {
            var logs = await _localApplicationLogService.ReadRecentLogsAsync(200, cancellationToken);
            _isShowingLocalLogs = true;
            NotifyLogModeToggleChanged();
            ReplaceLogs(logs);
            IsRemoteCredentialsVisible = false;
            NotifyLogModeToggleChanged();
            StatusText = Logs.Count == 0
                ? "本地日志暂无警告或错误。"
                : $"已读取 {Logs.Count} 行本地日志。";
        }
        catch (OperationCanceledException)
        {
            StatusText = "本地日志读取已取消。";
        }
        catch (Exception ex)
        {
            StatusText = ViewModelErrorText.ForUser("本地日志读取", ex);
        }
        finally
        {
            IsReadingRemoteLogs = false;
        }
    }

    [RelayCommand]
    private async Task ReadRemoteLogsAsync(CancellationToken cancellationToken = default)
    {
        await ReadRemoteLogsCoreAsync(clearLogsOnFailure: false, cancellationToken);
    }

    [RelayCommand]
    private async Task RefreshLogsAsync(CancellationToken cancellationToken = default)
    {
        if (IsRemoteLogMode)
        {
            await ReadRemoteLogsCoreAsync(clearLogsOnFailure: false, cancellationToken);
            return;
        }

        await LoadLocalLogsAsync(cancellationToken);
    }

    private async Task ReadRemoteLogsCoreAsync(bool clearLogsOnFailure, CancellationToken cancellationToken)
    {
        if (IsRemoteCredentialsVisible && _remoteProcessOptions.Count == 0)
        {
            await LoadRemoteLogTargetsAsync(cancellationToken);
            return;
        }

        var request = await TryCreateRequestAsync(cancellationToken);
        if (request is null)
        {
            return;
        }

        var isRemoteModeRequest = IsRemoteLogMode;
        IsReadingRemoteLogs = true;
        StatusText = $"正在读取 {request.Node.Name} 的远程日志...";

        try
        {
            var logs = await _remoteLogService.ReadRecentLogsAsync(request, cancellationToken);
            _isShowingLocalLogs = false;
            NotifyLogModeToggleChanged();
            ReplaceLogs(logs);
            StatusText = $"已读取 {Logs.Count} 行远程日志。";
            if (!isRemoteModeRequest)
            {
                IsRemoteCredentialsVisible = false;
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "远程日志读取已取消。";
        }
        catch (Exception ex)
        {
            if (clearLogsOnFailure)
            {
                Logs.Clear();
                ApplyFilters();
                OnPropertyChanged(nameof(LogFileText));
            }

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
    private async Task ToggleRemoteCredentialsAsync(CancellationToken cancellationToken = default)
    {
        if (IsRemoteLogMode)
        {
            await SwitchToLocalLogModeAsync(cancellationToken);
            return;
        }

        IsRemoteCredentialsVisible = true;
        NotifyLogModeToggleChanged();
        await LoadRemoteLogTargetsAsync(cancellationToken);
    }

    private async Task SwitchToLocalLogModeAsync(CancellationToken cancellationToken)
    {
        _isShowingLocalLogs = true;
        IsRemoteCredentialsVisible = false;
        NotifyLogModeToggleChanged();
        CanReadRemoteLogs = false;
        _remoteProcessOptions = [];
        _selectedRemoteProcessOption = null;
        await LoadLocalLogsAsync(cancellationToken);
    }

    private async Task LoadRemoteLogTargetsAsync(CancellationToken cancellationToken)
    {
        var activeSessions = _nodeConnectionSessionService
            .ListActiveSessions()
            .Where(session => session.State == NodeConnectionSessionState.Online)
            .Select(session => session.NodeName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _isShowingLocalLogs = false;
        NotifyLogModeToggleChanged();
        _isUpdatingRemoteFilters = true;
        SelectedNodeFilter = ReplaceFilterOptions(NodeFilterOptions, AllNodesFilter, activeSessions, SelectedNodeFilter);
        _isUpdatingRemoteFilters = false;
        if (activeSessions.Length == 0)
        {
            SelectedNodeName = string.Empty;
            ReplaceRemoteProcessOptions([]);
            StatusText = "请先在节点页连接一个节点。";
            return;
        }

        if (IsAllOrInvalidFilter(SelectedNodeFilter, AllNodesFilter, NodeFilterOptions))
        {
            _isUpdatingRemoteFilters = true;
            SelectedNodeFilter = activeSessions[0];
            _isUpdatingRemoteFilters = false;
        }

        SelectedNodeName = SelectedNodeFilter;
        await RefreshRemoteProcessesForSelectedNodeAsync(autoReadDefaultProcess: true, cancellationToken);
    }

    private async Task RefreshRemoteProcessesForSelectedNodeAsync(bool autoReadDefaultProcess, CancellationToken cancellationToken)
    {
        if (IsAllOrInvalidFilter(SelectedNodeFilter, AllNodesFilter, NodeFilterOptions))
        {
            ReplaceRemoteProcessOptions([]);
            return;
        }

        var node = await _nodeManagementService.GetNodeAsync(SelectedNodeFilter, cancellationToken);
        if (node is null)
        {
            ReplaceRemoteProcessOptions([]);
            StatusText = $"未找到节点 {SelectedNodeFilter}，请先在节点页保存节点资料。";
            return;
        }

        var credential = _nodeConnectionSessionService.GetConnectedCredential(node.Name);
        if (credential is null)
        {
            ReplaceRemoteProcessOptions([]);
            StatusText = "节点 SSH 会话已断开，请先重新连接节点。";
            return;
        }

        StatusText = $"正在刷新 {node.Name} 的 FRP 进程...";
        IReadOnlyList<RuntimeProcess> processes;
        try
        {
            processes = await _remoteRuntimeService.GetProcessesAsync(
                new RemoteRuntimeQueryRequest(node, credential),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            StatusText = "远程 FRP 进程刷新已取消。";
            return;
        }
        catch (Exception ex)
        {
            ReplaceRemoteProcessOptions([]);
            StatusText = ViewModelErrorText.ForUser("远程 FRP 进程刷新", ex);
            return;
        }

        var options = processes
            .Where(process => process.Status == FrpNexusStatus.Running)
            .Where(process => string.Equals(process.ProcessKind, "frpc", StringComparison.OrdinalIgnoreCase)
                || string.Equals(process.ProcessKind, "frps", StringComparison.OrdinalIgnoreCase))
            .Select(RemoteProcessLogOption.FromProcess)
            .OrderBy(option => option.ProcessKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.ProcessId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ReplaceRemoteProcessOptions(options);
        StatusText = options.Length == 0
            ? "未发现该节点正在运行的 frpc 或 frps 进程。"
            : $"已发现 {options.Length} 个远程 FRP 进程。";
        if (autoReadDefaultProcess && options.Length > 0)
        {
            await AutoReadSelectedRemoteLogsAsync(cancellationToken);
        }
    }

    private void ReplaceRemoteProcessOptions(IReadOnlyList<RemoteProcessLogOption> options)
    {
        _remoteProcessOptions = options;
        _selectedRemoteProcessOption = null;

        _isUpdatingRemoteFilters = true;
        ProcessFilterOptions.Clear();
        ProcessFilterOptions.Add(AllProcessesFilter);
        foreach (var option in options)
        {
            ProcessFilterOptions.Add(option.DisplayName);
        }

        SelectedProcessFilter = options.Count > 0 ? options[0].DisplayName : AllProcessesFilter;
        ApplySelectedRemoteProcess(SelectedProcessFilter);
        _isUpdatingRemoteFilters = false;
        CanReadRemoteLogs = options.Count > 0;
    }

    private async Task AutoReadSelectedRemoteLogsAsync(CancellationToken cancellationToken)
    {
        if (_isAutoReadingRemoteLogs || !CanReadRemoteLogs)
        {
            return;
        }

        _isAutoReadingRemoteLogs = true;
        try
        {
            await ReadRemoteLogsCoreAsync(clearLogsOnFailure: true, cancellationToken);
        }
        finally
        {
            _isAutoReadingRemoteLogs = false;
        }
    }

    private void ReplaceLogs(IReadOnlyList<LogEntry> logs)
    {
        Logs.Clear();
        foreach (var log in logs)
        {
            Logs.Add(log);
        }

        RefreshFilterOptionsFromLogs();
        ApplyFilters();
        OnPropertyChanged(nameof(LogFileText));
    }

    private async Task<RemoteLogReadRequest?> TryCreateRequestAsync(CancellationToken cancellationToken)
    {
        if (IsRemoteCredentialsVisible)
        {
            if (SelectedNodeFilter == AllNodesFilter || string.IsNullOrWhiteSpace(SelectedNodeFilter))
            {
                StatusText = "请先选择一个已连接节点。";
                return null;
            }

            _selectedRemoteProcessOption ??= _remoteProcessOptions.FirstOrDefault(
                option => string.Equals(option.DisplayName, SelectedProcessFilter, StringComparison.OrdinalIgnoreCase));
            if (_selectedRemoteProcessOption is null)
            {
                StatusText = "请先选择一个正在运行的 frpc 或 frps 进程。";
                return null;
            }

            var remoteNode = await _nodeManagementService.GetNodeAsync(SelectedNodeFilter, cancellationToken);
            if (remoteNode is null)
            {
                StatusText = $"未找到节点 {SelectedNodeFilter}，请先在节点页保存节点资料。";
                return null;
            }

            var connectedCredential = _nodeConnectionSessionService.GetConnectedCredential(remoteNode.Name);
            if (connectedCredential is null)
            {
                StatusText = "节点 SSH 会话已断开，请先重新连接节点。";
                return null;
            }

            return new RemoteLogReadRequest(
                remoteNode,
                connectedCredential,
                _selectedRemoteProcessOption.ProcessKind,
                _selectedRemoteProcessOption.LogPath);
        }

        if (SelectedNodeFilter != AllNodesFilter
            && SelectedNodeFilter != LocalNodeFilter)
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
            string.IsNullOrWhiteSpace(RemoteLogPath) ? FrpcLogPath : RemoteLogPath.Trim());
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedNodeFilterChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SelectedNodeFilter = AllNodesFilter;
            return;
        }

        if (!string.IsNullOrWhiteSpace(value) && value != AllNodesFilter)
        {
            SelectedNodeName = value;
        }

        ApplyFilters();
        OnPropertyChanged(nameof(TerminalConnectionText));

        if (IsRemoteCredentialsVisible && !_isUpdatingRemoteFilters)
        {
            _ = RefreshRemoteProcessesForSelectedNodeAsync(autoReadDefaultProcess: true, CancellationToken.None);
        }
    }

    partial void OnSelectedProcessFilterChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SelectedProcessFilter = AllProcessesFilter;
            return;
        }

        if (!string.IsNullOrWhiteSpace(value) && value != AllProcessesFilter)
        {
            ApplySelectedRemoteProcess(value);
        }

        ApplyFilters();
        OnPropertyChanged(nameof(LogFileText));
        if (IsRemoteCredentialsVisible
            && !_isUpdatingRemoteFilters
            && _selectedRemoteProcessOption is not null)
        {
            _ = AutoReadSelectedRemoteLogsAsync(CancellationToken.None);
        }
    }

    partial void OnRemoteLogPathChanged(string value)
    {
        OnPropertyChanged(nameof(LogFileText));
    }

    partial void OnSelectedLevelFilterChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SelectedLevelFilter = AllLevelsFilter;
            return;
        }

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
        if (!IsAllOrInvalidFilter(SelectedNodeFilter, AllNodesFilter, NodeFilterOptions)
            && !string.Equals(log.NodeName, SelectedNodeFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var selectedProcessName = _selectedRemoteProcessOption?.ProcessKind ?? SelectedProcessFilter;
        if (!IsAllOrInvalidFilter(SelectedProcessFilter, AllProcessesFilter, ProcessFilterOptions)
            && !string.Equals(log.ProcessName, selectedProcessName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IsAllOrInvalidFilter(SelectedLevelFilter, AllLevelsFilter, LevelFilterOptions)
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
        if (_isShowingLocalLogs)
        {
            RefreshLocalFilterOptionsFromLogs();
            return;
        }

        if (_remoteProcessOptions.Count > 0)
        {
            return;
        }

        RefreshNodeFilterOptionsFromLogs();
        SelectedProcessFilter = ReplaceFilterOptions(
            ProcessFilterOptions,
            AllProcessesFilter,
            Logs.Select(log => log.ProcessName).Where(value => !string.IsNullOrWhiteSpace(value)),
            SelectedProcessFilter);
    }

    private void RefreshNodeFilterOptionsFromLogs()
    {
        SelectedNodeFilter = ReplaceFilterOptions(
            NodeFilterOptions,
            AllNodesFilter,
            Logs.Select(log => log.NodeName).Where(value => !string.IsNullOrWhiteSpace(value)),
            SelectedNodeFilter);
    }

    private void RefreshLocalFilterOptionsFromLogs()
    {
        ReplaceFilterOptions(
            NodeFilterOptions,
            AllNodesFilter,
            Logs.Select(log => log.NodeName)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Append(LocalNodeFilter),
            LocalNodeFilter);
        SelectedNodeFilter = LocalNodeFilter;

        ReplaceFilterOptions(
            ProcessFilterOptions,
            AllProcessesFilter,
            Logs.Select(log => log.ProcessName)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Append(LocalProcessFilter),
            LocalProcessFilter);
        SelectedProcessFilter = LocalProcessFilter;
    }

    private bool IsRemoteLogMode => IsRemoteCredentialsVisible || !_isShowingLocalLogs;

    private void NotifyLogModeToggleChanged()
    {
        OnPropertyChanged(nameof(LogModeToggleText));
        OnPropertyChanged(nameof(LogModeToggleTooltip));
    }

    private void ApplySelectedRemoteProcess(string value)
    {
        _selectedRemoteProcessOption = _remoteProcessOptions.FirstOrDefault(
            option => string.Equals(option.DisplayName, value, StringComparison.OrdinalIgnoreCase));
        ProcessName = _selectedRemoteProcessOption?.ProcessKind ?? value;
        if (_selectedRemoteProcessOption is not null)
        {
            RemoteLogPath = _selectedRemoteProcessOption.LogPath;
        }
    }

    private static string ReplaceFilterOptions(
        ObservableCollection<string> options,
        string allOption,
        IEnumerable<string> values,
        string currentSelection)
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

        return IsAllOrInvalidFilter(currentSelection, allOption, options)
            ? allOption
            : currentSelection;
    }

    private static bool IsAllOrInvalidFilter(string selectedValue, string allOption, ObservableCollection<string> options)
    {
        return string.IsNullOrWhiteSpace(selectedValue)
            || string.Equals(selectedValue, allOption, StringComparison.OrdinalIgnoreCase)
            || !options.Any(option => string.Equals(option, selectedValue, StringComparison.OrdinalIgnoreCase));
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

    private sealed record RemoteProcessLogOption(
        string DisplayName,
        string NodeName,
        string ProcessKind,
        string ProcessId,
        string LogPath)
    {
        public static RemoteProcessLogOption FromProcess(RuntimeProcess process)
        {
            var processKind = string.Equals(process.ProcessKind, "frps", StringComparison.OrdinalIgnoreCase)
                ? "frps"
                : "frpc";
            var processId = string.IsNullOrWhiteSpace(process.ProcessId) ? "-" : process.ProcessId;

            return new RemoteProcessLogOption(
                $"{processKind} · PID {processId}",
                process.NodeName,
                processKind,
                processId,
                processKind == "frps" ? FrpsLogPath : FrpcLogPath);
        }
    }
}
