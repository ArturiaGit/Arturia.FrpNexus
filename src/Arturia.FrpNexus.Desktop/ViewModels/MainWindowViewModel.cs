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
    private readonly ILocalFrpcProcessService _localFrpcProcessService;
    private readonly IFrpLifecycleStateService _frpLifecycleStateService;
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
        ILocalFrpcProcessService localFrpcProcessService,
        IFrpLifecycleStateService frpLifecycleStateService,
        IConfirmationDialogService confirmationDialogService,
        IModalOverlayService modalOverlayService,
        IModalDialogHostService modalDialogHostService)
    {
        _nodeConnectionSessionService = nodeConnectionSessionService;
        _localFrpcProcessService = localFrpcProcessService;
        _frpLifecycleStateService = frpLifecycleStateService;
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

    public bool IsWorkflowDialogVisible => IsModalDialogVisible && !IsConfirmationDialogVisible;

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

        if (_frpLifecycleStateService
            .ListRemoteFrpsSnapshots()
            .Any(snapshot => snapshot.IsSshOnline && snapshot.FrpsStatus == FrpNexusStatus.Running))
        {
            risks.Add("remote-frps");
        }

        return risks;
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
            OnPropertyChanged(nameof(IsWorkflowDialogVisible));
        }

        if (string.IsNullOrEmpty(e.PropertyName)
            || string.Equals(e.PropertyName, nameof(IModalDialogHostService.CurrentDialog), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(CurrentModalDialog));
            OnPropertyChanged(nameof(IsConfirmationDialogVisible));
            OnPropertyChanged(nameof(IsWorkflowDialogVisible));
        }
    }
}
