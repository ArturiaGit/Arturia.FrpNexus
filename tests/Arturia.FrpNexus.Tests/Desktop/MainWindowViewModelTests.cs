using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Application.Configuration;
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
using Arturia.FrpNexus.Infrastructure.Configurations;
using Arturia.FrpNexus.Infrastructure.Deployments;
using Arturia.FrpNexus.Infrastructure.Runtime;
using Arturia.FrpNexus.Infrastructure.Sftp;
using Arturia.FrpNexus.Infrastructure.Ssh;
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
        Assert.IsType<ConfigurationsPageView>(locator.Build(CreateConfigurationsPageViewModel()));
        Assert.IsType<RuntimePageView>(locator.Build(CreateRuntimePageViewModel()));
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
        Assert.IsType<SqliteConfigurationVersionService>(serviceProvider.GetRequiredService<IConfigurationVersionService>());
        Assert.IsType<SqliteRuntimeRecordService>(serviceProvider.GetRequiredService<IRuntimeRecordService>());
        Assert.IsType<SqliteDeploymentRecordService>(serviceProvider.GetRequiredService<IDeploymentRecordService>());
        Assert.IsType<SqliteSettingsService>(serviceProvider.GetRequiredService<ISettingsService>());
        Assert.IsType<SshConnectionService>(serviceProvider.GetRequiredService<ISshConnectionService>());
        Assert.IsType<RemoteFileTransferService>(serviceProvider.GetRequiredService<IRemoteFileTransferService>());
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
            CreateConfigurationsPageViewModel(),
            CreateRuntimePageViewModel(),
            new LogsPageViewModel(),
            CreateSettingsPageViewModel());
    }

    private static NodesPageViewModel CreateNodesPageViewModel()
    {
        return new NodesPageViewModel(new FakeNodeManagementService(), new FakeSshConnectionService());
    }

    private static TunnelsPageViewModel CreateTunnelsPageViewModel()
    {
        return new TunnelsPageViewModel(new FakeTunnelManagementService());
    }

    private static ConfigurationsPageViewModel CreateConfigurationsPageViewModel()
    {
        return new ConfigurationsPageViewModel(new TomlConfigurationService(), new FakeConfigurationVersionService());
    }

    private static RuntimePageViewModel CreateRuntimePageViewModel()
    {
        return new RuntimePageViewModel(new FakeRuntimeRecordService(), new FakeDeploymentRecordService());
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

        public Task UpdateConnectionTestResultAsync(
            string nodeName,
            FrpNexusStatus status,
            DateTimeOffset testedAt,
            CancellationToken cancellationToken = default)
        {
            var index = _nodes.FindIndex(node => node.Name == nodeName);
            if (index >= 0)
            {
                var node = _nodes[index];
                _nodes[index] = node with
                {
                    ConnectionStatus = status,
                    LastConnectionTestedAt = testedAt
                };
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeSshConnectionService : ISshConnectionService
    {
        public Task<SshConnectionTestResult> TestConnectionAsync(SshConnectionTestRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SshConnectionTestResult(
                request.Node.Name,
                FrpNexusStatus.Online,
                DateTimeOffset.UtcNow,
                "SSH 连接测试成功。"));
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

    private sealed class FakeConfigurationVersionService : IConfigurationVersionService
    {
        public Task<IReadOnlyList<ConfigurationVersion>> ListConfigurationsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ConfigurationVersion>>([]);
        }

        public Task<ConfigurationVersion?> GetConfigurationAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ConfigurationVersion?>(null);
        }

        public Task SaveConfigurationAsync(ConfigurationVersion configuration, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteConfigurationAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRuntimeRecordService : IRuntimeRecordService
    {
        private readonly List<RuntimeProcess> _processes =
        [
            new("frps-main", "Web-Server-HK", "frps", FrpNexusStatus.Running, "14022", "4d 12h 30m", "0.0.0.0:7000"),
            new("frpc-web", "Web-Server-HK", "frpc", FrpNexusStatus.Running, "14090", "4d 10h 12m", "127.0.0.1:8080"),
            new("frpc-db", "DB-Node-SH", "frpc", FrpNexusStatus.Stopped, "-", "-", "127.0.0.1:3306"),
            new("frpc-edge", "Edge-Router-BJ", "frpc", FrpNexusStatus.Error, "-", "连接失败", "127.0.0.1:7777")
        ];

        public Task<IReadOnlyList<RuntimeProcess>> ListRuntimeProcessesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RuntimeProcess>>(_processes);
        }

        public Task<RuntimeProcess?> GetRuntimeProcessAsync(string processName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_processes.FirstOrDefault(process => process.Name == processName));
        }

        public Task SaveRuntimeProcessAsync(RuntimeProcess process, CancellationToken cancellationToken = default)
        {
            _processes.RemoveAll(item => item.Name == process.Name);
            _processes.Add(process);
            return Task.CompletedTask;
        }

        public Task DeleteRuntimeProcessAsync(string processName, CancellationToken cancellationToken = default)
        {
            _processes.RemoveAll(process => process.Name == processName);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDeploymentRecordService : IDeploymentRecordService
    {
        private readonly List<DeploymentRecord> _records =
        [
            new("测试 SSH 连接", "Web-Server-HK", "确认远程 Linux 节点凭据可用", FrpNexusStatus.Ready, DateTimeOffset.UtcNow),
            new("下载 FRP Release", "Web-Server-HK", "选择适合目标系统的 frpc / frps", FrpNexusStatus.Pending, DateTimeOffset.UtcNow),
            new("通过 SFTP 上传核心", "Web-Server-HK", "上传二进制文件与 TOML 配置", FrpNexusStatus.Pending, DateTimeOffset.UtcNow),
            new("启动远程进程", "Web-Server-HK", "执行启动命令并读取状态", FrpNexusStatus.Pending, DateTimeOffset.UtcNow)
        ];

        public Task<IReadOnlyList<DeploymentRecord>> ListDeploymentRecordsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DeploymentRecord>>(_records);
        }

        public Task<DeploymentRecord?> GetDeploymentRecordAsync(string stepName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_records.FirstOrDefault(record => record.StepName == stepName));
        }

        public Task SaveDeploymentRecordAsync(DeploymentRecord record, CancellationToken cancellationToken = default)
        {
            _records.RemoveAll(item => item.StepName == record.StepName);
            _records.Add(record);
            return Task.CompletedTask;
        }

        public Task DeleteDeploymentRecordAsync(string stepName, CancellationToken cancellationToken = default)
        {
            _records.RemoveAll(record => record.StepName == stepName);
            return Task.CompletedTask;
        }
    }
}
