using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop;
using Arturia.FrpNexus.Desktop.Composition;
using Arturia.FrpNexus.Desktop.Converters;
using Arturia.FrpNexus.Desktop.Logging;
using Arturia.FrpNexus.Desktop.ViewModels;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;
using Arturia.FrpNexus.Desktop.Views.Pages;
using Arturia.FrpNexus.Infrastructure.Nodes;
using Arturia.FrpNexus.Infrastructure.Settings;
using Arturia.FrpNexus.Infrastructure.Tunnels;
using Microsoft.Extensions.DependencyInjection;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void NavigationItems_ShouldUseRequiredChineseModuleOrder()
    {
        var viewModel = CreateMainWindowViewModel();

        var titles = viewModel.NavigationItems.Select(item => item.Title).ToArray();

        Assert.Equal(["仪表盘", "节点", "隧道", "配置", "运行", "日志", "设置"], titles);
    }

    [Fact]
    public void Constructor_ShouldSelectDashboardByDefault()
    {
        var viewModel = CreateMainWindowViewModel();

        Assert.Equal("仪表盘", viewModel.SelectedNavigationItem.Title);
        Assert.True(viewModel.SelectedNavigationItem.IsSelected);
        Assert.IsType<DashboardPageViewModel>(viewModel.CurrentPage);
        Assert.Equal("仪表盘概览", viewModel.CurrentPageTitle);
    }

    [Fact]
    public void NavigateCommand_ShouldUpdateCurrentPage()
    {
        var viewModel = CreateMainWindowViewModel();
        var logsItem = viewModel.NavigationItems.Single(item => item.Title == "日志");

        logsItem.NavigateCommand.Execute(logsItem);

        Assert.Equal(logsItem, viewModel.SelectedNavigationItem);
        Assert.True(logsItem.IsSelected);
        Assert.DoesNotContain(viewModel.NavigationItems.Where(item => item.Title != "日志"), item => item.IsSelected);
        Assert.IsType<LogsPageViewModel>(viewModel.CurrentPage);
        Assert.Equal("日志", viewModel.CurrentPageTitle);
        Assert.Equal("筛选、搜索并查看远程 FRP 日志输出", viewModel.CurrentPageSubtitle);
    }

    [Fact]
    public void SampleData_ShouldExposeNodesTunnelsAndLogs()
    {
        var nodes = (NodesPageViewModel)CreateMainWindowViewModel()
            .NavigationItems.Single(item => item.Title == "节点").Page;
        var tunnels = (TunnelsPageViewModel)CreateMainWindowViewModel()
            .NavigationItems.Single(item => item.Title == "隧道").Page;
        var logs = (LogsPageViewModel)CreateMainWindowViewModel()
            .NavigationItems.Single(item => item.Title == "日志").Page;
        var runtime = (RuntimePageViewModel)CreateMainWindowViewModel()
            .NavigationItems.Single(item => item.Title == "运行").Page;

        Assert.Contains(nodes.Nodes, node => node.ConnectionStatus == FrpNexusStatus.Online);
        Assert.Contains(nodes.Nodes, node => node.ConnectionStatus == FrpNexusStatus.Offline);
        Assert.Contains(nodes.Nodes, node => node.FrpStatus == FrpNexusStatus.Running);
        Assert.Contains(nodes.Nodes, node => node.FrpStatus == FrpNexusStatus.Stopped);
        Assert.Contains(tunnels.Tunnels, tunnel => tunnel.Protocol == TunnelProtocol.Tcp);
        Assert.Contains(tunnels.Tunnels, tunnel => tunnel.Protocol == TunnelProtocol.Udp);
        Assert.Contains(tunnels.Tunnels, tunnel => tunnel.Protocol == TunnelProtocol.Http);
        Assert.Contains(tunnels.Tunnels, tunnel => tunnel.Protocol == TunnelProtocol.Https);
        Assert.Contains(tunnels.Tunnels, tunnel => tunnel.Status == FrpNexusStatus.Warning);
        Assert.Contains(logs.Logs, log => log.Status == FrpNexusStatus.Error);
        Assert.Contains(runtime.Processes, process => process.Status == FrpNexusStatus.Running);
        Assert.Contains(runtime.Processes, process => process.Status == FrpNexusStatus.Stopped);
        Assert.Contains(runtime.Processes, process => process.Status == FrpNexusStatus.Error);
    }

    [Fact]
    public void ConfigurationPreview_ShouldContainTomlProxyFields()
    {
        var configurations = (ConfigurationsPageViewModel)CreateMainWindowViewModel()
            .NavigationItems.Single(item => item.Title == "配置").Page;
        var toml = configurations.Preview.Toml;

        Assert.Contains("[[proxies]]", toml);
        Assert.Contains("type = \"http\"", toml);
        Assert.Contains("localPort = 8080", toml);
        Assert.Contains("customDomains = [\"example.com\"]", toml);
    }

    [Fact]
    public void PageViewTypes_ShouldExistForEveryMainModuleViewModel()
    {
        Assert.NotNull(typeof(DashboardPageView));
        Assert.NotNull(typeof(NodesPageView));
        Assert.NotNull(typeof(TunnelsPageView));
        Assert.NotNull(typeof(ConfigurationsPageView));
        Assert.NotNull(typeof(RuntimePageView));
        Assert.NotNull(typeof(LogsPageView));
        Assert.NotNull(typeof(SettingsPageView));
    }

    [Fact]
    public void Converters_ShouldLiveInDedicatedConvertersNamespace()
    {
        Assert.Equal("Arturia.FrpNexus.Desktop.Converters", typeof(StatusTextConverter).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.Converters", typeof(StatusClassesConverter).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.Converters", typeof(TunnelProtocolTextConverter).Namespace);
    }

    [Fact]
    public void ViewLocator_ShouldMapPageViewModelsToPageViews()
    {
        var locator = new ViewLocator();

        Assert.IsType<DashboardPageView>(locator.Build(new DashboardPageViewModel()));
        Assert.IsType<NodesPageView>(locator.Build(CreateNodesPageViewModel()));
        Assert.IsType<TunnelsPageView>(locator.Build(CreateTunnelsPageViewModel()));
        Assert.IsType<ConfigurationsPageView>(locator.Build(new ConfigurationsPageViewModel()));
        Assert.IsType<RuntimePageView>(locator.Build(new RuntimePageViewModel()));
        Assert.IsType<LogsPageView>(locator.Build(new LogsPageViewModel()));
        Assert.IsType<SettingsPageView>(locator.Build(CreateSettingsPageViewModel()));
    }

    [Fact]
    public void DesktopCompositionRoot_ShouldResolveMainWindowViewModel()
    {
        using var serviceProvider = DesktopCompositionRoot.BuildServiceProvider();

        var viewModel = serviceProvider.GetRequiredService<MainWindowViewModel>();

        Assert.Equal("仪表盘", viewModel.SelectedNavigationItem.Title);
        Assert.IsType<DashboardPageViewModel>(viewModel.CurrentPage);
    }

    [Fact]
    public void DesktopCompositionRoot_ShouldResolvePhaseOneApplicationPlaceholders()
    {
        using var serviceProvider = DesktopCompositionRoot.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<INodeManagementService>());
        Assert.NotNull(serviceProvider.GetRequiredService<ISshConnectionService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IRemoteFileTransferService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IFrpReleaseService>());
        Assert.NotNull(serviceProvider.GetRequiredService<ITomlConfigurationService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IRemoteRuntimeService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IRemoteLogService>());
        Assert.IsType<SqliteNodeManagementService>(serviceProvider.GetRequiredService<INodeManagementService>());
        Assert.IsType<SqliteTunnelManagementService>(serviceProvider.GetRequiredService<ITunnelManagementService>());
        Assert.IsType<SqliteSettingsService>(serviceProvider.GetRequiredService<ISettingsService>());
    }

    [Fact]
    public void DesktopCompositionRoot_ShouldResolveSettingsPageViewModelWithSettingsService()
    {
        using var serviceProvider = DesktopCompositionRoot.BuildServiceProvider();

        var viewModel = serviceProvider.GetRequiredService<SettingsPageViewModel>();

        Assert.NotNull(viewModel);
        Assert.NotEmpty(viewModel.SshKeys);
    }

    [Fact]
    public void DesktopLogPath_ShouldUseLocalApplicationData()
    {
        var logPath = DesktopLogPaths.GetWarningLogPath();

        Assert.Contains(
            Path.Combine("Arturia", "FrpNexus", "logs"),
            logPath);
        Assert.EndsWith("frpnexus-.log", logPath);
    }

    private static MainWindowViewModel CreateMainWindowViewModel()
    {
        return new MainWindowViewModel(
            new DashboardPageViewModel(),
            CreateNodesPageViewModel(),
            CreateTunnelsPageViewModel(),
            new ConfigurationsPageViewModel(),
            new RuntimePageViewModel(),
            new LogsPageViewModel(),
            CreateSettingsPageViewModel());
    }

    private static NodesPageViewModel CreateNodesPageViewModel()
    {
        return new NodesPageViewModel(new FakeNodeManagementService());
    }

    private static TunnelsPageViewModel CreateTunnelsPageViewModel()
    {
        return new TunnelsPageViewModel(new FakeTunnelManagementService());
    }

    private static SettingsPageViewModel CreateSettingsPageViewModel()
    {
        return new SettingsPageViewModel(new FakeSettingsService());
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        private readonly FrpNexusSettingsSnapshot _settings = new(
            "Light",
            "zh-CN",
            "GitHub Releases",
            @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\core",
            @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\configs",
            @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\logs",
            @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\data\frpnexus.db");

        public Task<FrpNexusSettingsSnapshot> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_settings);
        }

        public Task SaveSettingsAsync(FrpNexusSettingsSnapshot settings, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNodeManagementService : INodeManagementService
    {
        private readonly List<NodeProfile> _nodes =
        [
            new("Web-Server-HK", "103.114.160.22", 22, "root", "密钥 (ID_RSA_HK)", "Linux x86_64 (Ubuntu 22.04 LTS)", FrpNexusStatus.Online, FrpNexusStatus.Running, "v0.51.3", "4d 12h 30m", "/etc/frp/frpc.toml"),
            new("DB-Node-SH", "47.101.44.112", 22, "deploy", "密钥 (ID_RSA_SH)", "Debian 12", FrpNexusStatus.Online, FrpNexusStatus.Stopped, "v0.51.3", "-", "/opt/frp/frpc.toml"),
            new("Edge-Router-BJ", "123.56.77.89", 2222, "root", "密钥 (ID_RSA_BJ)", "Ubuntu 20.04 LTS", FrpNexusStatus.Offline, FrpNexusStatus.Stopped, "-", "-", "/etc/frp/frpc.toml")
        ];

        public Task<IReadOnlyList<NodeProfile>> ListNodesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<NodeProfile>>(_nodes);
        }

        public Task<NodeProfile?> GetNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_nodes.FirstOrDefault(node => node.Name == nodeName));
        }

        public Task SaveNodeAsync(NodeProfile node, CancellationToken cancellationToken = default)
        {
            _nodes.RemoveAll(item => item.Name == node.Name);
            _nodes.Add(node);
            return Task.CompletedTask;
        }

        public Task DeleteNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            _nodes.RemoveAll(node => node.Name == nodeName);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTunnelManagementService : ITunnelManagementService
    {
        private readonly List<TunnelProfile> _tunnels =
        [
            new("web-dev-portal", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中"),
            new("ssh-bastion", TunnelProtocol.Tcp, "Node-Beta-SG", "127.0.0.1", 22, "60022", FrpNexusStatus.Running, "运行中"),
            new("udp-game-server", TunnelProtocol.Udp, "Node-Gamma-JP", "127.0.0.1", 7777, "7777", FrpNexusStatus.Error, "端口被占用"),
            new("secure-api", TunnelProtocol.Https, "Node-Alpha-HK", "127.0.0.1", 8443, "api.example.com", FrpNexusStatus.Warning, "证书待检查")
        ];

        public Task<IReadOnlyList<TunnelProfile>> ListTunnelsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TunnelProfile>>(_tunnels);
        }

        public Task<TunnelProfile?> GetTunnelAsync(string tunnelName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tunnels.FirstOrDefault(tunnel => tunnel.Name == tunnelName));
        }

        public Task SaveTunnelAsync(TunnelProfile tunnel, CancellationToken cancellationToken = default)
        {
            _tunnels.RemoveAll(item => item.Name == tunnel.Name);
            _tunnels.Add(tunnel);
            return Task.CompletedTask;
        }

        public Task DeleteTunnelAsync(string tunnelName, CancellationToken cancellationToken = default)
        {
            _tunnels.RemoveAll(tunnel => tunnel.Name == tunnelName);
            return Task.CompletedTask;
        }
    }
}
