using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.Services;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class NodesPageInlineConnectionTests
{
    [Fact]
    public async Task NodeRowConnectionAction_ShouldShowConnectForOfflineAndDisconnectForOnline()
    {
        var node = CreateNode();
        var onlineSessionService = new FakeNodeConnectionSessionService();
        await onlineSessionService.ConnectAsync(
            node,
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
        var offlineViewModel = CreateViewModel(new FakeNodeManagementService([node]), new FakeNodeConnectionSessionService());
        var onlineViewModel = CreateViewModel(new FakeNodeManagementService([node]), onlineSessionService);

        await offlineViewModel.LoadNodesAsync();
        await onlineViewModel.LoadNodesAsync();

        Assert.Equal("连接", offlineViewModel.NodeRows.Single().ConnectionActionText);
        Assert.Equal("断开", onlineViewModel.NodeRows.Single().ConnectionActionText);
    }

    [Fact]
    public async Task ToggleNodeConnectionCommand_ShouldOpenDialogWithoutSelectingRow()
    {
        var dialogService = new FakeNodeConnectionWorkflowDialogService();
        var viewModel = CreateViewModel(
            new FakeNodeManagementService([CreateNode()]),
            new FakeNodeConnectionSessionService(),
            dialogService);
        await viewModel.LoadNodesAsync();

        await viewModel.ToggleNodeConnectionCommand.ExecuteAsync(viewModel.NodeRows.Single());

        Assert.Equal("行内测试节点", dialogService.LastNode?.Name);
        Assert.Null(viewModel.SelectedNode);
        Assert.False(viewModel.NodeRows.Single().IsSelected);
    }

    [Fact]
    public async Task ToggleNodeConnectionCommand_ShouldRefreshRemoteFrpsRuntimeAfterRowConnectionWithoutSelectingRow()
    {
        var node = CreateNode();
        var sessionService = new FakeNodeConnectionSessionService();
        var runtimeService = new FakeRemoteRuntimeService
        {
            Processes =
            [
                new RuntimeProcess(
                    "frps-1920916",
                    node.Name,
                    "frps",
                    FrpNexusStatus.Running,
                    "1920916",
                    "00:08",
                    "-",
                    "/opt/frp/frps -c /opt/frp/frps.toml")
            ]
        };
        var dialogService = new FakeNodeConnectionWorkflowDialogService
        {
            Result = new NodeConnectionWorkflowResult(node.Name, true, true, false),
            OnShowAsync = async shownNode =>
            {
                await sessionService.ConnectAsync(
                    shownNode,
                    new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
            }
        };
        var viewModel = CreateViewModel(
            new FakeNodeManagementService([node]),
            sessionService,
            dialogService,
            remoteRuntimeService: runtimeService);
        await viewModel.LoadNodesAsync();

        await viewModel.ToggleNodeConnectionCommand.ExecuteAsync(viewModel.NodeRows.Single());

        Assert.Null(viewModel.SelectedNode);
        Assert.False(viewModel.NodeRows.Single().IsSelected);
        Assert.Equal("运行中", viewModel.NodeRows.Single().FrpServiceText);
        Assert.Equal(1, runtimeService.GetProcessesRequestCount);
    }

    [Fact]
    public async Task ToggleNodeConnectionCommand_ReadyDeploymentResult_ShouldReusePresenceWhenSelectingNode()
    {
        var node = CreateNode();
        var sessionService = new FakeNodeConnectionSessionService();
        var runtimeService = new FakeRemoteRuntimeService
        {
            Processes =
            [
                new RuntimeProcess(
                    "frps-1920916",
                    node.Name,
                    "frps",
                    FrpNexusStatus.Running,
                    "1920916",
                    "00:08",
                    "-",
                    "/opt/frp/frps -c /opt/frp/frps.toml")
            ]
        };
        var fileTransferService = new FakeRemoteFileTransferService();
        var dialogService = new FakeNodeConnectionWorkflowDialogService
        {
            Result = new NodeConnectionWorkflowResult(node.Name, true, true, false, true),
            OnShowAsync = async shownNode =>
            {
                await sessionService.ConnectAsync(
                    shownNode,
                    new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
            }
        };
        var viewModel = CreateViewModel(
            new FakeNodeManagementService([node]),
            sessionService,
            dialogService,
            fileTransferService,
            runtimeService);
        await viewModel.LoadNodesAsync();

        await viewModel.ToggleNodeConnectionCommand.ExecuteAsync(viewModel.NodeRows.Single());
        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single());
        await Task.Delay(50);

        Assert.Equal("success", viewModel.CoreUploadSeverity);
        Assert.Equal(0, fileTransferService.PresenceRequestCount);
        Assert.Equal("frps 运行中", viewModel.RemoteFrpsStatusTitle);
        Assert.Equal("success", viewModel.RemoteFrpsSeverity);
    }

    [Fact]
    public async Task ToggleNodeConnectionCommand_MissingDeploymentResult_ShouldReusePresenceWhenSelectingNode()
    {
        var node = CreateNode();
        var sessionService = new FakeNodeConnectionSessionService();
        var fileTransferService = new FakeRemoteFileTransferService();
        var dialogService = new FakeNodeConnectionWorkflowDialogService
        {
            Result = new NodeConnectionWorkflowResult(node.Name, true, false, false, true),
            OnShowAsync = async shownNode =>
            {
                await sessionService.ConnectAsync(
                    shownNode,
                    new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
            }
        };
        var viewModel = CreateViewModel(
            new FakeNodeManagementService([node]),
            sessionService,
            dialogService,
            fileTransferService);
        await viewModel.LoadNodesAsync();

        await viewModel.ToggleNodeConnectionCommand.ExecuteAsync(viewModel.NodeRows.Single());
        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single());
        await Task.Delay(50);

        Assert.Equal("warning", viewModel.CoreUploadSeverity);
        Assert.Equal(0, fileTransferService.PresenceRequestCount);
    }

    [Fact]
    public async Task ToggleNodeConnectionCommand_RuntimeRefreshFailure_ShouldKeepSshOnlineAndShowFrpsWarning()
    {
        var node = CreateNode();
        var sessionService = new FakeNodeConnectionSessionService();
        var runtimeService = new FakeRemoteRuntimeService
        {
            GetProcessesException = new InvalidOperationException("runtime unavailable")
        };
        var dialogService = new FakeNodeConnectionWorkflowDialogService
        {
            Result = new NodeConnectionWorkflowResult(node.Name, true, true, false),
            OnShowAsync = async shownNode =>
            {
                await sessionService.ConnectAsync(
                    shownNode,
                    new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
            }
        };
        var viewModel = CreateViewModel(
            new FakeNodeManagementService([node]),
            sessionService,
            dialogService,
            remoteRuntimeService: runtimeService);
        await viewModel.LoadNodesAsync();

        await viewModel.ToggleNodeConnectionCommand.ExecuteAsync(viewModel.NodeRows.Single());

        var row = viewModel.NodeRows.Single();
        Assert.Equal("在线", row.ConnectionStatusText);
        Assert.Equal("警告", row.FrpServiceText);
        Assert.Equal(1, runtimeService.GetProcessesRequestCount);
    }

    [Fact]
    public async Task ToggleNodeConnectionCommand_MultipleRemoteFrpsWithoutConfigMatch_ShouldNotAutoAttach()
    {
        var node = CreateNode();
        var sessionService = new FakeNodeConnectionSessionService();
        var runtimeService = new FakeRemoteRuntimeService
        {
            Processes =
            [
                new RuntimeProcess("frps-1", node.Name, "frps", FrpNexusStatus.Running, "1", "00:01", "-", "/opt/frp/frps -c /etc/frp/a.toml"),
                new RuntimeProcess("frps-2", node.Name, "frps", FrpNexusStatus.Running, "2", "00:02", "-", "/opt/frp/frps -c /etc/frp/b.toml")
            ]
        };
        var dialogService = new FakeNodeConnectionWorkflowDialogService
        {
            Result = new NodeConnectionWorkflowResult(node.Name, true, true, false),
            OnShowAsync = async shownNode =>
            {
                await sessionService.ConnectAsync(
                    shownNode,
                    new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
            }
        };
        var viewModel = CreateViewModel(
            new FakeNodeManagementService([node]),
            sessionService,
            dialogService,
            remoteRuntimeService: runtimeService);
        await viewModel.LoadNodesAsync();

        await viewModel.ToggleNodeConnectionCommand.ExecuteAsync(viewModel.NodeRows.Single());

        Assert.Equal("警告", viewModel.NodeRows.Single().FrpServiceText);
        Assert.Equal(1, runtimeService.GetProcessesRequestCount);
    }

    [Fact]
    public async Task ToggleNodeConnectionCommand_ShouldDisconnectOnlineNode()
    {
        var node = CreateNode();
        var sessionService = new FakeNodeConnectionSessionService();
        await sessionService.ConnectAsync(
            node,
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
        var viewModel = CreateViewModel(new FakeNodeManagementService([node]), sessionService);
        await viewModel.LoadNodesAsync();

        await viewModel.ToggleNodeConnectionCommand.ExecuteAsync(viewModel.NodeRows.Single());

        Assert.Equal("连接", viewModel.NodeRows.Single().ConnectionActionText);
        Assert.True(viewModel.NodeRows.Single().IsConnectionNeutral);
    }

    [Fact]
    public async Task DeploymentStatus_ShouldStayHiddenBeforeSshConnection()
    {
        var node = CreateNode();
        var viewModel = CreateViewModel(new FakeNodeManagementService([node]), new FakeNodeConnectionSessionService());
        await viewModel.LoadNodesAsync();

        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single());

        Assert.False(viewModel.IsDeploymentStatusVisible);
        Assert.Equal("连接后检查", viewModel.SelectedDeploymentSummaryText);
        Assert.Equal("连接后检查", viewModel.CoreUploadStatusTitle);
    }

    [Fact]
    public async Task SelectingOnlineNode_ShouldAutoCheckDeploymentPresenceOnlyOnce()
    {
        var node = CreateNode();
        var otherNode = CreateNode() with { Name = "其它节点", Host = "203.0.113.71" };
        var sessionService = new FakeNodeConnectionSessionService();
        await sessionService.ConnectAsync(
            node,
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
        var fileTransferService = new FakeRemoteFileTransferService();
        var viewModel = CreateViewModel(new FakeNodeManagementService([node, otherNode]), sessionService, remoteFileTransferService: fileTransferService);
        await viewModel.LoadNodesAsync();

        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single(row => row.Name == node.Name));
        await WaitUntilAsync(() => fileTransferService.PresenceRequestCount == 1);
        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single(row => row.Name == otherNode.Name));
        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single(row => row.Name == node.Name));
        await Task.Delay(50);

        Assert.True(viewModel.IsDeploymentStatusVisible);
        Assert.Equal("部署文件已就绪", viewModel.CoreUploadStatusTitle);
        Assert.Equal(1, fileTransferService.PresenceRequestCount);
    }

    [Fact]
    public async Task RefreshDeploymentPresenceCommand_ShouldForceCheckAfterAutoCheck()
    {
        var node = CreateNode();
        var sessionService = new FakeNodeConnectionSessionService();
        await sessionService.ConnectAsync(
            node,
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
        var fileTransferService = new FakeRemoteFileTransferService();
        var viewModel = CreateViewModel(new FakeNodeManagementService([node]), sessionService, remoteFileTransferService: fileTransferService);
        await viewModel.LoadNodesAsync();

        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single());
        await WaitUntilAsync(() => fileTransferService.PresenceRequestCount == 1);
        await viewModel.RefreshDeploymentPresenceCommand.ExecuteAsync(null);

        Assert.True(viewModel.CanRefreshDeploymentPresence);
        Assert.Equal(2, fileTransferService.PresenceRequestCount);
    }

    [Fact]
    public async Task DisconnectingNode_ShouldClearDeploymentPresenceCacheForNextOnlineSession()
    {
        var node = CreateNode();
        var otherNode = CreateNode() with { Name = "其它节点", Host = "203.0.113.71" };
        var sessionService = new FakeNodeConnectionSessionService();
        await sessionService.ConnectAsync(
            node,
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
        var fileTransferService = new FakeRemoteFileTransferService();
        var viewModel = CreateViewModel(new FakeNodeManagementService([node, otherNode]), sessionService, remoteFileTransferService: fileTransferService);
        await viewModel.LoadNodesAsync();

        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single(row => row.Name == node.Name));
        await WaitUntilAsync(() => fileTransferService.PresenceRequestCount == 1);
        await viewModel.ToggleNodeConnectionCommand.ExecuteAsync(viewModel.NodeRows.Single(row => row.Name == node.Name));
        await sessionService.ConnectAsync(
            node,
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single(row => row.Name == otherNode.Name));
        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single(row => row.Name == node.Name));
        await WaitUntilAsync(() => fileTransferService.PresenceRequestCount == 2);

        Assert.Equal(2, fileTransferService.PresenceRequestCount);
    }

    [Fact]
    public async Task RefreshDeploymentPresenceCommand_ShouldUseRemoteFilePresenceForReadyState()
    {
        var node = CreateNode();
        var sessionService = new FakeNodeConnectionSessionService();
        await sessionService.ConnectAsync(
            node,
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
        var fileTransferService = new FakeRemoteFileTransferService();
        var viewModel = CreateViewModel(new FakeNodeManagementService([node]), sessionService, remoteFileTransferService: fileTransferService);
        await viewModel.LoadNodesAsync();
        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single());

        await viewModel.RefreshDeploymentPresenceCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsDeploymentStatusVisible);
        Assert.Equal("部署文件已就绪", viewModel.CoreUploadStatusTitle);
        Assert.Equal("success", viewModel.CoreUploadSeverity);
        Assert.Equal(["/opt/frp/frps", "/opt/frp/frps.toml"], fileTransferService.LastPresenceRequest?.RemotePaths);
    }

    [Fact]
    public async Task QuickRemoteFrpsActions_ShouldRequireOnlineSessionAndReadyDeployment()
    {
        var node = CreateNode();
        var offlineViewModel = CreateViewModel(
            new FakeNodeManagementService([node]),
            new FakeNodeConnectionSessionService());
        await offlineViewModel.LoadNodesAsync();
        offlineViewModel.SelectNodeCommand.Execute(offlineViewModel.NodeRows.Single());

        Assert.False(offlineViewModel.CanRunRemoteFrpsCommand);

        var sessionService = new FakeNodeConnectionSessionService();
        await sessionService.ConnectAsync(
            node,
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
        var missingFileTransferService = new FakeRemoteFileTransferService(["/opt/frp/frps"]);
        var missingViewModel = CreateViewModel(
            new FakeNodeManagementService([node]),
            sessionService,
            remoteFileTransferService: missingFileTransferService);
        await missingViewModel.LoadNodesAsync();
        missingViewModel.SelectNodeCommand.Execute(missingViewModel.NodeRows.Single());
        await missingViewModel.RefreshDeploymentPresenceCommand.ExecuteAsync(null);

        Assert.Equal("在线", missingViewModel.SelectedSshConnectionText);
        Assert.False(missingViewModel.CanRunRemoteFrpsCommand);

        missingFileTransferService.ExistingPaths = ["/opt/frp/frps", "/opt/frp/frps.toml"];
        await missingViewModel.RefreshDeploymentPresenceCommand.ExecuteAsync(null);

        Assert.Equal("部署文件已就绪", missingViewModel.CoreUploadStatusTitle);
        Assert.True(missingViewModel.CanRunRemoteFrpsCommand);
    }

    [Fact]
    public async Task DisconnectingSelectedOnlineNode_ShouldRefreshDetailsAndDisableQuickActions()
    {
        var node = CreateNode();
        var sessionService = new FakeNodeConnectionSessionService();
        await sessionService.ConnectAsync(
            node,
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
        var viewModel = CreateViewModel(
            new FakeNodeManagementService([node]),
            sessionService,
            remoteFileTransferService: new FakeRemoteFileTransferService());
        await viewModel.LoadNodesAsync();
        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single());
        await viewModel.RefreshDeploymentPresenceCommand.ExecuteAsync(null);

        Assert.Equal("在线", viewModel.SelectedSshConnectionText);
        Assert.True(viewModel.CanRunRemoteFrpsCommand);

        await viewModel.ToggleNodeConnectionCommand.ExecuteAsync(viewModel.NodeRows.Single());

        Assert.Equal("离线", viewModel.SelectedSshConnectionText);
        Assert.Equal("连接后检查", viewModel.SelectedDeploymentSummaryText);
        Assert.False(viewModel.CanRunRemoteFrpsCommand);
    }

    [Fact]
    public async Task RefreshDeploymentPresenceCommand_ShouldResolveRemoteDirectoryFromSelectedNodeConfigPath()
    {
        var node = CreateNode() with { ConfigPath = "/etc/frp/frps.toml" };
        var sessionService = new FakeNodeConnectionSessionService();
        await sessionService.ConnectAsync(
            node,
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
        var fileTransferService = new FakeRemoteFileTransferService();
        var viewModel = CreateViewModel(new FakeNodeManagementService([node]), sessionService, remoteFileTransferService: fileTransferService);
        await viewModel.LoadNodesAsync();
        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single());

        await viewModel.RefreshDeploymentPresenceCommand.ExecuteAsync(null);

        Assert.Equal("/etc/frp", viewModel.RemoteCoreDirectory);
        Assert.Equal(["/etc/frp/frps", "/etc/frp/frps.toml"], fileTransferService.LastPresenceRequest?.RemotePaths);
    }

    [Fact]
    public async Task RefreshDeploymentPresenceCommand_ShouldShowMissingRemoteFiles()
    {
        var node = CreateNode();
        var sessionService = new FakeNodeConnectionSessionService();
        await sessionService.ConnectAsync(
            node,
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
        var fileTransferService = new FakeRemoteFileTransferService(["/opt/frp/frps"]);
        var viewModel = CreateViewModel(new FakeNodeManagementService([node]), sessionService, remoteFileTransferService: fileTransferService);
        await viewModel.LoadNodesAsync();
        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single());

        await viewModel.RefreshDeploymentPresenceCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsDeploymentStatusVisible);
        Assert.Equal("需要部署准备", viewModel.CoreUploadStatusTitle);
        Assert.Equal("warning", viewModel.CoreUploadSeverity);
        Assert.Contains("frps.toml", viewModel.CoreUploadStatusText);
    }

    [Fact]
    public async Task DeploymentWorkflowCommand_ShouldBeAvailableOnlyForMissingDeploymentFiles()
    {
        var node = CreateNode();
        var sessionService = new FakeNodeConnectionSessionService();
        await sessionService.ConnectAsync(
            node,
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
        var fileTransferService = new FakeRemoteFileTransferService(["/opt/frp/frps"]);
        var viewModel = CreateViewModel(new FakeNodeManagementService([node]), sessionService, remoteFileTransferService: fileTransferService);
        await viewModel.LoadNodesAsync();
        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single());

        await viewModel.RefreshDeploymentPresenceCommand.ExecuteAsync(null);

        Assert.True(viewModel.CanOpenDeploymentWorkflow);

        fileTransferService.ExistingPaths = ["/opt/frp/frps", "/opt/frp/frps.toml"];
        await viewModel.RefreshDeploymentPresenceCommand.ExecuteAsync(null);

        Assert.False(viewModel.CanOpenDeploymentWorkflow);
    }

    [Fact]
    public async Task OpenDeploymentWorkflowCommand_ShouldOpenDialogAndSyncReadyStateWithoutDuplicateCheck()
    {
        var node = CreateNode();
        var sessionService = new FakeNodeConnectionSessionService();
        await sessionService.ConnectAsync(
            node,
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
        var fileTransferService = new FakeRemoteFileTransferService(["/opt/frp/frps"]);
        var dialogService = new FakeNodeConnectionWorkflowDialogService
        {
            Result = new NodeConnectionWorkflowResult(node.Name, true, true, true)
        };
        var viewModel = CreateViewModel(
            new FakeNodeManagementService([node]),
            sessionService,
            dialogService,
            fileTransferService);
        await viewModel.LoadNodesAsync();
        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single());
        await viewModel.RefreshDeploymentPresenceCommand.ExecuteAsync(null);

        var presenceChecksBeforeDialog = fileTransferService.PresenceRequestCount;
        await viewModel.OpenDeploymentWorkflowCommand.ExecuteAsync(null);

        Assert.Equal(node.Name, dialogService.LastNode?.Name);
        Assert.True(dialogService.LastOptions?.SkipInitialDeploymentPresenceCheck);
        Assert.Equal("部署文件已就绪", viewModel.CoreUploadStatusTitle);
        Assert.Equal("success", viewModel.CoreUploadSeverity);
        Assert.Equal("在线", viewModel.SelectedSshConnectionText);
        Assert.True(viewModel.CanRunRemoteFrpsCommand);
        Assert.Equal(presenceChecksBeforeDialog, fileTransferService.PresenceRequestCount);
    }

    [Fact]
    public async Task OpenDeploymentWorkflowCommand_ShouldSyncMissingStateFromDialogWithoutDuplicateCheck()
    {
        var node = CreateNode();
        var sessionService = new FakeNodeConnectionSessionService();
        await sessionService.ConnectAsync(
            node,
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
        var fileTransferService = new FakeRemoteFileTransferService(["/opt/frp/frps"]);
        var dialogService = new FakeNodeConnectionWorkflowDialogService
        {
            Result = new NodeConnectionWorkflowResult(node.Name, true, false, false, true)
        };
        var viewModel = CreateViewModel(
            new FakeNodeManagementService([node]),
            sessionService,
            dialogService,
            fileTransferService);
        await viewModel.LoadNodesAsync();
        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single());
        await viewModel.RefreshDeploymentPresenceCommand.ExecuteAsync(null);
        var presenceChecksBeforeDialog = fileTransferService.PresenceRequestCount;

        await viewModel.OpenDeploymentWorkflowCommand.ExecuteAsync(null);

        Assert.Equal(node.Name, dialogService.LastNode?.Name);
        Assert.True(dialogService.LastOptions?.SkipInitialDeploymentPresenceCheck);
        Assert.Equal("需要部署准备", viewModel.CoreUploadStatusTitle);
        Assert.Equal("warning", viewModel.CoreUploadSeverity);
        Assert.Equal(presenceChecksBeforeDialog, fileTransferService.PresenceRequestCount);
    }

    [Fact]
    public async Task OpenDeploymentWorkflowCommand_ShouldPreventStaleAutoCheckFromOverwritingDialogResult()
    {
        var node = CreateNode();
        var sessionService = new FakeNodeConnectionSessionService();
        await sessionService.ConnectAsync(
            node,
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
        var fileTransferService = new DelayedRemoteFileTransferService(["/opt/frp/frps"]);
        var dialogService = new FakeNodeConnectionWorkflowDialogService
        {
            Result = new NodeConnectionWorkflowResult(node.Name, true, true, false, true)
        };
        var viewModel = CreateViewModel(
            new FakeNodeManagementService([node]),
            sessionService,
            dialogService,
            fileTransferService);
        await viewModel.LoadNodesAsync();

        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single());
        await WaitUntilAsync(() => fileTransferService.PresenceRequestCount == 1);
        await viewModel.OpenDeploymentWorkflowCommand.ExecuteAsync(null);

        Assert.Equal("部署文件已就绪", viewModel.CoreUploadStatusTitle);
        fileTransferService.ReleasePendingChecks();
        await WaitUntilAsync(() => !viewModel.IsDeploymentPresenceChecking);

        Assert.Equal("部署文件已就绪", viewModel.CoreUploadStatusTitle);
        Assert.Equal("success", viewModel.CoreUploadSeverity);
        Assert.Equal(1, fileTransferService.PresenceRequestCount);
    }

    [Fact]
    public async Task RefreshDeploymentPresenceCommand_ShouldShowSafeErrorWhenPresenceCheckFails()
    {
        var node = CreateNode();
        var sessionService = new FakeNodeConnectionSessionService();
        await sessionService.ConnectAsync(
            node,
            new SshCredentialReference(
                SshAuthenticationMode.SessionPassword,
                SessionPassword: "SECRET_PASSWORD",
                PrivateKeyPassphrase: "SECRET_PASSPHRASE"));
        var fileTransferService = new FakeRemoteFileTransferService(status: FrpNexusStatus.Error, message: "SFTP 权限不足。");
        var viewModel = CreateViewModel(new FakeNodeManagementService([node]), sessionService, remoteFileTransferService: fileTransferService);
        await viewModel.LoadNodesAsync();
        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single());

        await viewModel.RefreshDeploymentPresenceCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsDeploymentStatusVisible);
        Assert.Equal("部署检查失败", viewModel.CoreUploadStatusTitle);
        Assert.Equal("error", viewModel.CoreUploadSeverity);
        Assert.DoesNotContain("SECRET_PASSWORD", viewModel.CoreUploadStatusText);
        Assert.DoesNotContain("SECRET_PASSPHRASE", viewModel.CoreUploadStatusText);
    }

    [Fact]
    public async Task SelectedFrpProcessText_ShouldUseOnlineSessionInsteadOfStoredConnectionStatus()
    {
        var node = CreateNode() with
        {
            ConnectionStatus = FrpNexusStatus.Offline,
            FrpStatus = FrpNexusStatus.Running,
            Uptime = "00:13"
        };
        var sessionService = new FakeNodeConnectionSessionService();
        await sessionService.ConnectAsync(
            node,
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
        var viewModel = CreateViewModel(new FakeNodeManagementService([node]), sessionService);
        await viewModel.LoadNodesAsync();

        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single());

        Assert.Equal("在线", viewModel.SelectedSshConnectionText);
        Assert.Equal("frps 运行中", viewModel.SelectedFrpProcessText);
        Assert.True(viewModel.IsSelectedFrpSuccess);
    }

    [Fact]
    public async Task RefreshRemoteFrpsStatusCommand_ShouldShowRunningProcessAndStartLocalUptime()
    {
        var node = CreateNode();
        var sessionService = new FakeNodeConnectionSessionService();
        await sessionService.ConnectAsync(
            node,
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
        var runtimeService = new FakeRemoteRuntimeService
        {
            Processes =
            [
                new RuntimeProcess("frps-1812494", node.Name, "frps", FrpNexusStatus.Running, "1812494", "00:13", "-")
            ]
        };
        var viewModel = CreateViewModel(
            new FakeNodeManagementService([node]),
            sessionService,
            remoteRuntimeService: runtimeService);
        await viewModel.LoadNodesAsync();
        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single());

        await viewModel.RefreshRemoteFrpsStatusCommand.ExecuteAsync(null);
        viewModel.TickSelectedFrpsUptimeForTest(TimeSpan.FromSeconds(2));

        Assert.Equal("frps 运行中", viewModel.SelectedFrpProcessText);
        Assert.Equal("00:15", viewModel.SelectedFrpUptimeText);
        Assert.Equal("停止", viewModel.RemoteFrpsToggleButtonText);
    }

    [Fact]
    public async Task ToggleRemoteFrpsCommand_ShouldStartWhenStoppedAndStopWhenRunning()
    {
        var node = CreateNode();
        var sessionService = new FakeNodeConnectionSessionService();
        await sessionService.ConnectAsync(
            node,
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
        var runtimeService = new FakeRemoteRuntimeService();
        var viewModel = CreateViewModel(
            new FakeNodeManagementService([node]),
            sessionService,
            remoteFileTransferService: new FakeRemoteFileTransferService(),
            remoteRuntimeService: runtimeService);
        await viewModel.LoadNodesAsync();
        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows.Single());
        await viewModel.RefreshDeploymentPresenceCommand.ExecuteAsync(null);

        runtimeService.Processes =
        [
            new RuntimeProcess("frps-1812494", node.Name, "frps", FrpNexusStatus.Running, "1812494", "00:01", "-")
        ];
        Assert.Equal("启动", viewModel.RemoteFrpsToggleButtonText);
        await viewModel.ToggleRemoteFrpsCommand.ExecuteAsync(null);

        Assert.Equal(1, runtimeService.StartRequestCount);
        Assert.Equal("停止", viewModel.RemoteFrpsToggleButtonText);

        runtimeService.Processes = [];
        await viewModel.ToggleRemoteFrpsCommand.ExecuteAsync(null);

        Assert.Equal(1, runtimeService.StopRequestCount);
        Assert.Equal("启动", viewModel.RemoteFrpsToggleButtonText);
        Assert.Equal("未运行", viewModel.SelectedFrpUptimeText);
    }

    [Fact]
    public async Task ToggleNodeConnectionCommand_RemoteFrpsRunningAndCancel_ShouldNotDisconnect()
    {
        var node = CreateNode();
        var sessionService = new FakeNodeConnectionSessionService();
        await sessionService.ConnectAsync(
            node,
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"));
        var runtimeService = new FakeRemoteRuntimeService
        {
            Processes =
            [
                new RuntimeProcess("frps-1", node.Name, "frps", FrpNexusStatus.Running, "1", "00:01", "-")
            ]
        };
        var confirmation = new FakeConfirmationDialogService { NextResult = false };
        var viewModel = CreateViewModel(
            new FakeNodeManagementService([node]),
            sessionService,
            remoteRuntimeService: runtimeService,
            confirmationDialogService: confirmation);
        await viewModel.LoadNodesAsync();

        await viewModel.ToggleNodeConnectionCommand.ExecuteAsync(viewModel.NodeRows.Single());

        Assert.Equal(1, confirmation.ShowCount);
        Assert.Equal(0, sessionService.DisconnectCount);
        Assert.Equal(NodeConnectionSessionState.Online, sessionService.GetSessionStatus(node.Name).State);
    }

    private static NodesPageViewModel CreateViewModel(
        INodeManagementService nodeManagementService,
        INodeConnectionSessionService nodeConnectionSessionService,
        INodeConnectionWorkflowDialogService? dialogService = null,
        IRemoteFileTransferService? remoteFileTransferService = null,
        IRemoteRuntimeService? remoteRuntimeService = null,
        IConfirmationDialogService? confirmationDialogService = null)
    {
        return new NodesPageViewModel(
            nodeManagementService,
            nodeConnectionSessionService,
            remoteRuntimeService ?? new FakeRemoteRuntimeService(),
            remoteFileTransferService ?? new FakeRemoteFileTransferService(),
            new Arturia.FrpNexus.Application.Configuration.TomlConfigurationService(),
            new FakeFilePickerService(),
            new FakeRemoteDirectoryPickerService(),
            new FakeNodeCredentialSecretService(),
            new FakeDeploymentRecordService(),
            dialogService,
            confirmationDialogService);
    }

    private static NodeProfile CreateNode()
    {
        return new NodeProfile(
            "行内测试节点",
            "203.0.113.70",
            22,
            "deploy",
            "会话密码",
            "Ubuntu 22.04 LTS",
            FrpNexusStatus.Online,
            FrpNexusStatus.Stopped,
            "v0.61.1",
            "-",
            "/opt/frp/frps.toml");
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

    private sealed class FakeNodeManagementService(IReadOnlyList<NodeProfile> nodes) : INodeManagementService
    {
        private readonly List<NodeProfile> _nodes = [.. nodes];

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
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNodeConnectionSessionService : INodeConnectionSessionService
    {
        private readonly Dictionary<string, NodeConnectionSessionSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SshCredentialReference> _credentials = new(StringComparer.OrdinalIgnoreCase);

        public int DisconnectCount { get; private set; }

        public Task<NodeConnectionSessionResult> ConnectAsync(
            NodeProfile node,
            SshCredentialReference credential,
            CancellationToken cancellationToken = default)
        {
            var connectedAt = DateTimeOffset.UtcNow;
            _credentials[node.Name] = credential;
            _snapshots[node.Name] = new NodeConnectionSessionSnapshot(
                node.Name,
                NodeConnectionSessionState.Online,
                connectedAt,
                "SSH 节点连接在线。");
            return Task.FromResult(new NodeConnectionSessionResult(
                node.Name,
                NodeConnectionSessionState.Online,
                connectedAt,
                "SSH 节点连接成功。"));
        }

        public Task<NodeConnectionSessionResult> DisconnectAsync(
            string nodeName,
            CancellationToken cancellationToken = default)
        {
            DisconnectCount++;
            _credentials.Remove(nodeName);
            _snapshots[nodeName] = new NodeConnectionSessionSnapshot(
                nodeName,
                NodeConnectionSessionState.Disconnected,
                null,
                "SSH 节点连接已断开。");
            return Task.FromResult(new NodeConnectionSessionResult(
                nodeName,
                NodeConnectionSessionState.Disconnected,
                null,
                "SSH 节点连接已断开。"));
        }

        public NodeConnectionSessionSnapshot GetSessionStatus(string nodeName)
        {
            return _snapshots.TryGetValue(nodeName, out var snapshot)
                ? snapshot
                : new NodeConnectionSessionSnapshot(nodeName, NodeConnectionSessionState.Offline, null, "尚未连接。");
        }

        public SshCredentialReference? GetConnectedCredential(string nodeName)
        {
            return _credentials.TryGetValue(nodeName, out var credential) ? credential : null;
        }

        public IReadOnlyList<NodeConnectionSessionSnapshot> ListActiveSessions()
        {
            return _snapshots.Values
                .Where(snapshot => snapshot.State == NodeConnectionSessionState.Online)
                .ToArray();
        }
    }

    private sealed class FakeConfirmationDialogService : IConfirmationDialogService
    {
        public int ShowCount { get; private set; }

        public bool NextResult { get; set; } = true;

        public Task<bool> ShowAsync(
            ConfirmationDialogRequest request,
            CancellationToken cancellationToken = default)
        {
            ShowCount++;
            return Task.FromResult(NextResult);
        }

        public Task<ConfirmationDialogResult> ShowChoiceAsync(
            ConfirmationDialogChoiceRequest request,
            CancellationToken cancellationToken = default)
        {
            ShowCount++;
            return Task.FromResult(NextResult
                ? ConfirmationDialogResult.Confirm
                : ConfirmationDialogResult.Cancel);
        }
    }

    private sealed class FakeNodeConnectionWorkflowDialogService : INodeConnectionWorkflowDialogService
    {
        public NodeProfile? LastNode { get; private set; }

        public NodeConnectionWorkflowOptions? LastOptions { get; private set; }

        public NodeConnectionWorkflowResult? Result { get; init; }

        public Func<NodeProfile, Task>? OnShowAsync { get; init; }

        public Task<NodeConnectionWorkflowResult> ShowAsync(
            NodeProfile node,
            NodeConnectionWorkflowOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastNode = node;
            LastOptions = options;
            return ShowCoreAsync(node);
        }

        private async Task<NodeConnectionWorkflowResult> ShowCoreAsync(NodeProfile node)
        {
            if (OnShowAsync is not null)
            {
                await OnShowAsync(node);
            }

            return Result ?? new NodeConnectionWorkflowResult(node.Name, false, false, false);
        }
    }

    private sealed class FakeRemoteRuntimeService : IRemoteRuntimeService
    {
        public IReadOnlyList<RuntimeProcess> Processes { get; set; } = [];

        public Exception? GetProcessesException { get; set; }

        public int GetProcessesRequestCount { get; private set; }

        public int StartRequestCount { get; private set; }

        public int StopRequestCount { get; private set; }

        public int RestartRequestCount { get; private set; }

        public Task<IReadOnlyList<RuntimeProcess>> GetProcessesAsync(
            RemoteRuntimeQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            GetProcessesRequestCount++;
            if (GetProcessesException is not null)
            {
                throw GetProcessesException;
            }

            return Task.FromResult(Processes);
        }

        public Task<RemoteRuntimeCommandResult> StartAsync(RemoteRuntimeCommandRequest request, CancellationToken cancellationToken = default)
        {
            StartRequestCount++;
            return Task.FromResult(CreateResult(request));
        }

        public Task<RemoteRuntimeCommandResult> StopAsync(RemoteRuntimeCommandRequest request, CancellationToken cancellationToken = default)
        {
            StopRequestCount++;
            return Task.FromResult(CreateResult(request));
        }

        public Task<RemoteRuntimeCommandResult> RestartAsync(RemoteRuntimeCommandRequest request, CancellationToken cancellationToken = default)
        {
            RestartRequestCount++;
            return Task.FromResult(CreateResult(request));
        }

        private static RemoteRuntimeCommandResult CreateResult(RemoteRuntimeCommandRequest request)
        {
            return new RemoteRuntimeCommandResult(request.Node.Name, request.ProcessKind, FrpNexusStatus.Ready, DateTimeOffset.UtcNow, "ok");
        }
    }

    private sealed class FakeRemoteFileTransferService(
        IReadOnlyCollection<string>? existingPaths = null,
        FrpNexusStatus status = FrpNexusStatus.Ready,
        string message = "ok") : IRemoteFileTransferService
    {
        private readonly FrpNexusStatus _status = status;
        private readonly string _message = message;

        public IReadOnlyCollection<string>? ExistingPaths { get; set; } = existingPaths;

        public RemoteFilePresenceRequest? LastPresenceRequest { get; private set; }

        public int PresenceRequestCount { get; private set; }

        public Task<RemoteFilePresenceResult> CheckRemoteFilesAsync(
            RemoteFilePresenceRequest request,
            CancellationToken cancellationToken = default)
        {
            PresenceRequestCount++;
            LastPresenceRequest = request;
            IReadOnlyList<RemoteFilePresenceEntry> files = request.RemotePaths
                .Select(path => new RemoteFilePresenceEntry(path, ExistingPaths is null || ExistingPaths.Contains(path)))
                .ToArray();
            return Task.FromResult(new RemoteFilePresenceResult(
                request.Node.Name,
                files,
                _status,
                DateTimeOffset.UtcNow,
                _message));
        }

        public Task<RemoteFileTransferResult> UploadFrpBinaryAsync(RemoteFileUploadRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RemoteFileTransferResult(request.Node.Name, request.RemotePath, FrpNexusStatus.Ready, DateTimeOffset.UtcNow, "ok"));
        }

        public Task<RemoteFileTransferResult> UploadConfigurationAsync(RemoteConfigurationUploadRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RemoteFileTransferResult(request.Node.Name, request.RemotePath, FrpNexusStatus.Ready, DateTimeOffset.UtcNow, "ok"));
        }

        public Task<RemoteFileDeleteResult> DeleteRemoteFilesAsync(RemoteFileDeleteRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RemoteFileDeleteResult(request.Node.Name, request.RemotePaths, [], FrpNexusStatus.Ready, DateTimeOffset.UtcNow, "ok"));
        }
    }

    private sealed class DelayedRemoteFileTransferService(IReadOnlyCollection<string>? existingPaths = null) : IRemoteFileTransferService
    {
        private readonly TaskCompletionSource _releaseChecks = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyCollection<string>? ExistingPaths { get; } = existingPaths;

        public int PresenceRequestCount { get; private set; }

        public async Task<RemoteFilePresenceResult> CheckRemoteFilesAsync(
            RemoteFilePresenceRequest request,
            CancellationToken cancellationToken = default)
        {
            PresenceRequestCount++;
            await _releaseChecks.Task.WaitAsync(cancellationToken);
            IReadOnlyList<RemoteFilePresenceEntry> files = request.RemotePaths
                .Select(path => new RemoteFilePresenceEntry(path, ExistingPaths is null || ExistingPaths.Contains(path)))
                .ToArray();
            return new RemoteFilePresenceResult(
                request.Node.Name,
                files,
                FrpNexusStatus.Ready,
                DateTimeOffset.UtcNow,
                "ok");
        }

        public void ReleasePendingChecks()
        {
            _releaseChecks.TrySetResult();
        }

        public Task<RemoteFileTransferResult> UploadFrpBinaryAsync(RemoteFileUploadRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RemoteFileTransferResult(request.Node.Name, request.RemotePath, FrpNexusStatus.Ready, DateTimeOffset.UtcNow, "ok"));
        }

        public Task<RemoteFileTransferResult> UploadConfigurationAsync(RemoteConfigurationUploadRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RemoteFileTransferResult(request.Node.Name, request.RemotePath, FrpNexusStatus.Ready, DateTimeOffset.UtcNow, "ok"));
        }

        public Task<RemoteFileDeleteResult> DeleteRemoteFilesAsync(RemoteFileDeleteRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RemoteFileDeleteResult(request.Node.Name, request.RemotePaths, [], FrpNexusStatus.Ready, DateTimeOffset.UtcNow, "ok"));
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

    private sealed class FakeNodeCredentialSecretService : INodeCredentialSecretService
    {
        public Task<bool> HasSessionPasswordAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<string?> GetSessionPasswordAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public Task SaveSessionPasswordAsync(string nodeName, string sessionPassword, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteSessionPasswordAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDeploymentRecordService : IDeploymentRecordService
    {
        public Task<IReadOnlyList<DeploymentRecord>> ListDeploymentRecordsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DeploymentRecord>>([]);
        }

        public Task<DeploymentRecord?> GetDeploymentRecordAsync(string stepName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DeploymentRecord?>(null);
        }

        public Task SaveDeploymentRecordAsync(DeploymentRecord record, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteDeploymentRecordAsync(string stepName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
