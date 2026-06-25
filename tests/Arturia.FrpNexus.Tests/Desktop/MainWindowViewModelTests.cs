using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Application.Configuration;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop;
using Arturia.FrpNexus.Desktop.Composition;
using Arturia.FrpNexus.Desktop.Converters;
using Arturia.FrpNexus.Desktop.Logging;
using Arturia.FrpNexus.Desktop.Services;
using Arturia.FrpNexus.Desktop.Theming;
using Arturia.FrpNexus.Desktop.ViewModels;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;
using Arturia.FrpNexus.Desktop.Views.Pages;
using Arturia.FrpNexus.Infrastructure.Nodes;
using Arturia.FrpNexus.Infrastructure.Settings;
using Arturia.FrpNexus.Infrastructure.Tunnels;
using Arturia.FrpNexus.Infrastructure.Configurations;
using Arturia.FrpNexus.Infrastructure.Deployments;
using Arturia.FrpNexus.Infrastructure.Logs;
using Arturia.FrpNexus.Infrastructure.Releases;
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

        Assert.Equal(["仪表盘", "节点", "隧道", "配置预览", "日志", "设置"], titles);
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
        Assert.Equal("筛选、搜索并查看 FRP 与 FrpNexus 日志输出", viewModel.CurrentPageSubtitle);
    }

    [Fact]
    public void SampleData_ShouldExposeNodesTunnelsAndRuntimeRecords()
    {
        var nodes = (NodesPageViewModel)CreateMainWindowViewModel()
            .NavigationItems.Single(item => item.Title == "节点").Page;
        var tunnels = (TunnelsPageViewModel)CreateMainWindowViewModel()
            .NavigationItems.Single(item => item.Title == "隧道").Page;
        var runtime = CreateRuntimePageViewModel();

        Assert.Contains(nodes.Nodes, node => node.ConnectionStatus == FrpNexusStatus.Online);
        Assert.Contains(nodes.Nodes, node => node.ConnectionStatus == FrpNexusStatus.Offline);
        Assert.Contains(nodes.Nodes, node => node.FrpStatus == FrpNexusStatus.Running);
        Assert.Contains(nodes.Nodes, node => node.FrpStatus == FrpNexusStatus.Stopped);
        Assert.Contains(tunnels.Tunnels, tunnel => tunnel.Protocol == TunnelProtocol.Tcp);
        Assert.Contains(tunnels.Tunnels, tunnel => tunnel.Protocol == TunnelProtocol.Udp);
        Assert.Contains(tunnels.Tunnels, tunnel => tunnel.Protocol == TunnelProtocol.Http);
        Assert.Contains(tunnels.Tunnels, tunnel => tunnel.Protocol == TunnelProtocol.Https);
        Assert.Contains(tunnels.Tunnels, tunnel => tunnel.Status == FrpNexusStatus.Warning);
        Assert.Contains(runtime.Processes, process => process.Status == FrpNexusStatus.Running);
        Assert.Contains(runtime.Processes, process => process.Status == FrpNexusStatus.Stopped);
        Assert.Contains(runtime.Processes, process => process.Status == FrpNexusStatus.Error);
    }

    [Fact]
    public void ConfigurationPreview_ShouldStartEmptyUntilGenerated()
    {
        var configurations = (ConfigurationsPageViewModel)CreateMainWindowViewModel()
            .NavigationItems.Single(item => item.Title == "配置预览").Page;
        Assert.Empty(configurations.TomlPreview);
        Assert.Single(configurations.TomlPreviewLines);
        Assert.Contains("TOML", configurations.TomlPreviewLines[0].Tokens[0].Text);
    }

    [Fact]
    public async Task NavigateCommand_ToConfigurations_ShouldRefreshPreviewData()
    {
        var nodeService = new CountingNodeManagementService();
        var configurationsPage = CreateConfigurationsPageViewModel(nodeService);
        var viewModel = CreateMainWindowViewModel(configurationsPage: configurationsPage);
        var configurationsItem = viewModel.NavigationItems.Single(item => item.Title == "配置预览");
        var beforeCount = nodeService.ListNodesCallCount;

        configurationsItem.NavigateCommand.Execute(configurationsItem);
        await Task.Delay(50);

        Assert.Equal(configurationsItem, viewModel.SelectedNavigationItem);
        Assert.Equal("配置预览", viewModel.CurrentPageTitle);
        Assert.True(nodeService.ListNodesCallCount > beforeCount);
    }

    [Fact]
    public async Task NavigateCommand_ToLogs_ShouldRefreshLocalLogs()
    {
        var localLogs = new FakeLocalApplicationLogService(
            new LogEntry("2026-06-15 10:00:00.000", "ERROR", "客户端", "FrpNexus", "本地错误", FrpNexusStatus.Error));
        var logsPage = CreateLogsPageViewModel(localLogs);
        var viewModel = CreateMainWindowViewModel(logsPage: logsPage);
        var logsItem = viewModel.NavigationItems.Single(item => item.Title == "日志");

        logsItem.NavigateCommand.Execute(logsItem);
        await Task.Delay(50);

        Assert.Equal(logsItem, viewModel.SelectedNavigationItem);
        Assert.Equal(1, localLogs.ReadCallCount);
        Assert.Single(logsPage.Logs);
        Assert.Equal("已读取 1 行本地日志。", logsPage.StatusText);
    }

    [Fact]
    public async Task NavigateCommand_ToLogs_WhenAutoRefreshDisabled_ShouldNotReadLocalLogs()
    {
        var localLogs = new FakeLocalApplicationLogService(
            new LogEntry("2026-06-15 10:00:00.000", "ERROR", "客户端", "FrpNexus", "本地错误", FrpNexusStatus.Error));
        var logsPage = CreateLogsPageViewModel(localLogs);
        logsPage.IsAutoRefreshEnabled = false;
        var viewModel = CreateMainWindowViewModel(logsPage: logsPage);
        var logsItem = viewModel.NavigationItems.Single(item => item.Title == "日志");

        logsItem.NavigateCommand.Execute(logsItem);
        await Task.Delay(50);

        Assert.Equal(logsItem, viewModel.SelectedNavigationItem);
        Assert.Equal(0, localLogs.ReadCallCount);
        Assert.Empty(logsPage.Logs);
    }

    [Fact]
    public async Task NavigateCommand_AwayFromLogs_ShouldDeactivateLogAutoRefresh()
    {
        var localLogs = new FakeLocalApplicationLogService(
            new LogEntry("2026-06-15 10:00:00.000", "ERROR", "客户端", "FrpNexus", "本地错误", FrpNexusStatus.Error));
        var logsPage = CreateLogsPageViewModel(localLogs, TimeSpan.FromMilliseconds(20));
        var viewModel = CreateMainWindowViewModel(logsPage: logsPage);
        var logsItem = viewModel.NavigationItems.Single(item => item.Title == "日志");
        var nodesItem = viewModel.NavigationItems.Single(item => item.Title == "节点");

        logsItem.NavigateCommand.Execute(logsItem);
        await WaitUntilAsync(() => localLogs.ReadCallCount >= 2);
        nodesItem.NavigateCommand.Execute(nodesItem);
        var readCountAfterLeavingLogs = localLogs.ReadCallCount;
        await Task.Delay(70);

        Assert.Equal(nodesItem, viewModel.SelectedNavigationItem);
        Assert.Equal(readCountAfterLeavingLogs, localLogs.ReadCallCount);
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

        Assert.True(locator.Match(CreateDashboardPageViewModel()));
        Assert.True(locator.Match(CreateNodesPageViewModel()));
        Assert.True(locator.Match(CreateTunnelsPageViewModel()));
        Assert.True(locator.Match(CreateConfigurationsPageViewModel()));
        Assert.True(locator.Match(CreateRuntimePageViewModel()));
        Assert.True(locator.Match(CreateLogsPageViewModel()));
        Assert.True(locator.Match(CreateSettingsPageViewModel()));
        Assert.NotNull(typeof(DashboardPageView));
        Assert.NotNull(typeof(NodesPageView));
        Assert.NotNull(typeof(TunnelsPageView));
        Assert.NotNull(typeof(ConfigurationsPageView));
        Assert.NotNull(typeof(RuntimePageView));
        Assert.NotNull(typeof(LogsPageView));
        Assert.NotNull(typeof(SettingsPageView));
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
        Assert.NotNull(serviceProvider.GetRequiredService<INodeConnectionSessionService>());
        Assert.NotNull(serviceProvider.GetRequiredService<ISshConnectionService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IRemoteFileTransferService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IFrpReleaseService>());
        Assert.NotNull(serviceProvider.GetRequiredService<ITomlConfigurationService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IRemoteRuntimeService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IRemoteLogService>());
        Assert.NotNull(serviceProvider.GetRequiredService<ILocalApplicationLogService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IFilePickerService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IClipboardService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IFrpCoreDownloadOptionsDialogService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IModalOverlayService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IModalDialogHostService>());
        Assert.IsType<SqliteNodeManagementService>(serviceProvider.GetRequiredService<INodeManagementService>());
        Assert.IsType<SqliteTunnelManagementService>(serviceProvider.GetRequiredService<ITunnelManagementService>());
        Assert.IsType<SqliteConfigurationVersionService>(serviceProvider.GetRequiredService<IConfigurationVersionService>());
        Assert.IsType<SqliteRuntimeRecordService>(serviceProvider.GetRequiredService<IRuntimeRecordService>());
        Assert.IsType<SqliteDeploymentRecordService>(serviceProvider.GetRequiredService<IDeploymentRecordService>());
        Assert.IsType<SqliteSettingsService>(serviceProvider.GetRequiredService<ISettingsService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IThemeService>());
        Assert.IsType<ModalOverlayService>(serviceProvider.GetRequiredService<IModalOverlayService>());
        Assert.IsType<ModalDialogHostService>(serviceProvider.GetRequiredService<IModalDialogHostService>());
        Assert.IsType<NodeConnectionSessionService>(serviceProvider.GetRequiredService<INodeConnectionSessionService>());
        Assert.IsType<SshConnectionService>(serviceProvider.GetRequiredService<ISshConnectionService>());
        Assert.IsType<RemoteFileTransferService>(serviceProvider.GetRequiredService<IRemoteFileTransferService>());
        Assert.IsType<FrpReleaseService>(serviceProvider.GetRequiredService<IFrpReleaseService>());
        Assert.IsType<RemoteRuntimeService>(serviceProvider.GetRequiredService<IRemoteRuntimeService>());
        Assert.IsType<RemoteLogService>(serviceProvider.GetRequiredService<IRemoteLogService>());
    }

    [Fact]
    public void DesktopCompositionRoot_ShouldResolveSettingsPageViewModelWithSettingsService()
    {
        using var serviceProvider = DesktopCompositionRoot.BuildServiceProvider();

        var viewModel = serviceProvider.GetRequiredService<SettingsPageViewModel>();

        Assert.NotNull(viewModel);
        Assert.NotNull(viewModel.CredentialSecurityNodes);
    }

    [Fact]
    public void ModalOverlayState_ShouldNotifyMainWindowBinding()
    {
        var modalOverlayService = new ModalOverlayService();
        var viewModel = CreateMainWindowViewModel(modalOverlayService);
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        using var overlay = modalOverlayService.ShowOverlay();

        Assert.True(viewModel.IsModalOverlayVisible);
        Assert.Contains(nameof(MainWindowViewModel.IsModalOverlayVisible), changedProperties);
    }

    [Fact]
    public void ModalDialogHostState_ShouldNotifyMainWindowBinding()
    {
        var modalDialogHostService = new ModalDialogHostService();
        var viewModel = CreateMainWindowViewModel(modalDialogHostService: modalDialogHostService);
        var dialog = new object();
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        modalDialogHostService.ShowDialog(dialog);

        Assert.True(viewModel.IsModalDialogVisible);
        Assert.Same(dialog, viewModel.CurrentModalDialog);
        Assert.Contains(nameof(MainWindowViewModel.IsModalDialogVisible), changedProperties);
        Assert.Contains(nameof(MainWindowViewModel.CurrentModalDialog), changedProperties);
    }

    [Fact]
    public async Task ConfirmCloseAsync_NoLifecycleRisk_ShouldNotShowConfirmation()
    {
        var confirmation = new FakeConfirmationDialogService();
        var viewModel = CreateMainWindowViewModel(confirmationDialogService: confirmation);

        var confirmed = await viewModel.ConfirmCloseAsync();

        Assert.True(confirmed);
        Assert.Equal(0, confirmation.ShowCount);
    }

    [Fact]
    public async Task ConfirmCloseAsync_ManagedFrpcRunning_ShouldShowConfirmation()
    {
        var localFrpc = new FakeLocalFrpcProcessService();
        localFrpc.MarkRunning("VPS-A");
        var remoteRuntime = new FakeRemoteRuntimeService();
        var confirmation = new FakeConfirmationDialogService { NextResult = false };
        var viewModel = CreateMainWindowViewModel(
            localFrpcProcessService: localFrpc,
            remoteRuntimeService: remoteRuntime,
            confirmationDialogService: confirmation);

        var confirmed = await viewModel.ConfirmCloseAsync();

        Assert.False(confirmed);
        Assert.Equal(1, confirmation.ShowCount);
        Assert.Equal("继续关闭", confirmation.LastRequest?.ConfirmButtonText);
        Assert.Contains("frpc", confirmation.LastRequest?.Message);
        Assert.Equal(0, remoteRuntime.GetProcessesRequestCount);
    }

    [Fact]
    public async Task ConfirmCloseAsync_RemoteFrpsKnownRunningInMemory_ShouldShowConfirmationWithoutRemoteProbe()
    {
        var lifecycle = new FakeFrpLifecycleStateService();
        lifecycle.UpdateRemoteFrpsState("VPS-A", isSshOnline: true, FrpNexusStatus.Running);
        var remoteRuntime = new FakeRemoteRuntimeService();
        var confirmation = new FakeConfirmationDialogService { NextResult = false };
        var viewModel = CreateMainWindowViewModel(
            frpLifecycleStateService: lifecycle,
            remoteRuntimeService: remoteRuntime,
            confirmationDialogService: confirmation);

        var confirmed = await viewModel.ConfirmCloseAsync();

        Assert.False(confirmed);
        Assert.Equal(1, confirmation.ShowCount);
        Assert.Contains("frps", confirmation.LastRequest?.Message);
        Assert.Equal(0, remoteRuntime.GetProcessesRequestCount);
    }

    [Fact]
    public async Task ConfirmCloseAsync_RemoteFrpsRunning_ShouldOfferStopKeepAndReturnChoices()
    {
        var lifecycle = new FakeFrpLifecycleStateService();
        lifecycle.UpdateRemoteFrpsState("Web-Server-HK", isSshOnline: true, FrpNexusStatus.Running, "/etc/frp/frps.toml");
        var confirmation = new FakeConfirmationDialogService { NextChoiceResult = ConfirmationDialogResult.Cancel };
        var viewModel = CreateMainWindowViewModel(
            frpLifecycleStateService: lifecycle,
            confirmationDialogService: confirmation);

        var confirmed = await viewModel.ConfirmCloseAsync();

        Assert.False(confirmed);
        Assert.Equal(1, confirmation.ShowChoiceCount);
        Assert.Equal("停止 frps 并关闭", confirmation.LastChoiceRequest?.ConfirmButtonText);
        Assert.Equal("保持 frps 运行并关闭", confirmation.LastChoiceRequest?.SecondaryButtonText);
        Assert.Equal("返回处理", confirmation.LastChoiceRequest?.CancelButtonText);
    }

    [Fact]
    public async Task ConfirmCloseAsync_KeepRemoteFrpsRunning_ShouldPersistNonSensitiveReminderAndClose()
    {
        var lifecycle = new FakeFrpLifecycleStateService();
        lifecycle.UpdateRemoteFrpsState("Web-Server-HK", isSshOnline: true, FrpNexusStatus.Running, "/etc/frp/frps.toml");
        var confirmation = new FakeConfirmationDialogService { NextChoiceResult = ConfirmationDialogResult.Secondary };
        var retention = new FakeRemoteFrpsRetentionService();
        var viewModel = CreateMainWindowViewModel(
            frpLifecycleStateService: lifecycle,
            confirmationDialogService: confirmation,
            remoteFrpsRetentionService: retention);

        var confirmed = await viewModel.ConfirmCloseAsync();

        Assert.True(confirmed);
        var record = Assert.Single(retention.Records);
        Assert.Equal("Web-Server-HK", record.NodeName);
        Assert.Equal("/etc/frp/frps.toml", record.ConfigPath);
        Assert.DoesNotContain("password", record.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", record.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmCloseAsync_StopRemoteFrps_ShouldKillUniqueProcessMatchedByConfigPathAndClose()
    {
        var lifecycle = new FakeFrpLifecycleStateService();
        lifecycle.UpdateRemoteFrpsState("Web-Server-HK", isSshOnline: true, FrpNexusStatus.Running, "/etc/frp/frps.toml");
        var sessionService = new FakeNodeConnectionSessionService();
        await sessionService.ConnectAsync(
            CreateNode("Web-Server-HK", "/etc/frp/frps.toml"),
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
        var remoteRuntime = new FakeRemoteRuntimeService
        {
            Processes =
            [
                new("frps-191", "Web-Server-HK", "frps", FrpNexusStatus.Running, "191", "00:13", "-", "/opt/frp/frps -c /etc/frp/frps.toml"),
                new("frps-222", "Web-Server-HK", "frps", FrpNexusStatus.Running, "222", "00:10", "-", "/opt/frp/frps -c /opt/frp/other.toml")
            ]
        };
        var confirmation = new FakeConfirmationDialogService { NextChoiceResult = ConfirmationDialogResult.Confirm };
        var viewModel = CreateMainWindowViewModel(
            nodeConnectionSessionService: sessionService,
            remoteRuntimeService: remoteRuntime,
            frpLifecycleStateService: lifecycle,
            confirmationDialogService: confirmation);

        var confirmed = await viewModel.ConfirmCloseAsync();

        Assert.True(confirmed);
        Assert.Equal(1, remoteRuntime.StopCallCount);
        Assert.Contains("kill 191", remoteRuntime.LastStopRequest?.Command, StringComparison.Ordinal);
        Assert.DoesNotContain("pkill", remoteRuntime.LastStopRequest?.Command, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmCloseAsync_StopRemoteFrps_WhenMultipleProcessesCannotBeMatched_ShouldReturnToApp()
    {
        var lifecycle = new FakeFrpLifecycleStateService();
        lifecycle.UpdateRemoteFrpsState("Web-Server-HK", isSshOnline: true, FrpNexusStatus.Running, "/etc/frp/frps.toml");
        var sessionService = new FakeNodeConnectionSessionService();
        await sessionService.ConnectAsync(
            CreateNode("Web-Server-HK", "/etc/frp/frps.toml"),
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
        var remoteRuntime = new FakeRemoteRuntimeService
        {
            Processes =
            [
                new("frps-191", "Web-Server-HK", "frps", FrpNexusStatus.Running, "191", "00:13", "-", "/opt/frp/frps"),
                new("frps-222", "Web-Server-HK", "frps", FrpNexusStatus.Running, "222", "00:10", "-", "/opt/frp/frps")
            ]
        };
        var confirmation = new FakeConfirmationDialogService { NextChoiceResult = ConfirmationDialogResult.Confirm, NextResult = false };
        var viewModel = CreateMainWindowViewModel(
            nodeConnectionSessionService: sessionService,
            remoteRuntimeService: remoteRuntime,
            frpLifecycleStateService: lifecycle,
            confirmationDialogService: confirmation);

        var confirmed = await viewModel.ConfirmCloseAsync();

        Assert.False(confirmed);
        Assert.Equal(0, remoteRuntime.StopCallCount);
        Assert.Contains("无法唯一匹配", confirmation.LastRequest?.Message, StringComparison.Ordinal);
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

    [Fact]
    public async Task Dispose_ShouldDisposePollingPageViewModels()
    {
        var viewModel = CreateMainWindowViewModel();
        var tunnelsPage = Assert.IsType<TunnelsPageViewModel>(
            viewModel.NavigationItems.Single(item => item.Icon == "tunnels").Page);

        Assert.True(tunnelsPage.IsLocalFrpcStatusPollingActiveForTest);

        viewModel.Dispose();
        await WaitUntilAsync(() => !tunnelsPage.IsLocalFrpcStatusPollingActiveForTest);

        Assert.False(tunnelsPage.IsLocalFrpcStatusPollingActiveForTest);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }
    }

    private static MainWindowViewModel CreateMainWindowViewModel(
        IModalOverlayService? modalOverlayService = null,
        IModalDialogHostService? modalDialogHostService = null,
        INodeManagementService? nodeManagementService = null,
        INodeConnectionSessionService? nodeConnectionSessionService = null,
        IRemoteRuntimeService? remoteRuntimeService = null,
        ILocalFrpcProcessService? localFrpcProcessService = null,
        IFrpLifecycleStateService? frpLifecycleStateService = null,
        IRemoteFrpsRetentionService? remoteFrpsRetentionService = null,
        IConfirmationDialogService? confirmationDialogService = null,
        ConfigurationsPageViewModel? configurationsPage = null,
        LogsPageViewModel? logsPage = null)
    {
        return new MainWindowViewModel(
            CreateDashboardPageViewModel(),
            CreateNodesPageViewModel(),
            CreateTunnelsPageViewModel(),
            configurationsPage ?? CreateConfigurationsPageViewModel(),
            CreateRuntimePageViewModel(),
            logsPage ?? CreateLogsPageViewModel(),
            CreateSettingsPageViewModel(),
            new FakeNavigationRequestService(),
            nodeConnectionSessionService ?? new FakeNodeConnectionSessionService(),
            nodeManagementService ?? new FakeNodeManagementService(),
            remoteRuntimeService ?? new FakeRemoteRuntimeService(),
            localFrpcProcessService ?? new FakeLocalFrpcProcessService(),
            frpLifecycleStateService ?? new FakeFrpLifecycleStateService(),
            remoteFrpsRetentionService ?? new FakeRemoteFrpsRetentionService(),
            confirmationDialogService ?? new FakeConfirmationDialogService(),
            modalOverlayService ?? new ModalOverlayService(),
            modalDialogHostService ?? new ModalDialogHostService());
    }

    private static DashboardPageViewModel CreateDashboardPageViewModel()
    {
        return new DashboardPageViewModel(
            new FakeNodeManagementService(),
            new FakeTunnelManagementService(),
            new FakeRuntimeRecordService(),
            new FakeDeploymentRecordService(),
            new FakeNodeConnectionSessionService(),
            new FakeLocalFrpcProcessService(),
            new FakeFrpLifecycleStateService(),
            new FakeNavigationRequestService(),
            new FakeLocalApplicationLogService(),
            new FakeRemoteLogService(),
            new FakeRemoteRuntimeService());
    }

    private static NodesPageViewModel CreateNodesPageViewModel()
    {
        return new NodesPageViewModel(
            new FakeNodeManagementService(),
            new FakeNodeConnectionSessionService(),
            new FakeRemoteRuntimeService(),
            new FakeRemoteFileTransferService(),
            new Arturia.FrpNexus.Application.Configuration.TomlConfigurationService(),
            new FakeFilePickerService(),
            new FakeRemoteDirectoryPickerService(),
            new Arturia.FrpNexus.Desktop.ViewModels.Nodes.NodeCredentialWorkflow(new FakeNodeCredentialSecretService()),
            new Arturia.FrpNexus.Desktop.ViewModels.Nodes.NodeRemoteFrpsWorkflow(new FakeRemoteRuntimeService()),
            new FakeDeploymentRecordService(),
            new FakeNodeConnectionWorkflowDialogService(),
            new FakeConfirmationDialogService(),
            new FakeFrpLifecycleStateService(),
            new FakeRemoteFrpsRetentionService());
    }

    private static TunnelsPageViewModel CreateTunnelsPageViewModel()
    {
        return new TunnelsPageViewModel(
            new FakeTunnelManagementService(),
            new FakeNodeManagementService(),
            new FakeLocalFrpcProcessService(),
            new FakeLocalFrpcConfigurationService(),
            new FakeRuntimeRecordService(),
            new FakeFilePickerService());
    }

    private static ConfigurationsPageViewModel CreateConfigurationsPageViewModel(INodeManagementService? nodeManagementService = null)
    {
        return new ConfigurationsPageViewModel(
            new TomlConfigurationService(),
            nodeManagementService ?? new FakeNodeManagementService(),
            new FakeTunnelManagementService(),
            new FakeClipboardService());
    }

    private static RuntimePageViewModel CreateRuntimePageViewModel()
    {
        return new RuntimePageViewModel(
            new FakeRuntimeRecordService(),
            new FakeDeploymentRecordService(),
            new FakeNodeManagementService(),
            new FakeRemoteRuntimeService());
    }

    private static LogsPageViewModel CreateLogsPageViewModel(
        ILocalApplicationLogService? localLogs = null,
        TimeSpan? autoRefreshInterval = null)
    {
        return new LogsPageViewModel(
            new FakeNodeManagementService(),
            new FakeRemoteLogService(),
            localLogs ?? new FakeLocalApplicationLogService(),
            new FakeNodeConnectionSessionService(),
            new FakeRemoteRuntimeService(),
            autoRefreshInterval);
    }

    private static SettingsPageViewModel CreateSettingsPageViewModel()
    {
        return new SettingsPageViewModel(
            new FakeSettingsService(),
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService(),
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService(),
            new FakeLocalFolderLauncherService(),
            new FakeLocalCacheMaintenanceService(),
            new FakeConfirmationDialogService(),
            new FakeLocalStoragePathSettingsService());
    }

    private static NodeProfile CreateNode(string name, string configPath)
    {
        return new NodeProfile(
            name,
            "203.0.113.10",
            22,
            "root",
            "Session",
            "Ubuntu 22.04 LTS",
            FrpNexusStatus.Online,
            FrpNexusStatus.Running,
            "v0.61.1",
            "-",
            configPath);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        private readonly FrpNexusSettingsSnapshot _settings = new(
            "GitHub Releases",
            @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\logs",
            @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\data\frpnexus.db",
            string.Empty);

        public Task<FrpNexusSettingsSnapshot> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_settings);
        }

        public Task SaveSettingsAsync(FrpNexusSettingsSnapshot settings, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNavigationRequestService : INavigationRequestService
    {
        public event EventHandler<string>? NavigationRequested;

        public void RequestNavigation(string pageKey)
        {
            NavigationRequested?.Invoke(this, pageKey);
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

        public Task UpdateLastConnectionAsync(
            string nodeName,
            DateTimeOffset connectedAt,
            CancellationToken cancellationToken = default)
        {
            var index = _nodes.FindIndex(node => node.Name == nodeName);
            if (index >= 0)
            {
                _nodes[index] = _nodes[index] with { LastConnectionTestedAt = connectedAt };
            }

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

    private sealed class CountingNodeManagementService : INodeManagementService
    {
        private readonly IReadOnlyList<NodeProfile> _nodes =
        [
            new("Preview-Node", "203.0.113.10", 22, "root", "Session", "Ubuntu 22.04 LTS", FrpNexusStatus.Online, FrpNexusStatus.Stopped, "-", "-", "/opt/frp/frps.toml")
        ];

        public int ListNodesCallCount { get; private set; }

        public Task<IReadOnlyList<NodeProfile>> ListNodesAsync(CancellationToken cancellationToken = default)
        {
            ListNodesCallCount++;
            return Task.FromResult(_nodes);
        }

        public Task<NodeProfile?> GetNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_nodes.FirstOrDefault(node => node.Name == nodeName));
        }

        public Task SaveNodeAsync(NodeProfile node, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateLastConnectionAsync(string nodeName, DateTimeOffset connectedAt, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateConnectionTestResultAsync(string nodeName, FrpNexusStatus status, DateTimeOffset testedAt, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNodeConnectionSessionService : INodeConnectionSessionService
    {
        private readonly Dictionary<string, (NodeConnectionSessionSnapshot Snapshot, SshCredentialReference Credential)> _sessions =
            new(StringComparer.OrdinalIgnoreCase);

        public Task<NodeConnectionSessionResult> ConnectAsync(
            NodeProfile node,
            SshCredentialReference credential,
            CancellationToken cancellationToken = default)
        {
            _sessions[node.Name] = (
                new NodeConnectionSessionSnapshot(
                    node.Name,
                    NodeConnectionSessionState.Online,
                    DateTimeOffset.UtcNow,
                    "SSH 节点连接成功。"),
                credential);

            return Task.FromResult(new NodeConnectionSessionResult(
                node.Name,
                NodeConnectionSessionState.Online,
                DateTimeOffset.UtcNow,
                "SSH 节点连接成功。"));
        }

        public Task<NodeConnectionSessionResult> DisconnectAsync(
            string nodeName,
            CancellationToken cancellationToken = default)
        {
            _sessions.Remove(nodeName);

            return Task.FromResult(new NodeConnectionSessionResult(
                nodeName,
                NodeConnectionSessionState.Disconnected,
                null,
                "SSH 节点连接已断开。"));
        }

        public NodeConnectionSessionSnapshot GetSessionStatus(string nodeName)
        {
            if (_sessions.TryGetValue(nodeName, out var session))
            {
                return session.Snapshot;
            }

            return new NodeConnectionSessionSnapshot(nodeName, NodeConnectionSessionState.Offline, null, "尚未连接。");
        }

        public SshCredentialReference? GetConnectedCredential(string nodeName)
        {
            return _sessions.TryGetValue(nodeName, out var session)
                ? session.Credential
                : null;
        }

        public IReadOnlyList<NodeConnectionSessionSnapshot> ListActiveSessions()
        {
            return _sessions.Values.Select(session => session.Snapshot).ToArray();
        }
    }

    private sealed class FakeRemoteFileTransferService : IRemoteFileTransferService
    {
        public Task<RemoteFilePresenceResult> CheckRemoteFilesAsync(
            RemoteFilePresenceRequest request,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<RemoteFilePresenceEntry> files = request.RemotePaths
                .Select(path => new RemoteFilePresenceEntry(path, true))
                .ToArray();
            return Task.FromResult(new RemoteFilePresenceResult(
                request.Node.Name,
                files,
                FrpNexusStatus.Ready,
                DateTimeOffset.UtcNow,
                "远程文件已就绪。"));
        }

        public Task<RemoteFileTransferResult> UploadFrpBinaryAsync(
            RemoteFileUploadRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RemoteFileTransferResult(
                request.Node.Name,
                request.RemotePath,
                FrpNexusStatus.Ready,
                DateTimeOffset.UtcNow,
                "FRP 核心上传成功。"));
        }

        public Task<RemoteFileTransferResult> UploadConfigurationAsync(
            RemoteConfigurationUploadRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RemoteFileTransferResult(
                request.Node.Name,
                request.RemotePath,
                FrpNexusStatus.Ready,
                DateTimeOffset.UtcNow,
                "TOML 配置上传成功。"));
        }

        public Task<RemoteFileDeleteResult> DeleteRemoteFilesAsync(
            RemoteFileDeleteRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RemoteFileDeleteResult(
                request.Node.Name,
                request.RemotePaths,
                [],
                FrpNexusStatus.Ready,
                DateTimeOffset.UtcNow,
                "远程文件已清理。"));
        }
    }

    private sealed class FakeFilePickerService : IFilePickerService
    {
        public Task<string?> PickFrpBinaryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> PickLocalFrpcBinaryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> PickLocalFrpcConfigPathAsync(
            string suggestedFileName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> PickFrpDownloadDirectoryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class FakeLocalFrpcConfigurationService : ILocalFrpcConfigurationService
    {
        public Task<LocalFrpcConfigurationSnapshot> GetConfigurationAsync(
            string nodeName,
            CancellationToken cancellationToken = default)
        {
            var configPath = GetDefaultNodeConfigPath(nodeName);
            return Task.FromResult(new LocalFrpcConfigurationSnapshot(
                string.Empty,
                configPath,
                configPath));
        }

        public Task SaveFrpcBinaryPathAsync(
            string frpcBinaryPath,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveNodeConfigPathAsync(
            string nodeName,
            string frpcConfigPath,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public string GetDefaultNodeConfigPath(string nodeName)
        {
            return Path.Combine(Path.GetTempPath(), "FrpNexusTests", $"{nodeName}.frpc.toml");
        }
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRemoteDirectoryPickerService : IRemoteDirectoryPickerService
    {
        public Task<string?> PickRemoteDirectoryAsync(
            NodeProfile node,
            SshCredentialReference credential,
            string initialDirectory,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class FakeNodeConnectionWorkflowDialogService : INodeConnectionWorkflowDialogService
    {
        public Task<NodeConnectionWorkflowResult> ShowAsync(
            NodeProfile node,
            NodeConnectionWorkflowOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new NodeConnectionWorkflowResult(node.Name, false, false, false));
        }
    }

    private sealed class FakeNodeCredentialSecretService : INodeCredentialSecretService
    {
        public Task<bool> HasSessionPasswordAsync(
            string nodeName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<string?> GetSessionPasswordAsync(
            string nodeName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public Task SaveSessionPasswordAsync(
            string nodeName,
            string password,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteSessionPasswordAsync(
            string nodeName,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLocalFolderLauncherService : ILocalFolderLauncherService
    {
        public Task OpenFolderAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLocalCacheMaintenanceService : ILocalCacheMaintenanceService
    {
        public Task<LocalCacheCleanupResult> ClearDefaultFrpReleaseCacheAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LocalCacheCleanupResult(0, 0, string.Empty));
        }
    }

    private sealed class FakeLocalStoragePathSettingsService : ILocalStoragePathSettingsService
    {
        public LocalStoragePathSettings GetSettings()
        {
            return new LocalStoragePathSettings(string.Empty, string.Empty);
        }

        public string GetLogDirectory()
        {
            return string.Empty;
        }

        public string GetSqliteDatabaseDirectory()
        {
            return string.Empty;
        }

        public string GetSqliteDatabasePath()
        {
            return string.Empty;
        }

        public Task SaveSettingsAsync(
            LocalStoragePathSettings pathSettings,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<SqliteDatabaseRelocationResult> PrepareSqliteDatabaseDirectoryAsync(
            string currentDatabasePath,
            string targetDatabaseDirectory,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SqliteDatabaseRelocationResult(
                currentDatabasePath,
                Path.Combine(targetDatabaseDirectory, "frpnexus.db"),
                false,
                false,
                null));
        }

        public Task<LogDirectoryRelocationResult> PrepareLogDirectoryAsync(
            string currentLogDirectory,
            string targetLogDirectory,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LogDirectoryRelocationResult(
                currentLogDirectory,
                targetLogDirectory,
                0,
                0));
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

        public Task ReplaceRuntimeProcessesForNodeAsync(
            string nodeName,
            IReadOnlyList<RuntimeProcess> processes,
            CancellationToken cancellationToken = default)
        {
            _processes.RemoveAll(process =>
                string.Equals(process.NodeName, nodeName, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(process.ProcessKind, "frpc", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(process.ProcessKind, "frps", StringComparison.OrdinalIgnoreCase)));
            _processes.AddRange(processes);
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

    private sealed class FakeRemoteRuntimeService : IRemoteRuntimeService
    {
        public IReadOnlyList<RuntimeProcess>? Processes { get; set; }

        public int GetProcessesRequestCount { get; private set; }

        public int StopCallCount { get; private set; }

        public RemoteRuntimeCommandRequest? LastStopRequest { get; private set; }

        public Task<IReadOnlyList<RuntimeProcess>> GetProcessesAsync(RemoteRuntimeQueryRequest request, CancellationToken cancellationToken = default)
        {
            GetProcessesRequestCount++;
            IReadOnlyList<RuntimeProcess> processes = Processes ??
            [
                new("frps-main", request.Node.Name, "frps", FrpNexusStatus.Running, "2048", "1h", "0.0.0.0:7000")
            ];

            return Task.FromResult(processes);
        }

        public Task<RemoteRuntimeCommandResult> StartAsync(RemoteRuntimeCommandRequest request, CancellationToken cancellationToken = default)
        {
            return CreateResult(request, FrpNexusStatus.Running, "远程启动命令执行完成。");
        }

        public Task<RemoteRuntimeCommandResult> StopAsync(RemoteRuntimeCommandRequest request, CancellationToken cancellationToken = default)
        {
            StopCallCount++;
            LastStopRequest = request;

            return CreateResult(request, FrpNexusStatus.Stopped, "远程停止命令执行完成。");
        }

        public Task<RemoteRuntimeCommandResult> RestartAsync(RemoteRuntimeCommandRequest request, CancellationToken cancellationToken = default)
        {
            return CreateResult(request, FrpNexusStatus.Running, "远程重启命令执行完成。");
        }

        private static Task<RemoteRuntimeCommandResult> CreateResult(RemoteRuntimeCommandRequest request, FrpNexusStatus status, string message)
        {
            return Task.FromResult(new RemoteRuntimeCommandResult(
                request.Node.Name,
                request.ProcessName,
                status,
                DateTimeOffset.UtcNow,
                message));
        }
    }

    private sealed class FakeFrpLifecycleStateService : IFrpLifecycleStateService
    {
        private readonly List<RemoteFrpsLifecycleSnapshot> _snapshots = [];

        public IReadOnlyList<RemoteFrpsLifecycleSnapshot> ListRemoteFrpsSnapshots()
        {
            return _snapshots.ToArray();
        }

        public void UpdateRemoteFrpsState(
            string nodeName,
            bool isSshOnline,
            FrpNexusStatus frpsStatus,
            string configPath = "")
        {
            _snapshots.RemoveAll(snapshot => string.Equals(snapshot.NodeName, nodeName, StringComparison.OrdinalIgnoreCase));
            _snapshots.Add(new RemoteFrpsLifecycleSnapshot(nodeName, isSshOnline, frpsStatus, configPath));
        }

        public void RemoveRemoteFrpsState(string nodeName)
        {
            _snapshots.RemoveAll(snapshot => string.Equals(snapshot.NodeName, nodeName, StringComparison.OrdinalIgnoreCase));
        }
    }

    private sealed class FakeLocalFrpcProcessService : ILocalFrpcProcessService
    {
        private readonly HashSet<string> _runningNodes = new(StringComparer.OrdinalIgnoreCase);

        public void MarkRunning(string nodeName)
        {
            _runningNodes.Add(nodeName);
        }

        public Task<LocalFrpcProcessResult> ApplyNodeTunnelsAsync(LocalFrpcProcessRequest request, CancellationToken cancellationToken = default)
        {
            _runningNodes.Add(request.Node.Name);
            return Task.FromResult(new LocalFrpcProcessResult(
                request.Node.Name,
                FrpNexusStatus.Running,
                DateTimeOffset.UtcNow,
                "本地 frpc 已按节点应用配置。"));
        }

        public Task<LocalFrpcProcessResult> StopNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            _runningNodes.Remove(nodeName);
            return Task.FromResult(new LocalFrpcProcessResult(
                nodeName,
                FrpNexusStatus.Stopped,
                DateTimeOffset.UtcNow,
                "该节点本地 frpc 已停止。"));
        }

        public LocalFrpcProcessSnapshot GetNodeStatus(string nodeName, string? expectedConfigPath = null)
        {
            return _runningNodes.Contains(nodeName)
                ? new LocalFrpcProcessSnapshot(nodeName, FrpNexusStatus.Running, "本地 frpc 正在运行。", 4321, expectedConfigPath)
                : new LocalFrpcProcessSnapshot(nodeName, FrpNexusStatus.Stopped, "本地 frpc 未运行。");
        }
        public IReadOnlyList<LocalFrpcProcessSnapshot> ListManagedSessions()
        {
            return _runningNodes
                .Select(nodeName => new LocalFrpcProcessSnapshot(
                    nodeName,
                    FrpNexusStatus.Running,
                    "本地 frpc 正在运行。",
                    4321,
                    $"{nodeName}.frpc.toml"))
                .ToArray();
        }
    }

    private sealed class FakeConfirmationDialogService : IConfirmationDialogService
    {
        public int ShowCount { get; private set; }

        public int ShowChoiceCount { get; private set; }

        public bool NextResult { get; set; } = true;

        public ConfirmationDialogResult NextChoiceResult { get; set; } = ConfirmationDialogResult.Confirm;

        public ConfirmationDialogRequest? LastRequest { get; private set; }

        public ConfirmationDialogChoiceRequest? LastChoiceRequest { get; private set; }

        public Task<bool> ShowAsync(
            ConfirmationDialogRequest request,
            CancellationToken cancellationToken = default)
        {
            ShowCount++;
            LastRequest = request;
            return Task.FromResult(NextResult);
        }

        public Task<ConfirmationDialogResult> ShowChoiceAsync(
            ConfirmationDialogChoiceRequest request,
            CancellationToken cancellationToken = default)
        {
            ShowChoiceCount++;
            LastChoiceRequest = request;
            return Task.FromResult(NextChoiceResult);
        }
    }

    private sealed class FakeRemoteFrpsRetentionService : IRemoteFrpsRetentionService
    {
        public List<RemoteFrpsRetentionRecord> Records { get; } = [];

        public Task<IReadOnlyList<RemoteFrpsRetentionRecord>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RemoteFrpsRetentionRecord>>(Records.ToArray());
        }

        public Task<RemoteFrpsRetentionRecord?> GetAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Records.FirstOrDefault(record =>
                string.Equals(record.NodeName, nodeName, StringComparison.OrdinalIgnoreCase)));
        }

        public Task SaveAsync(RemoteFrpsRetentionRecord record, CancellationToken cancellationToken = default)
        {
            Records.RemoveAll(candidate =>
                string.Equals(candidate.NodeName, record.NodeName, StringComparison.OrdinalIgnoreCase));
            Records.Add(record);
            return Task.CompletedTask;
        }

        public Task ClearAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            Records.RemoveAll(candidate =>
                string.Equals(candidate.NodeName, nodeName, StringComparison.OrdinalIgnoreCase));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLocalApplicationLogService(params LogEntry[] logs) : ILocalApplicationLogService
    {
        public int ReadCallCount { get; private set; }

        public string CurrentLogDirectory => @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\logs";

        public Task<IReadOnlyList<LogEntry>> ReadRecentLogsAsync(
            int lineCount = 200,
            CancellationToken cancellationToken = default)
        {
            ReadCallCount++;
            return Task.FromResult<IReadOnlyList<LogEntry>>(logs.Take(lineCount).ToArray());
        }
    }

    private sealed class FakeFrpReleaseService : IFrpReleaseService
    {
        public Task<IReadOnlyList<FrpReleaseVersion>> ListAvailableVersionsAsync(
            FrpReleaseSourceOptions? sourceOptions = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<FrpReleaseVersion>>([]);
        }

        public Task<FrpReleasePreparationResult> PrepareReleaseAsync(
            FrpReleasePreparationRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("MainWindow tests do not prepare FRP releases.");
        }
    }

    private sealed class FakeFrpCoreDownloadOptionsDialogService : IFrpCoreDownloadOptionsDialogService
    {
        public Task<FrpCoreDownloadOptions?> ShowAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<FrpCoreDownloadOptions?>(new FrpCoreDownloadOptions(
                "frpc",
                "windows_amd64"));
        }
    }

    private sealed class FakeRemoteLogService : IRemoteLogService
    {
        public Task<IReadOnlyList<LogEntry>> ReadRecentLogsAsync(RemoteLogReadRequest request, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<LogEntry> logs =
            [
                new("2026-06-04 12:00:00.000", "INFO", request.Node.Name, request.ProcessName, "remote log", FrpNexusStatus.Ready)
            ];

            return Task.FromResult(logs);
        }

        public async IAsyncEnumerable<LogEntry> StreamLogsAsync(RemoteLogReadRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var logs = await ReadRecentLogsAsync(request, cancellationToken);
            foreach (var log in logs)
            {
                yield return log;
            }
        }
    }
}
