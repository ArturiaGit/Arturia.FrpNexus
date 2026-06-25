using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.Services;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class LogsPageViewModelTests
{
    [Fact]
    public void Constructor_ShouldStartWithoutStaticSampleLogs()
    {
        var viewModel = CreateViewModel();

        Assert.Empty(viewModel.Logs);
        Assert.Empty(viewModel.VisibleLogs);
        Assert.Equal("点击刷新读取本地 FrpNexus 日志。", viewModel.StatusText);
        Assert.Equal("远程日志", viewModel.LogModeToggleText);
        Assert.Equal("读取远程 FRP 日志", viewModel.LogModeToggleTooltip);
        Assert.DoesNotContain(viewModel.NodeFilterOptions, item => item == "Web-Server-HK");
        Assert.DoesNotContain(viewModel.NodeFilterOptions, item => item == "DB-Node-SH");
    }

    [Fact]
    public async Task LoadLocalLogsCommand_ShouldLoadRealLocalLogEntriesAndRefreshFilters()
    {
        var localLogs = new FakeLocalApplicationLogService(
            new("2026-06-15 10:00:00.000", "WARN", "客户端", "FrpNexus", "本地警告", FrpNexusStatus.Warning),
            new("2026-06-15 10:00:01.000", "ERROR", "客户端", "FrpNexus", "本地错误", FrpNexusStatus.Error));
        var viewModel = CreateViewModel(localLogs: localLogs);

        await viewModel.LoadLocalLogsCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.Logs.Count);
        Assert.Equal(2, viewModel.VisibleLogs.Count);
        Assert.Contains("客户端", viewModel.NodeFilterOptions);
        Assert.DoesNotContain("本机", viewModel.NodeFilterOptions);
        Assert.Contains("FrpNexus", viewModel.ProcessFilterOptions);
        Assert.Equal("客户端", viewModel.SelectedNodeFilter);
        Assert.Equal("FrpNexus", viewModel.SelectedProcessFilter);
        Assert.Equal("Lines: 2", viewModel.LinesText);
        Assert.Contains(localLogs.CurrentLogDirectory, viewModel.LogFileText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/tmp/frpnexus-frpc.log", viewModel.LogFileText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("已读取 2 行本地日志。", viewModel.StatusText);
        Assert.Equal(1, localLogs.ReadCallCount);
    }

    [Fact]
    public async Task EmptyFilterSelection_ShouldBehaveAsAllFilters()
    {
        var viewModel = CreateViewModel(localLogs: new FakeLocalApplicationLogService(
            new("2026-06-15 10:00:00.000", "WARN", "客户端", "FrpNexus", "本地警告", FrpNexusStatus.Warning),
            new("2026-06-15 10:00:01.000", "ERROR", "客户端", "FrpNexus", "本地错误", FrpNexusStatus.Error)));
        await viewModel.LoadLocalLogsCommand.ExecuteAsync(null);

        viewModel.SelectedNodeFilter = string.Empty;
        viewModel.SelectedProcessFilter = string.Empty;

        Assert.Equal(2, viewModel.VisibleLogs.Count);
        Assert.Equal("Lines: 2", viewModel.LinesText);
        Assert.DoesNotContain("[Connected: ]", viewModel.TerminalConnectionText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadLocalLogsCommand_WhenNoLocalLogs_ShouldShowEmptyState()
    {
        var viewModel = CreateViewModel(localLogs: new FakeLocalApplicationLogService());

        await viewModel.LoadLocalLogsCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.Logs);
        Assert.Empty(viewModel.VisibleLogs);
        Assert.Contains("客户端", viewModel.NodeFilterOptions);
        Assert.Contains("FrpNexus", viewModel.ProcessFilterOptions);
        Assert.Equal("客户端", viewModel.SelectedNodeFilter);
        Assert.Equal("FrpNexus", viewModel.SelectedProcessFilter);
        Assert.Equal("Lines: 0", viewModel.LinesText);
        Assert.Equal("本地日志暂无警告或错误。", viewModel.StatusText);
    }

    [Fact]
    public async Task Filters_ShouldApplyToLoadedLocalLogs()
    {
        var viewModel = CreateViewModel(localLogs: new FakeLocalApplicationLogService(
            new("2026-06-15 10:00:00.000", "WARN", "客户端", "FrpNexus", "本地警告", FrpNexusStatus.Warning),
            new("2026-06-15 10:00:01.000", "ERROR", "客户端", "FrpNexus", "本地错误", FrpNexusStatus.Error)));
        await viewModel.LoadLocalLogsCommand.ExecuteAsync(null);

        viewModel.SelectedNodeFilter = "客户端";
        viewModel.SelectedProcessFilter = "FrpNexus";
        viewModel.SelectedLevelFilter = "ERROR";
        viewModel.SearchText = "错误";

        var log = Assert.Single(viewModel.VisibleLogs);
        Assert.Equal("ERROR", log.Level);
        Assert.Equal("本地错误", log.Message);
        Assert.Equal("[Connected: 客户端]", viewModel.TerminalConnectionText);
    }

    [Fact]
    public async Task ReadRemoteLogsCommand_ShouldLoadLogsAndClearSecret()
    {
        var remoteLogService = new FakeRemoteLogService();
        var viewModel = CreateViewModel(remoteLogService: remoteLogService);
        viewModel.SshSessionPassword = "SESSION_PASSWORD_PLACEHOLDER";
        viewModel.SelectedNodeName = "Web-Server-HK";
        viewModel.ProcessName = "frpc";
        viewModel.RemoteLogPath = "/tmp/frpnexus-frpc.log";

        await viewModel.ReadRemoteLogsCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Logs);
        Assert.Single(viewModel.VisibleLogs);
        Assert.Equal("已读取 1 行远程日志。", viewModel.StatusText);
        Assert.Equal(string.Empty, viewModel.SshSessionPassword);
        Assert.False(viewModel.IsRemoteCredentialsVisible);
        Assert.NotNull(remoteLogService.LastRequest);
        Assert.DoesNotContain("SESSION_PASSWORD_PLACEHOLDER", viewModel.Logs.Single().Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadRemoteLogsCommand_ShouldRejectMissingSessionPassword()
    {
        var remoteLogService = new FakeRemoteLogService();
        var viewModel = CreateViewModel(remoteLogService: remoteLogService);
        viewModel.SelectedNodeName = "Web-Server-HK";
        viewModel.ProcessName = "frpc";
        viewModel.RemoteLogPath = "/tmp/frpnexus-frpc.log";

        await viewModel.ReadRemoteLogsCommand.ExecuteAsync(null);

        Assert.Equal("请输入本次会话使用的 SSH 密码，密码不会保存到 SQLite。", viewModel.StatusText);
        Assert.Null(remoteLogService.LastRequest);
    }

    [Fact]
    public async Task ReadRemoteLogsCommand_ShouldReportFailureAndClearSecret()
    {
        var viewModel = CreateViewModel(remoteLogService: new FailingRemoteLogService());
        viewModel.SelectedNodeName = "Web-Server-HK";
        viewModel.SshSessionPassword = "SESSION_PASSWORD_PLACEHOLDER";

        await viewModel.ReadRemoteLogsCommand.ExecuteAsync(null);

        Assert.Equal("远程日志读取失败，请检查输入、网络或本地数据状态后重试。", viewModel.StatusText);
        Assert.Equal(string.Empty, viewModel.SshSessionPassword);
        Assert.False(viewModel.IsReadingRemoteLogs);
    }

    [Fact]
    public async Task ReadRemoteLogsCommand_ShouldUseSelectedNodeFilterForRemoteRequest()
    {
        var remoteLogService = new FakeRemoteLogService();
        var viewModel = CreateViewModel(remoteLogService: remoteLogService);
        viewModel.SelectedNodeFilter = "DB-Node-SH";
        viewModel.SshSessionPassword = "SESSION_PASSWORD_PLACEHOLDER";

        await viewModel.ReadRemoteLogsCommand.ExecuteAsync(null);

        Assert.NotNull(remoteLogService.LastRequest);
        Assert.Equal("DB-Node-SH", remoteLogService.LastRequest.Node.Name);
    }

    [Fact]
    public void ToggleRemoteCredentialsCommand_ShouldShowAndHideCredentialPanel()
    {
        var viewModel = CreateViewModel();

        viewModel.ToggleRemoteCredentialsCommand.Execute(null);

        Assert.True(viewModel.IsRemoteCredentialsVisible);

        viewModel.ToggleRemoteCredentialsCommand.Execute(null);

        Assert.False(viewModel.IsRemoteCredentialsVisible);
    }

    [Fact]
    public async Task ToggleRemoteCredentialsCommand_ShouldListOnlyActiveSessionNodes()
    {
        var viewModel = CreateViewModel(nodeConnectionSessionService: new FakeNodeConnectionSessionService(
        [
            new("Web-Server-HK", NodeConnectionSessionState.Online, DateTimeOffset.UtcNow, "online")
        ]));

        await viewModel.ToggleRemoteCredentialsCommand.ExecuteAsync(null);

        Assert.Contains("Web-Server-HK", viewModel.NodeFilterOptions);
        Assert.DoesNotContain("DB-Node-SH", viewModel.NodeFilterOptions);
        Assert.Equal("Web-Server-HK", viewModel.SelectedNodeFilter);
    }

    [Fact]
    public async Task SelectingRemoteNode_ShouldLoadOnlyThatNodeRuntimeProcesses()
    {
        var remoteRuntimeService = new FakeRemoteRuntimeService([
            new("frpc-2048", "Web-Server-HK", "frpc", FrpNexusStatus.Running, "2048", "1h", "-"),
            new("frps-4096", "Web-Server-HK", "frps", FrpNexusStatus.Running, "4096", "2h", "-")
        ]);
        var viewModel = CreateViewModel(
            remoteRuntimeService: remoteRuntimeService,
            nodeConnectionSessionService: new FakeNodeConnectionSessionService(
            [
                new("Web-Server-HK", NodeConnectionSessionState.Online, DateTimeOffset.UtcNow, "online")
            ]));

        await viewModel.ToggleRemoteCredentialsCommand.ExecuteAsync(null);

        Assert.Contains("frpc · PID 2048", viewModel.ProcessFilterOptions);
        Assert.Contains("frps · PID 4096", viewModel.ProcessFilterOptions);
        Assert.DoesNotContain("nexus_daemon", viewModel.ProcessFilterOptions);
        Assert.NotNull(remoteRuntimeService.LastQueryRequest);
        Assert.Equal("Web-Server-HK", remoteRuntimeService.LastQueryRequest.Node.Name);
    }

    [Fact]
    public async Task ToggleRemoteCredentialsCommand_WhenNoActiveNodes_ShouldShowEmptyPrompt()
    {
        var remoteRuntimeService = new FakeRemoteRuntimeService([
            new("frpc-2048", "Web-Server-HK", "frpc", FrpNexusStatus.Running, "2048", "1h", "-")
        ]);
        var viewModel = CreateViewModel(
            remoteRuntimeService: remoteRuntimeService,
            nodeConnectionSessionService: new FakeNodeConnectionSessionService([]));

        await viewModel.ToggleRemoteCredentialsCommand.ExecuteAsync(null);

        Assert.Equal("请先在节点页连接一个节点。", viewModel.StatusText);
        Assert.Equal(["全部节点"], viewModel.NodeFilterOptions);
        Assert.Equal(["全部进程"], viewModel.ProcessFilterOptions);
        Assert.Null(remoteRuntimeService.LastQueryRequest);
    }

    [Fact]
    public async Task ToggleRemoteCredentialsCommand_WhenNodeHasNoFrpProcesses_ShouldShowEmptyPrompt()
    {
        var viewModel = CreateViewModel(
            remoteRuntimeService: new FakeRemoteRuntimeService([]),
            nodeConnectionSessionService: new FakeNodeConnectionSessionService(
            [
                new("Web-Server-HK", NodeConnectionSessionState.Online, DateTimeOffset.UtcNow, "online")
            ]));

        await viewModel.ToggleRemoteCredentialsCommand.ExecuteAsync(null);

        Assert.Equal("未发现该节点正在运行的 frpc 或 frps 进程。", viewModel.StatusText);
        Assert.Equal(["全部进程"], viewModel.ProcessFilterOptions);
    }

    [Fact]
    public async Task ReadRemoteLogsCommand_ShouldReuseConnectedCredentialAndUseFrpsLogPath()
    {
        var remoteLogService = new FakeRemoteLogService();
        var viewModel = CreateViewModel(
            remoteLogService: remoteLogService,
            remoteRuntimeService: new FakeRemoteRuntimeService([
                new("frps-4096", "Web-Server-HK", "frps", FrpNexusStatus.Running, "4096", "2h", "-")
            ]),
            nodeConnectionSessionService: new FakeNodeConnectionSessionService(
            [
                new("Web-Server-HK", NodeConnectionSessionState.Online, DateTimeOffset.UtcNow, "online")
            ]));

        await viewModel.ToggleRemoteCredentialsCommand.ExecuteAsync(null);
        viewModel.SelectedProcessFilter = "frps · PID 4096";

        await viewModel.ReadRemoteLogsCommand.ExecuteAsync(null);

        Assert.NotNull(remoteLogService.LastRequest);
        Assert.Equal("Web-Server-HK", remoteLogService.LastRequest.Node.Name);
        Assert.Equal("frps", remoteLogService.LastRequest.ProcessName);
        Assert.Equal("/tmp/frpnexus-frps.log", remoteLogService.LastRequest.LogPath);
        Assert.Equal(string.Empty, viewModel.SshSessionPassword);
    }

    [Fact]
    public async Task ToggleRemoteCredentialsCommand_ShouldAutomaticallyReadDefaultRemoteProcessLogs()
    {
        var remoteLogService = new FakeRemoteLogService();
        var viewModel = CreateViewModel(
            remoteLogService: remoteLogService,
            remoteRuntimeService: new FakeRemoteRuntimeService([
                new("frpc-2048", "Web-Server-HK", "frpc", FrpNexusStatus.Running, "2048", "1h", "-")
            ]),
            nodeConnectionSessionService: new FakeNodeConnectionSessionService(
            [
                new("Web-Server-HK", NodeConnectionSessionState.Online, DateTimeOffset.UtcNow, "online")
            ]));

        await viewModel.ToggleRemoteCredentialsCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Logs);
        Assert.Equal("Web-Server-HK", viewModel.SelectedNodeFilter);
        Assert.Equal("frpc · PID 2048", viewModel.SelectedProcessFilter);
        Assert.Equal("本地日志", viewModel.LogModeToggleText);
        Assert.Equal("返回并读取本地 FrpNexus 日志", viewModel.LogModeToggleTooltip);
        Assert.NotNull(remoteLogService.LastRequest);
        Assert.Equal("Web-Server-HK", remoteLogService.LastRequest.Node.Name);
        Assert.Equal("frpc", remoteLogService.LastRequest.ProcessName);
        Assert.Equal("/tmp/frpnexus-frpc.log", remoteLogService.LastRequest.LogPath);
        Assert.Equal("已读取 1 行远程日志。", viewModel.StatusText);
    }

    [Fact]
    public async Task SelectedRemoteProcessFilterChanged_ShouldAutomaticallyReadSelectedProcessLogs()
    {
        var remoteLogService = new FakeRemoteLogService();
        var viewModel = CreateViewModel(
            remoteLogService: remoteLogService,
            remoteRuntimeService: new FakeRemoteRuntimeService([
                new("frpc-2048", "Web-Server-HK", "frpc", FrpNexusStatus.Running, "2048", "1h", "-"),
                new("frps-4096", "Web-Server-HK", "frps", FrpNexusStatus.Running, "4096", "2h", "-")
            ]),
            nodeConnectionSessionService: new FakeNodeConnectionSessionService(
            [
                new("Web-Server-HK", NodeConnectionSessionState.Online, DateTimeOffset.UtcNow, "online")
            ]));
        await viewModel.ToggleRemoteCredentialsCommand.ExecuteAsync(null);

        viewModel.SelectedProcessFilter = "frps · PID 4096";
        await Task.Delay(50);

        Assert.NotNull(remoteLogService.LastRequest);
        Assert.Equal("frps", remoteLogService.LastRequest.ProcessName);
        Assert.Equal("/tmp/frpnexus-frps.log", remoteLogService.LastRequest.LogPath);
        Assert.Equal(2, remoteLogService.ReadCallCount);
    }

    [Fact]
    public async Task RefreshLogsCommand_WhenRemoteMode_ShouldReadCurrentRemoteLogs()
    {
        var localLogs = new FakeLocalApplicationLogService(
            new LogEntry("2026-06-15 10:00:00.000", "WARN", "客户端", "FrpNexus", "本地警告", FrpNexusStatus.Warning));
        var remoteLogService = new FakeRemoteLogService();
        var viewModel = CreateViewModel(
            remoteLogService: remoteLogService,
            localLogs: localLogs,
            remoteRuntimeService: new FakeRemoteRuntimeService([
                new("frpc-2048", "Web-Server-HK", "frpc", FrpNexusStatus.Running, "2048", "1h", "-")
            ]),
            nodeConnectionSessionService: new FakeNodeConnectionSessionService(
            [
                new("Web-Server-HK", NodeConnectionSessionState.Online, DateTimeOffset.UtcNow, "online")
            ]));
        await viewModel.ToggleRemoteCredentialsCommand.ExecuteAsync(null);

        await viewModel.RefreshLogsCommand.ExecuteAsync(null);

        Assert.Equal(0, localLogs.ReadCallCount);
        Assert.Equal(2, remoteLogService.ReadCallCount);
        Assert.Equal("Web-Server-HK", viewModel.Logs.Single().NodeName);
    }

    [Fact]
    public async Task ToggleRemoteCredentialsCommand_WhenRemoteModeIsVisible_ShouldReturnToLocalLogs()
    {
        var localNodeName = CreateViewModel().SelectedNodeFilter;
        var localLogs = new FakeLocalApplicationLogService(
            new LogEntry("2026-06-15 10:00:00.000", "WARN", localNodeName, "FrpNexus", "local warning", FrpNexusStatus.Warning));
        var remoteLogService = new FakeRemoteLogService();
        var viewModel = CreateViewModel(
            remoteLogService: remoteLogService,
            localLogs: localLogs,
            remoteRuntimeService: new FakeRemoteRuntimeService([
                new("frpc-2048", "Web-Server-HK", "frpc", FrpNexusStatus.Running, "2048", "1h", "-")
            ]),
            nodeConnectionSessionService: new FakeNodeConnectionSessionService(
            [
                new("Web-Server-HK", NodeConnectionSessionState.Online, DateTimeOffset.UtcNow, "online")
            ]));
        await viewModel.LoadLocalLogsCommand.ExecuteAsync(null);
        await viewModel.ToggleRemoteCredentialsCommand.ExecuteAsync(null);

        await viewModel.ToggleRemoteCredentialsCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsRemoteCredentialsVisible);
        Assert.False(viewModel.CanReadRemoteLogs);
        Assert.Equal("远程日志", viewModel.LogModeToggleText);
        Assert.Equal("读取远程 FRP 日志", viewModel.LogModeToggleTooltip);
        Assert.Equal(2, localLogs.ReadCallCount);
        Assert.Equal(1, remoteLogService.ReadCallCount);
        Assert.Single(viewModel.Logs);
        Assert.Equal(localNodeName, viewModel.Logs.Single().NodeName);
        Assert.Equal("FrpNexus", viewModel.SelectedProcessFilter);
        Assert.Contains(localNodeName, viewModel.NodeFilterOptions);
        Assert.Contains("FrpNexus", viewModel.ProcessFilterOptions);
        Assert.Contains(localLogs.CurrentLogDirectory, viewModel.LogFileText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/tmp/frpnexus-frpc.log", viewModel.LogFileText, StringComparison.OrdinalIgnoreCase);

        await viewModel.RefreshLogsCommand.ExecuteAsync(null);

        Assert.Equal(3, localLogs.ReadCallCount);
        Assert.Equal(1, remoteLogService.ReadCallCount);
    }

    [Fact]
    public async Task ToggleRemoteCredentialsCommand_WhenReturningToEmptyLocalLogs_ShouldKeepLocalDefaultFilters()
    {
        var localNodeName = CreateViewModel().SelectedNodeFilter;
        var localLogs = new FakeLocalApplicationLogService();
        var viewModel = CreateViewModel(
            localLogs: localLogs,
            remoteRuntimeService: new FakeRemoteRuntimeService([
                new("frpc-2048", "Web-Server-HK", "frpc", FrpNexusStatus.Running, "2048", "1h", "-")
            ]),
            nodeConnectionSessionService: new FakeNodeConnectionSessionService(
            [
                new("Web-Server-HK", NodeConnectionSessionState.Online, DateTimeOffset.UtcNow, "online")
            ]));
        await viewModel.ToggleRemoteCredentialsCommand.ExecuteAsync(null);

        await viewModel.ToggleRemoteCredentialsCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsRemoteCredentialsVisible);
        Assert.Empty(viewModel.Logs);
        Assert.Empty(viewModel.VisibleLogs);
        Assert.Equal(localNodeName, viewModel.SelectedNodeFilter);
        Assert.Equal("FrpNexus", viewModel.SelectedProcessFilter);
        Assert.Contains(localNodeName, viewModel.NodeFilterOptions);
        Assert.Contains("FrpNexus", viewModel.ProcessFilterOptions);
        Assert.Contains(localLogs.CurrentLogDirectory, viewModel.LogFileText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshForNavigationAsync_WhenRemoteMode_ShouldNotReplaceRemoteLogsWithLocalLogs()
    {
        var localLogs = new FakeLocalApplicationLogService(
            new LogEntry("2026-06-15 10:00:00.000", "WARN", "客户端", "FrpNexus", "本地警告", FrpNexusStatus.Warning));
        var remoteLogService = new FakeRemoteLogService();
        var viewModel = CreateViewModel(
            remoteLogService: remoteLogService,
            localLogs: localLogs,
            remoteRuntimeService: new FakeRemoteRuntimeService([
                new("frpc-2048", "Web-Server-HK", "frpc", FrpNexusStatus.Running, "2048", "1h", "-")
            ]),
            nodeConnectionSessionService: new FakeNodeConnectionSessionService(
            [
                new("Web-Server-HK", NodeConnectionSessionState.Online, DateTimeOffset.UtcNow, "online")
            ]));
        await viewModel.ToggleRemoteCredentialsCommand.ExecuteAsync(null);

        await viewModel.RefreshForNavigationAsync();

        Assert.Equal(0, localLogs.ReadCallCount);
        Assert.Equal("Web-Server-HK", viewModel.Logs.Single().NodeName);
        Assert.Contains("/tmp/frpnexus-frpc.log", viewModel.LogFileText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OnActivatedAsync_WhenAutoRefreshEnabled_ShouldPeriodicallyRefreshLocalLogs()
    {
        var localLogs = new FakeLocalApplicationLogService(
            new LogEntry("2026-06-15 10:00:00.000", "WARN", "客户端", "FrpNexus", "本地警告", FrpNexusStatus.Warning));
        var viewModel = CreateViewModel(
            localLogs: localLogs,
            autoRefreshInterval: TimeSpan.FromMilliseconds(20));

        await viewModel.OnActivatedAsync();
        await Task.Delay(70);
        viewModel.OnDeactivated();

        Assert.True(localLogs.ReadCallCount >= 2);
        Assert.Single(viewModel.Logs);
        Assert.Equal("客户端", viewModel.Logs.Single().NodeName);
    }

    [Fact]
    public async Task OnActivatedAsync_WhenAutoRefreshDisabled_ShouldNotStartPeriodicRefresh()
    {
        var localLogs = new FakeLocalApplicationLogService(
            new LogEntry("2026-06-15 10:00:00.000", "WARN", "客户端", "FrpNexus", "本地警告", FrpNexusStatus.Warning));
        var viewModel = CreateViewModel(
            localLogs: localLogs,
            autoRefreshInterval: TimeSpan.FromMilliseconds(20));
        viewModel.IsAutoRefreshEnabled = false;

        await viewModel.OnActivatedAsync();
        await Task.Delay(70);
        viewModel.OnDeactivated();

        Assert.Equal(0, localLogs.ReadCallCount);
        Assert.Empty(viewModel.Logs);
    }

    [Fact]
    public async Task OnDeactivated_ShouldStopPeriodicRefresh()
    {
        var localLogs = new FakeLocalApplicationLogService(
            new LogEntry("2026-06-15 10:00:00.000", "WARN", "客户端", "FrpNexus", "本地警告", FrpNexusStatus.Warning));
        var viewModel = CreateViewModel(
            localLogs: localLogs,
            autoRefreshInterval: TimeSpan.FromMilliseconds(20));

        await viewModel.OnActivatedAsync();
        viewModel.OnDeactivated();
        var readCountAfterDeactivate = localLogs.ReadCallCount;
        await Task.Delay(70);

        Assert.Equal(readCountAfterDeactivate, localLogs.ReadCallCount);
    }

    [Fact]
    public async Task OnActivatedAsync_WhenRefreshIsStillRunning_ShouldNotReenterRefresh()
    {
        var localLogs = new SlowLocalApplicationLogService(
            TimeSpan.FromMilliseconds(80),
            new LogEntry("2026-06-15 10:00:00.000", "WARN", "客户端", "FrpNexus", "本地警告", FrpNexusStatus.Warning));
        var viewModel = CreateViewModel(
            localLogs: localLogs,
            autoRefreshInterval: TimeSpan.FromMilliseconds(10));

        await viewModel.OnActivatedAsync();
        await Task.Delay(120);
        viewModel.OnDeactivated();

        Assert.Equal(1, localLogs.MaxConcurrentReadCount);
        Assert.True(localLogs.ReadCallCount >= 2);
    }

    [Fact]
    public async Task OnActivatedAsync_WhenRemoteMode_ShouldPeriodicallyRefreshRemoteLogs()
    {
        var localLogs = new FakeLocalApplicationLogService(
            new LogEntry("2026-06-15 10:00:00.000", "WARN", "客户端", "FrpNexus", "本地警告", FrpNexusStatus.Warning));
        var remoteLogService = new FakeRemoteLogService();
        var viewModel = CreateViewModel(
            remoteLogService: remoteLogService,
            localLogs: localLogs,
            remoteRuntimeService: new FakeRemoteRuntimeService([
                new("frpc-2048", "Web-Server-HK", "frpc", FrpNexusStatus.Running, "2048", "1h", "-")
            ]),
            nodeConnectionSessionService: new FakeNodeConnectionSessionService(
            [
                new("Web-Server-HK", NodeConnectionSessionState.Online, DateTimeOffset.UtcNow, "online")
            ]),
            autoRefreshInterval: TimeSpan.FromMilliseconds(20));
        await viewModel.ToggleRemoteCredentialsCommand.ExecuteAsync(null);
        var remoteReadsBeforeActivation = remoteLogService.ReadCallCount;

        await viewModel.OnActivatedAsync();
        await Task.Delay(70);
        viewModel.OnDeactivated();

        Assert.True(remoteLogService.ReadCallCount > remoteReadsBeforeActivation);
        Assert.Equal(0, localLogs.ReadCallCount);
        Assert.Equal("Web-Server-HK", viewModel.Logs.Single().NodeName);
    }

    [Fact]
    public async Task AutoReadFailureForNewRemoteTarget_ShouldClearOldTargetLogs()
    {
        var remoteLogService = new FailingAfterFirstRemoteLogService();
        var viewModel = CreateViewModel(
            remoteLogService: remoteLogService,
            remoteRuntimeService: new FakeRemoteRuntimeService([
                new("frpc-2048", "Web-Server-HK", "frpc", FrpNexusStatus.Running, "2048", "1h", "-"),
                new("frps-4096", "Web-Server-HK", "frps", FrpNexusStatus.Running, "4096", "2h", "-")
            ]),
            nodeConnectionSessionService: new FakeNodeConnectionSessionService(
            [
                new("Web-Server-HK", NodeConnectionSessionState.Online, DateTimeOffset.UtcNow, "online")
            ]));
        await viewModel.ToggleRemoteCredentialsCommand.ExecuteAsync(null);
        Assert.Single(viewModel.Logs);

        viewModel.SelectedProcessFilter = "frps · PID 4096";
        await Task.Delay(50);

        Assert.Empty(viewModel.Logs);
        Assert.Empty(viewModel.VisibleLogs);
        Assert.Equal("远程日志读取失败，请检查输入、网络或本地数据状态后重试。", viewModel.StatusText);
    }

    private static LogsPageViewModel CreateViewModel(
        INodeManagementService? nodeManagementService = null,
        IRemoteLogService? remoteLogService = null,
        ILocalApplicationLogService? localLogs = null,
        INodeConnectionSessionService? nodeConnectionSessionService = null,
        IRemoteRuntimeService? remoteRuntimeService = null,
        TimeSpan? autoRefreshInterval = null)
    {
        return new LogsPageViewModel(
            nodeManagementService ?? new FakeNodeManagementService(),
            remoteLogService ?? new FakeRemoteLogService(),
            localLogs ?? new FakeLocalApplicationLogService(),
            nodeConnectionSessionService ?? new FakeNodeConnectionSessionService([]),
            remoteRuntimeService ?? new FakeRemoteRuntimeService([]),
            autoRefreshInterval);
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

    private sealed class SlowLocalApplicationLogService(TimeSpan readDelay, params LogEntry[] logs) : ILocalApplicationLogService
    {
        private int _activeReadCount;

        public int ReadCallCount { get; private set; }

        public int MaxConcurrentReadCount { get; private set; }

        public string CurrentLogDirectory => @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\logs";

        public async Task<IReadOnlyList<LogEntry>> ReadRecentLogsAsync(
            int lineCount = 200,
            CancellationToken cancellationToken = default)
        {
            ReadCallCount++;
            var activeReadCount = Interlocked.Increment(ref _activeReadCount);
            MaxConcurrentReadCount = Math.Max(MaxConcurrentReadCount, activeReadCount);
            try
            {
                await Task.Delay(readDelay, cancellationToken);
                return logs.Take(lineCount).ToArray();
            }
            finally
            {
                Interlocked.Decrement(ref _activeReadCount);
            }
        }
    }

    private sealed class FakeNodeManagementService : INodeManagementService
    {
        private readonly NodeProfile[] _nodes =
        [
            new(
                "Web-Server-HK",
                "203.0.113.10",
                22,
                "deploy",
                "会话密码",
                "Ubuntu 22.04 LTS",
                FrpNexusStatus.Online,
                FrpNexusStatus.Running,
                "v0.61.1",
                "-",
                "/etc/frp/frpc.toml"),
            new(
                "DB-Node-SH",
                "198.51.100.20",
                22,
                "deploy",
                "会话密码",
                "Debian 12",
                FrpNexusStatus.Online,
                FrpNexusStatus.Running,
                "v0.61.1",
                "-",
                "/etc/frp/frpc.toml")
        ];

        public Task<IReadOnlyList<NodeProfile>> ListNodesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<NodeProfile>>(_nodes);
        }

        public Task<NodeProfile?> GetNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_nodes.FirstOrDefault(node => string.Equals(nodeName, node.Name, StringComparison.OrdinalIgnoreCase)));
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

    private sealed class FakeRemoteLogService : IRemoteLogService
    {
        public RemoteLogReadRequest? LastRequest { get; private set; }

        public int ReadCallCount { get; private set; }

        public Task<IReadOnlyList<LogEntry>> ReadRecentLogsAsync(RemoteLogReadRequest request, CancellationToken cancellationToken = default)
        {
            ReadCallCount++;
            LastRequest = request;
            IReadOnlyList<LogEntry> logs =
            [
                new("2026-06-04 12:00:00.000", "INFO", request.Node.Name, request.ProcessName, "remote log line", FrpNexusStatus.Ready)
            ];

            return Task.FromResult(logs);
        }

        public async IAsyncEnumerable<LogEntry> StreamLogsAsync(
            RemoteLogReadRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var logs = await ReadRecentLogsAsync(request, cancellationToken);
            foreach (var log in logs)
            {
                yield return log;
            }
        }
    }

    private sealed class FakeNodeConnectionSessionService(
        IReadOnlyList<NodeConnectionSessionSnapshot> sessions) : INodeConnectionSessionService
    {
        private readonly SshCredentialReference _credential = new(
            SshAuthenticationMode.SessionPassword,
            null,
            "SESSION_PASSWORD_PLACEHOLDER",
            null);

        public Task<NodeConnectionSessionResult> ConnectAsync(
            NodeProfile node,
            SshCredentialReference credential,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new NodeConnectionSessionResult(
                node.Name,
                NodeConnectionSessionState.Online,
                DateTimeOffset.UtcNow,
                "online"));
        }

        public Task<NodeConnectionSessionResult> DisconnectAsync(
            string nodeName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new NodeConnectionSessionResult(
                nodeName,
                NodeConnectionSessionState.Disconnected,
                null,
                "disconnected"));
        }

        public NodeConnectionSessionSnapshot GetSessionStatus(string nodeName)
        {
            return sessions.FirstOrDefault(session => string.Equals(session.NodeName, nodeName, StringComparison.OrdinalIgnoreCase))
                ?? new NodeConnectionSessionSnapshot(nodeName, NodeConnectionSessionState.Offline, null, "offline");
        }

        public IReadOnlyList<NodeConnectionSessionSnapshot> ListActiveSessions()
        {
            return sessions
                .Where(session => session.State == NodeConnectionSessionState.Online)
                .ToArray();
        }

        public SshCredentialReference? GetConnectedCredential(string nodeName)
        {
            return GetSessionStatus(nodeName).State == NodeConnectionSessionState.Online
                ? _credential
                : null;
        }
    }

    private sealed class FakeRemoteRuntimeService(IReadOnlyList<RuntimeProcess> processes) : IRemoteRuntimeService
    {
        public RemoteRuntimeQueryRequest? LastQueryRequest { get; private set; }

        public Task<IReadOnlyList<RuntimeProcess>> GetProcessesAsync(
            RemoteRuntimeQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            LastQueryRequest = request;
            return Task.FromResult<IReadOnlyList<RuntimeProcess>>(
                processes
                    .Where(process => string.Equals(process.NodeName, request.Node.Name, StringComparison.OrdinalIgnoreCase))
                    .ToArray());
        }

        public Task<RemoteRuntimeCommandResult> StartAsync(
            RemoteRuntimeCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RemoteRuntimeCommandResult> StopAsync(
            RemoteRuntimeCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RemoteRuntimeCommandResult> RestartAsync(
            RemoteRuntimeCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FailingRemoteLogService : IRemoteLogService
    {
        public Task<IReadOnlyList<LogEntry>> ReadRecentLogsAsync(RemoteLogReadRequest request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("远程日志读取失败");
        }

        public async IAsyncEnumerable<LogEntry> StreamLogsAsync(
            RemoteLogReadRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw new InvalidOperationException("远程日志读取失败");
            #pragma warning disable CS0162
            yield break;
            #pragma warning restore CS0162
        }
    }

    private sealed class FailingAfterFirstRemoteLogService : IRemoteLogService
    {
        private int _readCallCount;

        public Task<IReadOnlyList<LogEntry>> ReadRecentLogsAsync(RemoteLogReadRequest request, CancellationToken cancellationToken = default)
        {
            _readCallCount++;
            if (_readCallCount > 1)
            {
                throw new InvalidOperationException("远程日志读取失败");
            }

            IReadOnlyList<LogEntry> logs =
            [
                new("2026-06-04 12:00:00.000", "INFO", request.Node.Name, request.ProcessName, "remote log line", FrpNexusStatus.Ready)
            ];

            return Task.FromResult(logs);
        }

        public async IAsyncEnumerable<LogEntry> StreamLogsAsync(
            RemoteLogReadRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var logs = await ReadRecentLogsAsync(request, cancellationToken);
            foreach (var log in logs)
            {
                yield return log;
            }
        }
    }
}
