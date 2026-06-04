using System.Collections.ObjectModel;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.Models;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private NavigationItem _selectedNavigationItem;

    public MainWindowViewModel(
        DashboardPageViewModel dashboardPage,
        NodesPageViewModel nodesPage,
        TunnelsPageViewModel tunnelsPage,
        ConfigurationsPageViewModel configurationsPage,
        RuntimePageViewModel runtimePage,
        LogsPageViewModel logsPage,
        SettingsPageViewModel settingsPage)
    {
        var navigateCommand = new RelayCommand<NavigationItem>(Navigate);

        NavigationItems =
        [
            new("仪表盘", "dashboard", dashboardPage, navigateCommand),
            new("节点", "nodes", nodesPage, navigateCommand),
            new("隧道", "tunnels", tunnelsPage, navigateCommand),
            new("配置", "configurations", configurationsPage, navigateCommand),
            new("运行", "runtime", runtimePage, navigateCommand),
            new("日志", "logs", logsPage, navigateCommand),
            new("设置", "settings", settingsPage, navigateCommand)
        ];

        _selectedNavigationItem = NavigationItems[0];
        _selectedNavigationItem.IsSelected = true;
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public PageViewModel CurrentPage => SelectedNavigationItem.Page;

    public string CurrentPageTitle => CurrentPage.Title;

    public string CurrentPageSubtitle => CurrentPage.Subtitle;

    public string ConnectionText => "连接状态: 已就绪";

    public FrpNexusStatus ConnectionStatus => FrpNexusStatus.Ready;

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
        }
    }

}
