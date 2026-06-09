using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.Models;
using Arturia.FrpNexus.Desktop.Services;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PageViewModelBase = Arturia.FrpNexus.Desktop.ViewModels.Pages.PageViewModel;

namespace Arturia.FrpNexus.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IModalOverlayService _modalOverlayService;
    private readonly IModalDialogHostService _modalDialogHostService;

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
        IModalOverlayService modalOverlayService,
        IModalDialogHostService modalDialogHostService)
    {
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
            new("配置", "configurations", configurationsPage, navigateCommand),
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
        }

        if (string.IsNullOrEmpty(e.PropertyName)
            || string.Equals(e.PropertyName, nameof(IModalDialogHostService.CurrentDialog), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(CurrentModalDialog));
        }
    }
}
