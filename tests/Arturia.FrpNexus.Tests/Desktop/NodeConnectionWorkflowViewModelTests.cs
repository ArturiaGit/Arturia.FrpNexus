using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Application.Configuration;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.Services;
using Arturia.FrpNexus.Desktop.ViewModels.Dialogs;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class NodeConnectionWorkflowViewModelTests
{
    [Fact]
    public void Constructor_ShouldHideDeploymentPreparationByDefault()
    {
        var fileTransferService = new FakeRemoteFileTransferService(new HashSet<string>(StringComparer.Ordinal));
        var viewModel = CreateViewModel(fileTransferService);

        Assert.False(viewModel.IsDeploymentPreparationVisible);
    }

    [Fact]
    public async Task ConnectCommand_ShouldHidePreparationWhileDeploymentPresenceIsChecking()
    {
        var fileTransferService = new FakeRemoteFileTransferService(new HashSet<string>(StringComparer.Ordinal))
        {
            HoldPresenceCheck = true
        };
        var viewModel = CreateViewModel(fileTransferService);
        viewModel.SshSessionPassword = "SESSION_PASSWORD_PLACEHOLDER";

        var connectTask = viewModel.ConnectCommand.ExecuteAsync(null);
        await fileTransferService.WaitForPresenceCheckAsync();

        Assert.True(viewModel.IsConnected);
        Assert.True(viewModel.IsDeploymentPresenceChecking);
        Assert.False(viewModel.IsDeploymentPreparationVisible);

        try
        {
            fileTransferService.ReleasePresenceCheck();
            await connectTask;
        }
        finally
        {
            fileTransferService.ReleasePresenceCheck();
        }
    }

    [Fact]
    public async Task ConnectCommand_ShouldCheckDeploymentPresenceAndHidePreparationWhenReady()
    {
        var fileTransferService = new FakeRemoteFileTransferService(
            new HashSet<string>(StringComparer.Ordinal)
            {
                "/opt/frp/frps",
                "/opt/frp/frps.toml"
            });
        var viewModel = CreateViewModel(fileTransferService);
        viewModel.SshSessionPassword = "SESSION_PASSWORD_PLACEHOLDER";

        await viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsConnected);
        Assert.True(viewModel.IsDeploymentReady);
        Assert.False(viewModel.IsDeploymentPreparationVisible);
        Assert.True(viewModel.IsRemoteFileDeleteVisible);
        Assert.True(viewModel.HasDeploymentPresenceChecked);
        Assert.Equal(["/opt/frp/frps", "/opt/frp/frps.toml"], fileTransferService.LastPresenceRequest?.RemotePaths);
    }

    [Fact]
    public async Task ConnectCommand_ShouldShowPreparationWhenDeploymentFilesAreMissing()
    {
        var fileTransferService = new FakeRemoteFileTransferService(
            new HashSet<string>(StringComparer.Ordinal)
            {
                "/opt/frp/frps"
            });
        var viewModel = CreateViewModel(fileTransferService);
        viewModel.SshSessionPassword = "SESSION_PASSWORD_PLACEHOLDER";

        await viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsConnected);
        Assert.False(viewModel.IsDeploymentReady);
        Assert.True(viewModel.IsDeploymentPreparationVisible);
        Assert.False(viewModel.IsRemoteFileDeleteVisible);
        Assert.True(viewModel.HasDeploymentPresenceChecked);
        Assert.Contains("frps.toml", viewModel.RemoteFilePresenceText);
    }

    [Fact]
    public async Task ConnectCommand_ShouldKeepPreparationHiddenWhenDeploymentCheckFails()
    {
        var fileTransferService = new FakeRemoteFileTransferService(new HashSet<string>(StringComparer.Ordinal))
        {
            PresenceStatus = FrpNexusStatus.Error
        };
        var viewModel = CreateViewModel(fileTransferService);
        viewModel.SshSessionPassword = "SESSION_PASSWORD_PLACEHOLDER";

        await viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsConnected);
        Assert.False(viewModel.IsDeploymentReady);
        Assert.False(viewModel.IsDeploymentPreparationVisible);
        Assert.False(viewModel.IsRemoteFileDeleteVisible);
        Assert.False(viewModel.IsDeploymentPresenceChecking);
    }


    [Fact]
    public async Task UploadCommands_ShouldRecheckDeploymentPresenceAndMarkReady()
    {
        var fileTransferService = new FakeRemoteFileTransferService(new HashSet<string>(StringComparer.Ordinal));
        var viewModel = CreateViewModel(fileTransferService);
        viewModel.SshSessionPassword = "SESSION_PASSWORD_PLACEHOLDER";

        await viewModel.ConnectCommand.ExecuteAsync(null);
        viewModel.SelectedLocalCorePath = @"C:\tools\frps";
        await viewModel.UploadCoreCommand.ExecuteAsync(null);
        await viewModel.UploadServerTomlCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsConnected);
        Assert.True(viewModel.HasDeploymentChanged);
        Assert.True(viewModel.IsDeploymentReady);
        Assert.False(viewModel.IsDeploymentPreparationVisible);
        Assert.True(viewModel.IsRemoteFileDeleteVisible);
        Assert.Equal(3, fileTransferService.PresenceRequestCount);
    }

    [Fact]
    public async Task LoadAsync_ShouldCheckDeploymentPresenceWhenNodeSessionIsAlreadyOnline()
    {
        var node = CreateNode();
        var sessionService = new FakeNodeConnectionSessionService();
        sessionService.SetOnline(node);
        var fileTransferService = new FakeRemoteFileTransferService(
            new HashSet<string>(StringComparer.Ordinal)
            {
                "/opt/frp/frps"
            });
        var viewModel = CreateViewModel(fileTransferService, sessionService, node);

        await viewModel.LoadAsync();

        Assert.True(viewModel.IsConnected);
        Assert.False(viewModel.IsDeploymentReady);
        Assert.True(viewModel.IsDeploymentPreparationVisible);
        Assert.True(viewModel.HasDeploymentPresenceChecked);
        Assert.Equal(["/opt/frp/frps", "/opt/frp/frps.toml"], fileTransferService.LastPresenceRequest?.RemotePaths);
    }

    [Fact]
    public async Task LoadAsync_ShouldSkipInitialPresenceCheckWhenDeployMissingFilesOptionIsUsed()
    {
        var node = CreateNode();
        var sessionService = new FakeNodeConnectionSessionService();
        sessionService.SetOnline(node);
        var fileTransferService = new FakeRemoteFileTransferService(
            new HashSet<string>(StringComparer.Ordinal)
            {
                "/opt/frp/frps"
            });
        var viewModel = CreateViewModel(
            fileTransferService,
            sessionService,
            node,
            NodeConnectionWorkflowOptions.DeployMissingFiles);

        await viewModel.LoadAsync();

        Assert.True(viewModel.IsConnected);
        Assert.False(viewModel.IsDeploymentReady);
        Assert.True(viewModel.IsDeploymentPreparationVisible);
        Assert.True(viewModel.HasDeploymentPresenceChecked);
        Assert.Equal("需要部署准备", viewModel.DeploymentStatusTitle);
        Assert.Equal(0, fileTransferService.PresenceRequestCount);
        Assert.Null(fileTransferService.LastPresenceRequest);
    }

    [Fact]
    public async Task DeleteRemoteFrpFilesCommand_ShouldShowPreparationAfterCleanup()
    {
        var fileTransferService = new FakeRemoteFileTransferService(
            new HashSet<string>(StringComparer.Ordinal)
            {
                "/opt/frp/frps",
                "/opt/frp/frps.toml"
            });
        var viewModel = CreateViewModel(fileTransferService);
        viewModel.SshSessionPassword = "SESSION_PASSWORD_PLACEHOLDER";
        await viewModel.ConnectCommand.ExecuteAsync(null);

        await viewModel.DeleteRemoteFrpFilesCommand.ExecuteAsync(null);
        await viewModel.DeleteRemoteFrpFilesCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasDeploymentChanged);
        Assert.False(viewModel.IsDeploymentReady);
        Assert.True(viewModel.IsDeploymentPreparationVisible);
        Assert.False(viewModel.IsRemoteFileDeleteVisible);
    }

    private static NodeConnectionWorkflowViewModel CreateViewModel(
        FakeRemoteFileTransferService fileTransferService,
        INodeConnectionSessionService? nodeConnectionSessionService = null,
        NodeProfile? node = null,
        NodeConnectionWorkflowOptions? options = null)
    {
        return new NodeConnectionWorkflowViewModel(
            nodeConnectionSessionService ?? new FakeNodeConnectionSessionService(),
            fileTransferService,
            new FakeRemoteRuntimeService(),
            new TomlConfigurationService(),
            new FakeFilePickerService(),
            new FakeRemoteDirectoryPickerService(),
            new FakeNodeCredentialSecretService(),
            new FakeDeploymentRecordService(),
            node ?? CreateNode(),
            options ?? NodeConnectionWorkflowOptions.Default,
            _ => { });
    }

    private static NodeProfile CreateNode()
    {
        return new NodeProfile(
            "连接工作流测试节点",
            "203.0.113.80",
            22,
            "deploy",
            "会话密码",
            "Ubuntu 22.04 LTS",
            FrpNexusStatus.Offline,
            FrpNexusStatus.Stopped,
            "v0.61.1",
            "-",
            "/opt/frp/frps.toml");
    }

    private sealed class FakeNodeConnectionSessionService : INodeConnectionSessionService
    {
        private readonly Dictionary<string, NodeConnectionSessionSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SshCredentialReference> _credentials = new(StringComparer.OrdinalIgnoreCase);

        public void SetOnline(NodeProfile node)
        {
            var credential = new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: "SESSION_PASSWORD_PLACEHOLDER");
            _credentials[node.Name] = credential;
            _snapshots[node.Name] = new NodeConnectionSessionSnapshot(
                node.Name,
                NodeConnectionSessionState.Online,
                DateTimeOffset.UtcNow,
                "SSH 节点连接在线。");
        }

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
                "SSH 节点连接成功。");
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
    }

    private sealed class FakeRemoteFileTransferService(IReadOnlySet<string> existingPaths)
        : IRemoteFileTransferService
    {
        private readonly HashSet<string> _existingPaths = new(existingPaths, StringComparer.Ordinal);
        private readonly TaskCompletionSource _presenceCheckStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _presenceCheckRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RemoteFilePresenceRequest? LastPresenceRequest { get; private set; }

        public int PresenceRequestCount { get; private set; }

        public bool HoldPresenceCheck { get; init; }

        public FrpNexusStatus? PresenceStatus { get; init; }

        public Task WaitForPresenceCheckAsync()
        {
            return _presenceCheckStarted.Task;
        }

        public void ReleasePresenceCheck()
        {
            _presenceCheckRelease.TrySetResult();
        }

        public Task<RemoteFilePresenceResult> CheckRemoteFilesAsync(
            RemoteFilePresenceRequest request,
            CancellationToken cancellationToken = default)
        {
            return CheckRemoteFilesCoreAsync(request, cancellationToken);
        }

        private async Task<RemoteFilePresenceResult> CheckRemoteFilesCoreAsync(
            RemoteFilePresenceRequest request,
            CancellationToken cancellationToken)
        {
            PresenceRequestCount++;
            LastPresenceRequest = request;
            _presenceCheckStarted.TrySetResult();
            if (HoldPresenceCheck)
            {
                await _presenceCheckRelease.Task.WaitAsync(cancellationToken);
            }

            IReadOnlyList<RemoteFilePresenceEntry> files = request.RemotePaths
                .Select(path => new RemoteFilePresenceEntry(path, _existingPaths.Contains(path)))
                .ToArray();
            var ready = files.All(file => file.Exists);
            return new RemoteFilePresenceResult(
                request.Node.Name,
                files,
                PresenceStatus ?? (ready ? FrpNexusStatus.Ready : FrpNexusStatus.Warning),
                DateTimeOffset.UtcNow,
                ready ? "远程 frps 和 frps.toml 已就绪。" : "远程部署文件不完整，需要补齐缺失文件。");
        }

        public Task<RemoteFileTransferResult> UploadFrpBinaryAsync(
            RemoteFileUploadRequest request,
            CancellationToken cancellationToken = default)
        {
            _existingPaths.Add(request.RemotePath);
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
            _existingPaths.Add(request.RemotePath);
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
            foreach (var remotePath in request.RemotePaths)
            {
                _existingPaths.Remove(remotePath);
            }

            return Task.FromResult(new RemoteFileDeleteResult(
                request.Node.Name,
                request.RemotePaths,
                [],
                FrpNexusStatus.Ready,
                DateTimeOffset.UtcNow,
                "远程文件已清理。"));
        }
    }

    private sealed class FakeRemoteRuntimeService : IRemoteRuntimeService
    {
        public Task<IReadOnlyList<RuntimeProcess>> GetProcessesAsync(
            RemoteRuntimeQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RuntimeProcess>>([]);
        }

        public Task<RemoteRuntimeCommandResult> StartAsync(
            RemoteRuntimeCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            return CreateResult(request);
        }

        public Task<RemoteRuntimeCommandResult> StopAsync(
            RemoteRuntimeCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            return CreateResult(request);
        }

        public Task<RemoteRuntimeCommandResult> RestartAsync(
            RemoteRuntimeCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            return CreateResult(request);
        }

        private static Task<RemoteRuntimeCommandResult> CreateResult(RemoteRuntimeCommandRequest request)
        {
            return Task.FromResult(new RemoteRuntimeCommandResult(
                request.Node.Name,
                request.ProcessName,
                FrpNexusStatus.Ready,
                DateTimeOffset.UtcNow,
                "远程命令执行完成。"));
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

    private sealed class FakeDeploymentRecordService : IDeploymentRecordService
    {
        public Task<IReadOnlyList<DeploymentRecord>> ListDeploymentRecordsAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DeploymentRecord>>([]);
        }

        public Task<DeploymentRecord?> GetDeploymentRecordAsync(
            string stepName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DeploymentRecord?>(null);
        }

        public Task SaveDeploymentRecordAsync(
            DeploymentRecord record,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteDeploymentRecordAsync(
            string stepName,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
