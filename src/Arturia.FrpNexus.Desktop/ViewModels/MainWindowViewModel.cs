using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.Models;
using Arturia.FrpNexus.Desktop.Services;
using Arturia.FrpNexus.Desktop.ViewModels.Dialogs;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PageViewModelBase = Arturia.FrpNexus.Desktop.ViewModels.Pages.PageViewModel;

namespace Arturia.FrpNexus.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly INodeConnectionSessionService _nodeConnectionSessionService;
    private readonly INodeManagementService _nodeManagementService;
    private readonly IRemoteRuntimeService _remoteRuntimeService;
    private readonly ILocalFrpcProcessService _localFrpcProcessService;
    private readonly IFrpLifecycleStateService _frpLifecycleStateService;
    private readonly IRemoteFrpsRetentionService _remoteFrpsRetentionService;
    private readonly IConfirmationDialogService _confirmationDialogService;
    private readonly IModalOverlayService _modalOverlayService;
    private readonly IModalDialogHostService _modalDialogHostService;
    private bool _isCloseConfirmed;

    [ObservableProperty]
    private NavigationItem _selectedNavigationItem;

    public MainWindowViewModel(
        DashboardPageViewModel dashboardPage,
        NodesPageViewModel nodesPage,
        TunnelsPageViewModel tunnelsPage,
        ConfigurationsPageViewModel configurationsPage,
        RuntimePageViewModel runtimePage,
        LogsPageViewModel logsPage,
        SettingsPageViewModel settingsPage,
        INavigationRequestService navigationRequestService,
        INodeConnectionSessionService nodeConnectionSessionService,
        INodeManagementService nodeManagementService,
        IRemoteRuntimeService remoteRuntimeService,
        ILocalFrpcProcessService localFrpcProcessService,
        IFrpLifecycleStateService frpLifecycleStateService,
        IRemoteFrpsRetentionService remoteFrpsRetentionService,
        IConfirmationDialogService confirmationDialogService,
        IModalOverlayService modalOverlayService,
        IModalDialogHostService modalDialogHostService)
    {
        _nodeConnectionSessionService = nodeConnectionSessionService;
        _nodeManagementService = nodeManagementService;
        _remoteRuntimeService = remoteRuntimeService;
        _localFrpcProcessService = localFrpcProcessService;
        _frpLifecycleStateService = frpLifecycleStateService;
        _remoteFrpsRetentionService = remoteFrpsRetentionService;
        _confirmationDialogService = confirmationDialogService;
        _modalOverlayService = modalOverlayService;
        _modalDialogHostService = modalDialogHostService;
        _modalOverlayService.PropertyChanged += OnModalOverlayServicePropertyChanged;
        _modalDialogHostService.PropertyChanged += OnModalDialogHostServicePropertyChanged;

        var navigateCommand = new RelayCommand<NavigationItem>(Navigate);

        NavigationItems =
        [
            new("仪表盘", "dashboard", dashboardPage, navigateCommand),
            new("节点", "nodes", nodesPage, navigateCommand),
            new("隧道", "tunnels", tunnelsPage, navigateCommand),
            new("配置预览", "configurations", configurationsPage, navigateCommand),
            new("日志", "logs", logsPage, navigateCommand),
            new("设置", "settings", settingsPage, navigateCommand)
        ];

        _selectedNavigationItem = NavigationItems[0];
        _selectedNavigationItem.IsSelected = true;
        navigationRequestService.NavigationRequested += (_, pageKey) => NavigateToPageKey(pageKey);
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public PageViewModelBase CurrentPage => SelectedNavigationItem.Page;

    public string CurrentPageTitle => CurrentPage.Title;

    public string CurrentPageSubtitle => CurrentPage.Subtitle;

    public string ConnectionText => "连接状态: 已就绪";

    public FrpNexusStatus ConnectionStatus => FrpNexusStatus.Ready;

    public bool IsModalOverlayVisible => _modalOverlayService.IsOverlayVisible;

    public bool IsModalDialogVisible => _modalDialogHostService.IsDialogVisible;

    public bool IsConfirmationDialogVisible => CurrentModalDialog is ConfirmationDialogViewModel;

    public bool IsFrpCoreDownloadOptionsDialogVisible => CurrentModalDialog is FrpCoreDownloadOptionsDialogViewModel;

    public bool IsWorkflowDialogVisible => IsModalDialogVisible
        && !IsConfirmationDialogVisible
        && !IsFrpCoreDownloadOptionsDialogVisible;

    public object? CurrentModalDialog => _modalDialogHostService.CurrentDialog;

    partial void OnSelectedNavigationItemChanged(NavigationItem value)
    {
        foreach (var item in NavigationItems)
        {
            item.IsSelected = ReferenceEquals(item, value);
        }

        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(CurrentPageTitle));
        OnPropertyChanged(nameof(CurrentPageSubtitle));
    }

    private void Navigate(NavigationItem? item)
    {
        if (item is not null)
        {
            SelectedNavigationItem = item;
            _ = RefreshCurrentPageAsync(item.Page);
        }
    }

    public async Task<bool> ConfirmCloseAsync(CancellationToken cancellationToken = default)
    {
        if (_isCloseConfirmed)
        {
            return true;
        }

        var risks = await CollectLifecycleRisksAsync(cancellationToken);
        if (risks.Count == 0)
        {
            _isCloseConfirmed = true;
            return true;
        }

        var remoteFrpsSnapshots = GetRunningRemoteFrpsSnapshots();
        if (remoteFrpsSnapshots.Count > 0)
        {
            return await ConfirmCloseWithRemoteFrpsAsync(remoteFrpsSnapshots, cancellationToken);
        }

        var confirmed = await _confirmationDialogService.ShowAsync(
            new ConfirmationDialogRequest(
                "仍有 FRP 进程在运行",
                "关闭 FrpNexus 不会停止本地 frpc 或远程 frps；SSH 会话会断开。需要停止进程时，请先返回手动操作。",
                "继续关闭",
                "返回处理",
                "warning"),
            cancellationToken);
        if (confirmed)
        {
            _isCloseConfirmed = true;
        }

        return confirmed;
    }

    private async Task<bool> ConfirmCloseWithRemoteFrpsAsync(
        IReadOnlyList<RemoteFrpsLifecycleSnapshot> remoteFrpsSnapshots,
        CancellationToken cancellationToken)
    {
        var choice = await _confirmationDialogService.ShowChoiceAsync(
            new ConfirmationDialogChoiceRequest(
                "远程 frps 仍在运行",
                "检测到远程 frps 仍在运行。你可以停止 frps 后关闭，也可以保留远程 frps 继续运行并在下次连接同一节点时接入状态。",
                "停止 frps 并关闭",
                "保持 frps 运行并关闭",
                "返回处理",
                "warning"),
            cancellationToken);

        if (choice == ConfirmationDialogResult.Cancel)
        {
            return false;
        }

        if (choice == ConfirmationDialogResult.Secondary)
        {
            foreach (var snapshot in remoteFrpsSnapshots)
            {
                await _remoteFrpsRetentionService.SaveAsync(
                    new RemoteFrpsRetentionRecord(
                        snapshot.NodeName,
                        DateTimeOffset.UtcNow,
                        "kept-running",
                        snapshot.ConfigPath),
                    cancellationToken);
            }

            _isCloseConfirmed = true;
            return true;
        }

        var failures = await StopRemoteFrpsSnapshotsAsync(remoteFrpsSnapshots, cancellationToken);
        if (failures.Count == 0)
        {
            _isCloseConfirmed = true;
            return true;
        }

        var stillClose = await _confirmationDialogService.ShowAsync(
            new ConfirmationDialogRequest(
                "停止 frps 失败",
                $"以下节点的远程 frps 未能停止：{string.Join("；", failures)}。可以仍然关闭 FrpNexus，远程 frps 将继续运行。",
                "仍然关闭",
                "返回处理",
                "error"),
            cancellationToken);
        if (stillClose)
        {
            _isCloseConfirmed = true;
        }

        return stillClose;
    }

    private async Task<IReadOnlyList<string>> StopRemoteFrpsSnapshotsAsync(
        IReadOnlyList<RemoteFrpsLifecycleSnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        var nodes = await _nodeManagementService.ListNodesAsync(cancellationToken);

        foreach (var snapshot in snapshots)
        {
            var node = nodes.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, snapshot.NodeName, StringComparison.OrdinalIgnoreCase));
            if (node is null)
            {
                failures.Add($"{snapshot.NodeName}: 未找到节点配置");
                continue;
            }

            var credential = _nodeConnectionSessionService.GetConnectedCredential(snapshot.NodeName);
            if (credential is null)
            {
                failures.Add($"{snapshot.NodeName}: 当前 SSH 会话凭据不可用");
                continue;
            }

            var processes = await _remoteRuntimeService.GetProcessesAsync(
                new RemoteRuntimeQueryRequest(node, credential),
                cancellationToken);
            var match = SelectRemoteFrpsProcess(processes, snapshot.ConfigPath);
            if (match.Process is null)
            {
                failures.Add($"{snapshot.NodeName}: {match.Message}");
                continue;
            }

            var result = await _remoteRuntimeService.StopAsync(
                new RemoteRuntimeCommandRequest(
                    node,
                    credential,
                    match.Process.Name,
                    "frps",
                    BuildKillProcessCommand(match.Process.ProcessId)),
                cancellationToken);
            if (result.Status == FrpNexusStatus.Error)
            {
                failures.Add($"{snapshot.NodeName}: {result.Message}");
                continue;
            }

            _frpLifecycleStateService.UpdateRemoteFrpsState(
                snapshot.NodeName,
                isSshOnline: true,
                FrpNexusStatus.Stopped,
                snapshot.ConfigPath);
            await _remoteFrpsRetentionService.ClearAsync(snapshot.NodeName, cancellationToken);
        }

        return failures;
    }

    private static (RuntimeProcess? Process, string Message) SelectRemoteFrpsProcess(
        IReadOnlyList<RuntimeProcess> processes,
        string configPath)
    {
        var frpsProcesses = processes
            .Where(process => string.Equals(process.ProcessKind, "frps", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (frpsProcesses.Length == 0)
        {
            return (null, "frps 未运行");
        }

        var matchedByConfig = frpsProcesses
            .Where(process => CommandUsesConfigPath(process.CommandLine, configPath))
            .ToArray();
        if (matchedByConfig.Length == 1)
        {
            return (matchedByConfig[0], string.Empty);
        }

        if (matchedByConfig.Length > 1)
        {
            return (null, "发现多个 frps 使用同一配置，无法唯一匹配");
        }

        return frpsProcesses.Length == 1
            ? (frpsProcesses[0], string.Empty)
            : (null, "发现多个 frps，无法唯一匹配当前配置");
    }

    private static bool CommandUsesConfigPath(string commandLine, string expectedConfigPath)
    {
        if (string.IsNullOrWhiteSpace(commandLine) || string.IsNullOrWhiteSpace(expectedConfigPath))
        {
            return false;
        }

        var normalizedCommand = NormalizeLinuxPathForCommandMatch(commandLine);
        var normalizedExpectedPath = NormalizeLinuxPathForCommandMatch(expectedConfigPath);
        return normalizedCommand.Contains(normalizedExpectedPath, StringComparison.Ordinal);
    }

    private static string NormalizeLinuxPathForCommandMatch(string value)
    {
        return value.Trim().Trim('"', '\'').Replace("\\ ", " ", StringComparison.Ordinal);
    }

    private static string BuildKillProcessCommand(string processId)
    {
        return int.TryParse(processId, out var pid) && pid > 0
            ? $"kill {pid} && sleep 1; if kill -0 {pid} 2>/dev/null; then echo 'frps 未停止'; exit 1; fi"
            : "echo 'frps PID 无效'; exit 1";
    }

    private async Task<IReadOnlyList<string>> CollectLifecycleRisksAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        cancellationToken.ThrowIfCancellationRequested();

        var risks = new List<string>();
        var managedFrpcSessions = _localFrpcProcessService
            .ListManagedSessions()
            .Where(session => session.Status == FrpNexusStatus.Running && session.IsManaged)
            .ToArray();
        if (managedFrpcSessions.Length > 0)
        {
            risks.Add("local-frpc");
        }

        var activeSessions = _nodeConnectionSessionService.ListActiveSessions();
        if (activeSessions.Count > 0)
        {
            risks.Add("ssh");
        }

        if (GetRunningRemoteFrpsSnapshots().Count > 0)
        {
            risks.Add("remote-frps");
        }

        return risks;
    }

    private IReadOnlyList<RemoteFrpsLifecycleSnapshot> GetRunningRemoteFrpsSnapshots()
    {
        return _frpLifecycleStateService
            .ListRemoteFrpsSnapshots()
            .Where(snapshot => snapshot.IsSshOnline && snapshot.FrpsStatus == FrpNexusStatus.Running)
            .ToArray();
    }

    private void NavigateToPageKey(string pageKey)
    {
        var item = NavigationItems.FirstOrDefault(candidate =>
            string.Equals(candidate.Icon, pageKey, StringComparison.OrdinalIgnoreCase));

        if (item is not null)
        {
            Navigate(item);
        }
    }

    private static Task RefreshCurrentPageAsync(PageViewModelBase page)
    {
        return page switch
        {
            DashboardPageViewModel dashboard => dashboard.LoadDashboardAsync(),
            ConfigurationsPageViewModel configurations => configurations.LoadTargetNodesAsync(),
            LogsPageViewModel logs => logs.RefreshForNavigationAsync(),
            _ => Task.CompletedTask
        };
    }

    private void OnModalOverlayServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)
            || string.Equals(e.PropertyName, nameof(IModalOverlayService.IsOverlayVisible), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(IsModalOverlayVisible));
        }
    }

    private void OnModalDialogHostServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)
            || string.Equals(e.PropertyName, nameof(IModalDialogHostService.IsDialogVisible), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(IsModalDialogVisible));
            OnPropertyChanged(nameof(IsConfirmationDialogVisible));
            OnPropertyChanged(nameof(IsFrpCoreDownloadOptionsDialogVisible));
            OnPropertyChanged(nameof(IsWorkflowDialogVisible));
        }

        if (string.IsNullOrEmpty(e.PropertyName)
            || string.Equals(e.PropertyName, nameof(IModalDialogHostService.CurrentDialog), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(CurrentModalDialog));
            OnPropertyChanged(nameof(IsConfirmationDialogVisible));
            OnPropertyChanged(nameof(IsFrpCoreDownloadOptionsDialogVisible));
            OnPropertyChanged(nameof(IsWorkflowDialogVisible));
        }
    }
}
