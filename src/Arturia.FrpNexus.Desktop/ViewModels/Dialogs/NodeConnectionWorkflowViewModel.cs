using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.Services;
using Arturia.FrpNexus.Desktop.ViewModels.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Dialogs;

public sealed partial class NodeConnectionWorkflowViewModel : ObservableObject
{
    private readonly INodeConnectionSessionService _nodeConnectionSessionService;
    private readonly IRemoteFileTransferService _remoteFileTransferService;
    private readonly IRemoteRuntimeService _remoteRuntimeService;
    private readonly ITomlConfigurationService _tomlConfigurationService;
    private readonly IFilePickerService _filePickerService;
    private readonly IRemoteDirectoryPickerService _remoteDirectoryPickerService;
    private readonly INodeCredentialSecretService _nodeCredentialSecretService;
    private readonly IDeploymentRecordService _deploymentRecordService;
    private readonly NodeConnectionWorkflowOptions _options;
    private readonly Action<NodeConnectionWorkflowResult> _close;
    private bool _isRemoteFrpDeleteConfirmationPending;

    public NodeConnectionWorkflowViewModel(
        INodeConnectionSessionService nodeConnectionSessionService,
        IRemoteFileTransferService remoteFileTransferService,
        IRemoteRuntimeService remoteRuntimeService,
        ITomlConfigurationService tomlConfigurationService,
        IFilePickerService filePickerService,
        IRemoteDirectoryPickerService remoteDirectoryPickerService,
        INodeCredentialSecretService nodeCredentialSecretService,
        IDeploymentRecordService deploymentRecordService,
        NodeProfile node,
        NodeConnectionWorkflowOptions options,
        Action<NodeConnectionWorkflowResult> close)
    {
        _nodeConnectionSessionService = nodeConnectionSessionService;
        _remoteFileTransferService = remoteFileTransferService;
        _remoteRuntimeService = remoteRuntimeService;
        _tomlConfigurationService = tomlConfigurationService;
        _filePickerService = filePickerService;
        _remoteDirectoryPickerService = remoteDirectoryPickerService;
        _nodeCredentialSecretService = nodeCredentialSecretService;
        _deploymentRecordService = deploymentRecordService;
        Node = node;
        _options = options;
        _close = close;
        RemoteCoreDirectory = NodeConnectionWorkflowHelpers.ResolveFrpDirectoryFromConfigPath(node.ConfigPath);
        ServerTomlPreview = _tomlConfigurationService.GenerateServerToml(7000);
    }

    public NodeProfile Node { get; }

    public string NodeSummaryText => $"{Node.UserName}@{Node.Host}:{Node.SshPort}";

    public IReadOnlyList<string> SshAuthenticationModeOptions { get; } =
    [
        NodeConnectionWorkflowHelpers.SessionPasswordAuthenticationOption,
        NodeConnectionWorkflowHelpers.PrivateKeyAuthenticationOption,
        NodeConnectionWorkflowHelpers.SshAgentAuthenticationOption
    ];

    [ObservableProperty]
    private string _selectedSshAuthenticationMode = NodeConnectionWorkflowHelpers.SessionPasswordAuthenticationOption;

    [ObservableProperty]
    private string _sshPrivateKeyPath = string.Empty;

    [ObservableProperty]
    private string _sshSessionPassword = string.Empty;

    [ObservableProperty]
    private bool _rememberSessionPassword;

    [ObservableProperty]
    private bool _hasSavedSessionPassword;

    [ObservableProperty]
    private string _savedSessionPasswordText = "未保存会话密码。";

    [ObservableProperty]
    private string _sshPrivateKeyPassphrase = string.Empty;

    [ObservableProperty]
    private string _connectionStatusTitle = "等待连接";

    [ObservableProperty]
    private string _connectionStatusText = "输入本次 SSH 连接凭据后连接节点。";

    [ObservableProperty]
    private string _connectionSeverity = "info";

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _selectedLocalCorePath = string.Empty;

    [ObservableProperty]
    private string _selectedLocalCoreFileName = "尚未选择";

    [ObservableProperty]
    private string _remoteCoreDirectory = NodeConnectionWorkflowHelpers.DefaultRemoteCoreDirectory;

    [ObservableProperty]
    private string _deploymentStatusTitle = "可选部署";

    [ObservableProperty]
    private string _deploymentStatusText = "连接成功后可上传 frps 核心、frps.toml，或清理远程文件。";

    [ObservableProperty]
    private string _deploymentSeverity = "info";

    [ObservableProperty]
    private bool _isDeploymentReady;

    [ObservableProperty]
    private bool _isDeploymentPreparationVisible;

    [ObservableProperty]
    private string _remoteFilePresenceText = "连接成功后会检查远程 frps 和 frps.toml，并按真实状态显示上传或清理入口。";

    [ObservableProperty]
    private bool _isDeploymentPresenceChecking;

    [ObservableProperty]
    private bool _isUploadingCore;

    [ObservableProperty]
    private bool _isUploadingServerToml;

    [ObservableProperty]
    private bool _isDeletingRemoteFrpFiles;

    [ObservableProperty]
    private string _deleteRemoteFrpFilesButtonText = "清理远程文件";

    [ObservableProperty]
    private string _serverTomlPreview;

    [ObservableProperty]
    private string _runtimeStatusTitle = "尚未刷新";

    [ObservableProperty]
    private string _runtimeStatusText = "连接节点后可以刷新远程 frps 运行状态。";

    [ObservableProperty]
    private string _runtimeSeverity = "info";

    [ObservableProperty]
    private bool _isRuntimeCommandRunning;

    [ObservableProperty]
    private string _remoteFrpsStatusText = "未知";

    [ObservableProperty]
    private string _remoteFrpsUptimeText = "-";

    public bool IsSessionPasswordMode => NodeConnectionWorkflowHelpers.TryParseAuthenticationMode(SelectedSshAuthenticationMode, out var mode)
        && mode == SshAuthenticationMode.SessionPassword;

    public bool IsPrivateKeyMode => NodeConnectionWorkflowHelpers.TryParseAuthenticationMode(SelectedSshAuthenticationMode, out var mode)
        && mode == SshAuthenticationMode.PrivateKey;

    public bool IsSshAgentMode => NodeConnectionWorkflowHelpers.TryParseAuthenticationMode(SelectedSshAuthenticationMode, out var mode)
        && mode == SshAuthenticationMode.SshAgent;

    public bool CanConnect => !IsConnecting && !IsConnected && !IsSshAgentMode;

    public string ConnectButtonText => IsConnecting ? "正在连接..." : "连接";

    public bool IsConnectionProgressVisible => IsConnecting;

    public bool IsDeploymentVisible => IsConnected;

    public bool IsDeploymentStatusProgressVisible => IsAnyDeploymentOperationRunning;

    public string ConnectionBadgeText => IsConnected ? "已连接" : "未连接";

    public bool IsAnyDeploymentOperationRunning =>
        IsDeploymentPresenceChecking || IsUploadingCore || IsUploadingServerToml || IsDeletingRemoteFrpFiles;

    public bool CanPickLocalCoreFile => IsConnected && !IsAnyDeploymentOperationRunning;

    public bool CanPickRemoteCoreDirectory => IsConnected && !IsAnyDeploymentOperationRunning;

    public bool CanUploadCore => IsConnected && !IsAnyDeploymentOperationRunning;

    public bool CanUploadServerToml => IsConnected && !IsAnyDeploymentOperationRunning;

    public bool CanDeleteRemoteFrpFiles => IsConnected && IsDeploymentReady && !IsAnyDeploymentOperationRunning;

    public bool IsRemoteFileDeleteVisible => IsConnected && IsDeploymentReady;

    public bool CanRunRemoteFrpsCommand => IsConnected && !IsRuntimeCommandRunning;

    public bool IsRuntimeProgressVisible => IsRuntimeCommandRunning;

    public string UploadCoreButtonText => IsUploadingCore ? "正在上传..." : "上传 frps 核心";

    public string UploadServerTomlButtonText => IsUploadingServerToml ? "正在上传..." : "上传 frps.toml";

    public string RemoteCorePath => NodeConnectionWorkflowHelpers.CombineRemotePath(
        RemoteCoreDirectory,
        GetRemoteCoreFileName());

    public string RemoteServerConfigPath => NodeConnectionWorkflowHelpers.CombineRemotePath(
        RemoteCoreDirectory,
        NodeConnectionWorkflowHelpers.DefaultServerConfigFileName);

    public bool IsConnectionInfo => string.Equals(ConnectionSeverity, "info", StringComparison.OrdinalIgnoreCase);

    public bool IsConnectionSuccess => string.Equals(ConnectionSeverity, "success", StringComparison.OrdinalIgnoreCase);

    public bool IsConnectionWarning => string.Equals(ConnectionSeverity, "warning", StringComparison.OrdinalIgnoreCase);

    public bool IsConnectionError => string.Equals(ConnectionSeverity, "error", StringComparison.OrdinalIgnoreCase);

    public bool IsDeploymentInfo => string.Equals(DeploymentSeverity, "info", StringComparison.OrdinalIgnoreCase);

    public bool IsDeploymentSuccess => string.Equals(DeploymentSeverity, "success", StringComparison.OrdinalIgnoreCase);

    public bool IsDeploymentWarning => string.Equals(DeploymentSeverity, "warning", StringComparison.OrdinalIgnoreCase);

    public bool IsDeploymentError => string.Equals(DeploymentSeverity, "error", StringComparison.OrdinalIgnoreCase);

    public bool IsRuntimeInfo => string.Equals(RuntimeSeverity, "info", StringComparison.OrdinalIgnoreCase);

    public bool IsRuntimeSuccess => string.Equals(RuntimeSeverity, "success", StringComparison.OrdinalIgnoreCase);

    public bool IsRuntimeWarning => string.Equals(RuntimeSeverity, "warning", StringComparison.OrdinalIgnoreCase);

    public bool IsRuntimeError => string.Equals(RuntimeSeverity, "error", StringComparison.OrdinalIgnoreCase);

    public bool HasDeploymentChanged { get; private set; }

    public bool HasDeploymentPresenceChecked { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RefreshSavedSessionPasswordStateAsync(cancellationToken);
        var sessionSnapshot = _nodeConnectionSessionService.GetSessionStatus(Node.Name);
        if (sessionSnapshot.State != NodeConnectionSessionState.Online)
        {
            return;
        }

        IsConnected = true;
        SetConnectionStatus("已连接", sessionSnapshot.Message, "success");
        await RefreshRuntimeStatusAsync(cancellationToken, showIdleStatus: false);
        if (_options.SkipInitialDeploymentPresenceCheck)
        {
            ShowDeploymentPreparationFromKnownMissingState();
            return;
        }

        await RefreshDeploymentPresenceAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var typedSessionPassword = SshSessionPassword;
        var credentialResult = await TryCreateCredentialReferenceAsync(cancellationToken);
        if (!credentialResult.Success)
        {
            return;
        }

        IsConnecting = true;
        SetConnectionStatus("正在连接", $"正在连接 {Node.Name}...", "info");

        try
        {
            var result = await _nodeConnectionSessionService.ConnectAsync(
                Node,
                credentialResult.Credential,
                cancellationToken);

            if (result.State == NodeConnectionSessionState.Online)
            {
                IsConnected = true;
                if (result.ConnectedAt is not null)
                {
                    await SaveRememberedSessionPasswordIfNeededAsync(Node.Name, typedSessionPassword, cancellationToken);
                }

                SetConnectionStatus("连接成功", result.Message, "success");
                await RefreshRuntimeStatusAsync(cancellationToken, showIdleStatus: false);
                await RefreshDeploymentPresenceAsync(cancellationToken);
                return;
            }

            SetConnectionStatus("连接失败", result.Message, "error");
        }
        catch (OperationCanceledException)
        {
            SetConnectionStatus("连接已取消", "SSH 节点连接已取消。", "warning");
        }
        catch (Exception ex)
        {
            SetConnectionStatus("连接失败", ViewModelErrorText.ForUser("SSH 节点连接", ex), "error");
        }
        finally
        {
            ClearSessionSecrets();
            IsConnecting = false;
        }
    }

    [RelayCommand]
    private async Task PickLocalCoreFileAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var localPath = await _filePickerService.PickFrpBinaryAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(localPath))
            {
                SetDeploymentStatus("已取消选择", "未选择本地 frps 文件。", "warning");
                return;
            }

            SelectedLocalCorePath = localPath;
            if (!NodeConnectionWorkflowHelpers.IsFrpsFileName(SelectedLocalCoreFileName))
            {
                SetDeploymentStatus("请选择 frps", "远程 VPS 节点只需要上传 frps；frpc 将在本地由隧道页启动。", "warning");
                return;
            }

            SetDeploymentStatus("已选择 frps 核心", $"本次上传文件：{SelectedLocalCoreFileName}，远程路径预览：{RemoteCorePath}", "info");
        }
        catch (OperationCanceledException)
        {
            SetDeploymentStatus("已取消选择", "frps 文件选择已取消。", "warning");
        }
        catch (Exception ex)
        {
            SetDeploymentStatus("选择文件失败", ViewModelErrorText.ForUser("FRP 核心文件选择", ex), "error");
        }
    }

    [RelayCommand]
    private async Task PickRemoteCoreDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var credential = GetConnectedCredential();
        if (credential is null)
        {
            return;
        }

        try
        {
            var selectedDirectory = await _remoteDirectoryPickerService.PickRemoteDirectoryAsync(
                Node,
                credential,
                RemoteCoreDirectory,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(selectedDirectory))
            {
                SetDeploymentStatus("已取消选择", "未选择远程目录。", "warning");
                return;
            }

            RemoteCoreDirectory = selectedDirectory;
            SetDeploymentStatus("已选择远程目录", $"frps 路径：{RemoteCorePath}；frps.toml 路径：{RemoteServerConfigPath}", "info");
        }
        catch (OperationCanceledException)
        {
            SetDeploymentStatus("已取消选择", "远程目录选择已取消。", "warning");
        }
        catch (Exception ex)
        {
            SetDeploymentStatus("选择目录失败", ViewModelErrorText.ForUser("远程目录选择", ex), "error");
        }
    }

    [RelayCommand]
    private async Task UploadCoreAsync(CancellationToken cancellationToken = default)
    {
        var credential = GetConnectedCredential();
        if (credential is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedLocalCorePath))
        {
            SetDeploymentStatus("请选择 frps 核心", "请先选择本地 frps 文件，再执行上传。", "warning");
            return;
        }

        if (!NodeConnectionWorkflowHelpers.IsFrpsFileName(SelectedLocalCoreFileName))
        {
            SetDeploymentStatus("请选择 frps", "远程 VPS 节点只支持上传 frps。", "warning");
            return;
        }

        var directoryValidation = NodeConnectionWorkflowHelpers.ValidateRemoteCoreDirectory(RemoteCoreDirectory);
        if (!string.IsNullOrWhiteSpace(directoryValidation))
        {
            SetDeploymentStatus("远程目录无效", directoryValidation, "warning");
            return;
        }

        var fileNameValidation = NodeConnectionWorkflowHelpers.ValidateRemoteCoreFileName(GetRemoteCoreFileName());
        if (!string.IsNullOrWhiteSpace(fileNameValidation))
        {
            SetDeploymentStatus("远程文件名无效", fileNameValidation, "warning");
            return;
        }

        IsUploadingCore = true;
        SetDeploymentStatus("正在上传 frps 核心", $"正在上传到 {Node.Name}：{RemoteCorePath}", "info");

        try
        {
            var result = await _remoteFileTransferService.UploadFrpBinaryAsync(
                new RemoteFileUploadRequest(Node, credential, SelectedLocalCorePath, RemoteCorePath),
                cancellationToken);

            if (result.Status is FrpNexusStatus.Ready or FrpNexusStatus.Online or FrpNexusStatus.Running)
            {
                await SaveLastUploadedCoreRecordAsync(result, cancellationToken);
                HasDeploymentChanged = true;
                SetDeploymentStatus("上传成功", $"{result.Message} 目标：{result.RemotePath}", "success");
                await RefreshDeploymentPresenceAsync(cancellationToken);
                return;
            }

            SetDeploymentStatus("上传失败", result.Message, "error");
        }
        catch (OperationCanceledException)
        {
            SetDeploymentStatus("上传已取消", "FRP 核心上传已取消。", "warning");
        }
        catch (Exception ex)
        {
            SetDeploymentStatus("上传失败", ViewModelErrorText.ForUser("FRP 核心上传", ex), "error");
        }
        finally
        {
            IsUploadingCore = false;
        }
    }

    [RelayCommand]
    private async Task UploadServerTomlAsync(CancellationToken cancellationToken = default)
    {
        var credential = GetConnectedCredential();
        if (credential is null)
        {
            return;
        }

        var directoryValidation = NodeConnectionWorkflowHelpers.ValidateRemoteCoreDirectory(RemoteCoreDirectory);
        if (!string.IsNullOrWhiteSpace(directoryValidation))
        {
            SetDeploymentStatus("远程目录无效", directoryValidation, "warning");
            return;
        }

        ServerTomlPreview = _tomlConfigurationService.GenerateServerToml(7000);
        IsUploadingServerToml = true;
        SetDeploymentStatus("正在上传 frps.toml", $"已生成本地 frps.toml，正在上传到 {Node.Name}：{RemoteServerConfigPath}", "info");

        try
        {
            var result = await _remoteFileTransferService.UploadConfigurationAsync(
                new RemoteConfigurationUploadRequest(Node, credential, ServerTomlPreview, RemoteServerConfigPath),
                cancellationToken);

            if (result.Status == FrpNexusStatus.Error)
            {
                SetDeploymentStatus("frps.toml 上传失败", result.Message, "error");
                return;
            }

            HasDeploymentChanged = true;
            SetDeploymentStatus("frps.toml 上传成功", $"已上传 frps.toml 到 {result.RemotePath}。", "success");
            await RefreshDeploymentPresenceAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            SetDeploymentStatus("上传已取消", "frps.toml 上传已取消。", "warning");
        }
        catch (Exception ex)
        {
            SetDeploymentStatus("上传失败", ViewModelErrorText.ForUser("frps.toml 上传", ex), "error");
        }
        finally
        {
            IsUploadingServerToml = false;
        }
    }

    [RelayCommand]
    private async Task DeleteRemoteFrpFilesAsync(CancellationToken cancellationToken = default)
    {
        var credential = GetConnectedCredential();
        if (credential is null)
        {
            ResetRemoteFrpDeleteConfirmation();
            return;
        }

        if (!_isRemoteFrpDeleteConfirmationPending)
        {
            _isRemoteFrpDeleteConfirmationPending = true;
            DeleteRemoteFrpFilesButtonText = "确认清理远程文件";
            SetDeploymentStatus(
                "请确认清理远程文件",
                $"将删除 {RemoteCorePath} 和 {RemoteServerConfigPath}。该操作不会删除本地节点记录。",
                "warning");
            return;
        }

        var directoryValidation = NodeConnectionWorkflowHelpers.ValidateRemoteCoreDirectory(RemoteCoreDirectory);
        if (!string.IsNullOrWhiteSpace(directoryValidation))
        {
            SetDeploymentStatus("远程目录无效", directoryValidation, "warning");
            ResetRemoteFrpDeleteConfirmation();
            return;
        }

        IsDeletingRemoteFrpFiles = true;
        DeleteRemoteFrpFilesButtonText = "正在清理...";
        SetDeploymentStatus("正在清理远程文件", $"正在清理 {Node.Name} 上的 frps 和 frps.toml。", "info");

        try
        {
            var result = await _remoteFileTransferService.DeleteRemoteFilesAsync(
                new RemoteFileDeleteRequest(Node, credential, [RemoteCorePath, RemoteServerConfigPath]),
                cancellationToken);

            if (result.Status == FrpNexusStatus.Error)
            {
                SetDeploymentStatus("清理失败", result.Message, "error");
                return;
            }

            HasDeploymentChanged = true;
            IsDeploymentReady = false;
            IsDeploymentPreparationVisible = true;
            RemoteFilePresenceText = "远程 frps 或 frps.toml 已被清理，需要重新上传部署文件。";
            await _deploymentRecordService.DeleteDeploymentRecordAsync(
                CreateCoreUploadStepName(result.NodeName),
                cancellationToken);
            SetDeploymentStatus(
                "清理完成",
                result.MissingPaths.Count > 0 ? "远程文件已清理，部分文件原本不存在。" : "已清理远程 frps 核心和 frps.toml。",
                result.MissingPaths.Count > 0 ? "warning" : "success");
        }
        catch (OperationCanceledException)
        {
            SetDeploymentStatus("清理已取消", "远程 FRP 文件清理已取消。", "warning");
        }
        catch (Exception ex)
        {
            SetDeploymentStatus("清理失败", ViewModelErrorText.ForUser("远程 FRP 文件清理", ex), "error");
        }
        finally
        {
            IsDeletingRemoteFrpFiles = false;
            ResetRemoteFrpDeleteConfirmation();
        }
    }

    [RelayCommand]
    private async Task ClearSavedSessionPasswordAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _nodeCredentialSecretService.DeleteSessionPasswordAsync(Node.Name, cancellationToken);
            HasSavedSessionPassword = false;
            RememberSessionPassword = false;
            SavedSessionPasswordText = "未保存会话密码。";
            SetConnectionStatus("已清除保存密码", "该节点已保存的会话密码已删除。", "info");
        }
        catch (OperationCanceledException)
        {
            SetConnectionStatus("清除已取消", "清除保存密码操作已取消。", "warning");
        }
        catch (Exception ex)
        {
            SetConnectionStatus("清除失败", ViewModelErrorText.ForUser("保存密码清除", ex), "error");
        }
    }

    [RelayCommand]
    private void Close()
    {
        _close(new NodeConnectionWorkflowResult(Node.Name, IsConnected, IsDeploymentReady, HasDeploymentChanged, HasDeploymentPresenceChecked));
    }

    [RelayCommand]
    private Task RefreshRemoteFrpsStatusAsync(CancellationToken cancellationToken = default)
    {
        return RefreshRuntimeStatusAsync(cancellationToken, showIdleStatus: true);
    }

    [RelayCommand]
    private Task StartRemoteFrpsAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteRemoteFrpsCommandAsync("启动", cancellationToken);
    }

    [RelayCommand]
    private Task StopRemoteFrpsAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteRemoteFrpsCommandAsync("停止", cancellationToken);
    }

    [RelayCommand]
    private Task RestartRemoteFrpsAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteRemoteFrpsCommandAsync("重启", cancellationToken);
    }

    private async Task<CredentialCreationResult> TryCreateCredentialReferenceAsync(CancellationToken cancellationToken)
    {
        var resolvedSessionPassword = SshSessionPassword;
        if (NodeConnectionWorkflowHelpers.TryParseAuthenticationMode(SelectedSshAuthenticationMode, out var mode)
            && mode == SshAuthenticationMode.SessionPassword
            && string.IsNullOrWhiteSpace(resolvedSessionPassword))
        {
            try
            {
                resolvedSessionPassword = await _nodeCredentialSecretService.GetSessionPasswordAsync(
                    Node.Name,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                SetConnectionStatus("读取保存密码失败", ViewModelErrorText.ForUser("保存密码读取", ex), "error");
                return CredentialCreationResult.Failed;
            }
        }

        return TryCreateCredentialReference(out var credential, resolvedSessionPassword)
            ? new CredentialCreationResult(true, credential)
            : CredentialCreationResult.Failed;
    }

    private bool TryCreateCredentialReference(out SshCredentialReference credential, string? resolvedSessionPassword)
    {
        credential = new SshCredentialReference(SshAuthenticationMode.SessionPassword);

        if (!NodeConnectionWorkflowHelpers.TryParseAuthenticationMode(SelectedSshAuthenticationMode, out var mode))
        {
            SetConnectionStatus("请选择认证方式", "请选择有效的 SSH 认证方式。", "warning");
            return false;
        }

        if (mode == SshAuthenticationMode.SshAgent)
        {
            SetConnectionStatus("暂未接入", "SSH Agent 认证暂未接入，请先使用会话密码或私钥文件。", "warning");
            return false;
        }

        if (mode == SshAuthenticationMode.SessionPassword && string.IsNullOrWhiteSpace(resolvedSessionPassword))
        {
            SetConnectionStatus("缺少会话密码", "请输入本次会话使用的 SSH 密码，或使用已保存的会话密码。", "warning");
            return false;
        }

        if (mode == SshAuthenticationMode.PrivateKey && string.IsNullOrWhiteSpace(SshPrivateKeyPath))
        {
            SetConnectionStatus("缺少私钥路径", "请输入私钥文件路径；私钥内容和 passphrase 不会保存到 SQLite。", "warning");
            return false;
        }

        credential = new SshCredentialReference(
            mode,
            string.IsNullOrWhiteSpace(SshPrivateKeyPath) ? null : SshPrivateKeyPath.Trim(),
            string.IsNullOrWhiteSpace(resolvedSessionPassword) ? null : resolvedSessionPassword,
            string.IsNullOrWhiteSpace(SshPrivateKeyPassphrase) ? null : SshPrivateKeyPassphrase);

        return true;
    }

    private SshCredentialReference? GetConnectedCredential()
    {
        var sessionSnapshot = _nodeConnectionSessionService.GetSessionStatus(Node.Name);
        if (sessionSnapshot.State != NodeConnectionSessionState.Online)
        {
            SetDeploymentStatus("请先连接节点", "部署操作需要先连接当前节点。", "warning");
            return null;
        }

        var credential = _nodeConnectionSessionService.GetConnectedCredential(Node.Name);
        if (credential is not null)
        {
            return credential;
        }

        SetDeploymentStatus("连接凭据不可用", "当前节点连接凭据已失效，请断开后重新连接。", "error");
        return null;
    }

    private async Task RefreshRuntimeStatusAsync(CancellationToken cancellationToken, bool showIdleStatus)
    {
        var credential = GetConnectedCredential();
        if (credential is null)
        {
            return;
        }

        IsRuntimeCommandRunning = true;
        if (showIdleStatus)
        {
            SetRuntimeStatus("正在刷新 frps", $"正在读取 {Node.Name} 的远程 frps 进程状态。", "info");
        }

        try
        {
            var processes = await _remoteRuntimeService.GetProcessesAsync(
                new RemoteRuntimeQueryRequest(Node, credential),
                cancellationToken);
            var displayProcess = processes.FirstOrDefault(process => string.Equals(process.ProcessKind, "frps", StringComparison.OrdinalIgnoreCase));

            if (displayProcess is null)
            {
                RemoteFrpsStatusText = "未运行";
                RemoteFrpsUptimeText = "-";
                SetRuntimeStatus("frps 未运行", "未发现远程 frps 进程。", "warning");
                return;
            }

            RemoteFrpsStatusText = "运行中";
            RemoteFrpsUptimeText = string.IsNullOrWhiteSpace(displayProcess.Uptime) ? "-" : displayProcess.Uptime;
            SetRuntimeStatus("frps 正在运行", "已刷新远程 frps 进程状态。", "success");
        }
        catch (OperationCanceledException)
        {
            SetRuntimeStatus("刷新已取消", "远程 frps 状态刷新已取消。", "warning");
        }
        catch (Exception ex)
        {
            SetRuntimeStatus("FRP 状态刷新失败", ViewModelErrorText.ForUser("FRP 运行状态刷新", ex), "warning");
        }
        finally
        {
            IsRuntimeCommandRunning = false;
        }
    }

    private async Task RefreshDeploymentPresenceAsync(CancellationToken cancellationToken)
    {
        var credential = GetConnectedCredential();
        if (credential is null)
        {
            return;
        }

        var directoryValidation = NodeConnectionWorkflowHelpers.ValidateRemoteCoreDirectory(RemoteCoreDirectory);
        if (!string.IsNullOrWhiteSpace(directoryValidation))
        {
            IsDeploymentReady = false;
            IsDeploymentPreparationVisible = true;
            RemoteFilePresenceText = directoryValidation;
            SetDeploymentStatus("远程目录无效", directoryValidation, "warning");
            return;
        }

        HasDeploymentPresenceChecked = false;
        IsDeploymentReady = false;
        IsDeploymentPreparationVisible = false;
        IsDeploymentPresenceChecking = true;
        SetDeploymentStatus("正在检查部署文件", $"正在检查 {RemoteCorePath} 和 {RemoteServerConfigPath}。", "info");

        try
        {
            var result = await _remoteFileTransferService.CheckRemoteFilesAsync(
                new RemoteFilePresenceRequest(Node, credential, [RemoteCorePath, RemoteServerConfigPath]),
                cancellationToken);

            if (result.Status == FrpNexusStatus.Error)
            {
                IsDeploymentReady = false;
                IsDeploymentPreparationVisible = false;
                RemoteFilePresenceText = "部署检查失败，请检查 SFTP 权限或重新连接后再部署。";
                SetDeploymentStatus("部署检查失败", result.Message, "error");
                return;
            }

            HasDeploymentPresenceChecked = true;

            var missingFiles = result.Files
                .Where(file => !file.Exists)
                .Select(file => Path.GetFileName(file.RemotePath))
                .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (missingFiles.Length == 0)
            {
                IsDeploymentReady = true;
                IsDeploymentPreparationVisible = false;
                RemoteFilePresenceText = "远程 frps 和 frps.toml 已就绪。";
                SetDeploymentStatus("部署文件已就绪", "远程 frps 和 frps.toml 已存在，可关闭弹窗或清理后重新部署。", "success");
                return;
            }

            IsDeploymentReady = false;
            IsDeploymentPreparationVisible = true;
            RemoteFilePresenceText = $"缺少：{string.Join("、", missingFiles)}。请补齐部署文件。";
            SetDeploymentStatus("需要部署准备", $"缺少：{string.Join("、", missingFiles)}。请选择目录并上传 frps / frps.toml。", "warning");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            IsDeploymentReady = false;
            IsDeploymentPreparationVisible = false;
            RemoteFilePresenceText = "部署检查失败，请检查 SFTP 权限或重新连接后再部署。";
            SetDeploymentStatus("部署检查失败", ViewModelErrorText.ForUser("远程部署文件检查", ex), "error");
        }
        finally
        {
            IsDeploymentPresenceChecking = false;
        }
    }

    private void ShowDeploymentPreparationFromKnownMissingState()
    {
        HasDeploymentPresenceChecked = true;
        IsDeploymentReady = false;
        IsDeploymentPresenceChecking = false;
        IsDeploymentPreparationVisible = true;
        RemoteFilePresenceText = "右侧面板已判断远程 frps 或 frps.toml 缺失，请补齐部署文件。";
        SetDeploymentStatus("需要部署准备", "请选择目录并上传 frps / frps.toml。", "warning");
    }

    private async Task ExecuteRemoteFrpsCommandAsync(string action, CancellationToken cancellationToken)
    {
        var credential = GetConnectedCredential();
        if (credential is null)
        {
            return;
        }

        var command = action switch
        {
            "启动" => BuildStartFrpsCommand(),
            "停止" => "pkill -f '[f]rps' || true",
            _ => $"pkill -f '[f]rps' || true; sleep 1; {BuildStartFrpsCommand()}"
        };

        IsRuntimeCommandRunning = true;
        SetRuntimeStatus($"正在{action} frps", $"正在 {Node.Name} 上执行远程 frps {action}。", "info");

        try
        {
            var request = new RemoteRuntimeCommandRequest(
                Node,
                credential,
                $"frps-{Node.Name}",
                "frps",
                command);

            var result = action switch
            {
                "启动" => await _remoteRuntimeService.StartAsync(request, cancellationToken),
                "停止" => await _remoteRuntimeService.StopAsync(request, cancellationToken),
                _ => await _remoteRuntimeService.RestartAsync(request, cancellationToken)
            };

            if (result.Status == FrpNexusStatus.Error)
            {
                SetRuntimeStatus($"{action}失败", result.Message, "error");
                return;
            }

            IsRuntimeCommandRunning = false;
            await RefreshRuntimeStatusAsync(cancellationToken, showIdleStatus: false);
            if (action is "启动" or "重启" && !string.Equals(RemoteFrpsStatusText, "运行中", StringComparison.Ordinal))
            {
                SetRuntimeStatus($"{action}失败", "远程命令已返回，但未发现 frps 进程，请查看启动日志。", "error");
                return;
            }

            SetRuntimeStatus($"{action}完成", result.Message, "success");
        }
        catch (OperationCanceledException)
        {
            SetRuntimeStatus($"{action}已取消", $"远程 frps {action}已取消。", "warning");
        }
        catch (Exception ex)
        {
            SetRuntimeStatus($"{action}失败", ViewModelErrorText.ForUser($"远程 frps {action}", ex), "error");
        }
        finally
        {
            IsRuntimeCommandRunning = false;
        }
    }

    private string BuildStartFrpsCommand()
    {
        return string.Join(
            " ",
            $"chmod +x {ShellQuote(RemoteCorePath)} &&",
            $"(nohup {ShellQuote(RemoteCorePath)} -c {ShellQuote(RemoteServerConfigPath)} >/tmp/frpnexus-frps.log 2>&1 &",
            "frps_pid=$!;",
            "sleep 1;",
            "if kill -0 \"$frps_pid\" 2>/dev/null && ps -p \"$frps_pid\" -o args= | grep -q '[f]rps'; then exit 0; fi;",
            "echo 'frps 启动后未保持运行。';",
            "tail -n 20 /tmp/frpnexus-frps.log 2>/dev/null || true;",
            "exit 1; )");
    }

    private async Task SaveRememberedSessionPasswordIfNeededAsync(
        string nodeName,
        string typedSessionPassword,
        CancellationToken cancellationToken)
    {
        if (!IsSessionPasswordMode || !RememberSessionPassword || string.IsNullOrWhiteSpace(typedSessionPassword))
        {
            return;
        }

        await _nodeCredentialSecretService.SaveSessionPasswordAsync(
            nodeName,
            typedSessionPassword,
            cancellationToken);
        HasSavedSessionPassword = true;
        SavedSessionPasswordText = "已保存会话密码，可直接连接。";
    }

    private async Task RefreshSavedSessionPasswordStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            HasSavedSessionPassword = await _nodeCredentialSecretService.HasSessionPasswordAsync(
                Node.Name,
                cancellationToken);
            SavedSessionPasswordText = HasSavedSessionPassword
                ? "已保存会话密码，可直接连接；明文不会回填显示。"
                : "未保存会话密码。";
            if (!HasSavedSessionPassword)
            {
                RememberSessionPassword = false;
            }
        }
        catch
        {
            HasSavedSessionPassword = false;
            SavedSessionPasswordText = "保存密码状态读取失败。";
        }
    }

    private async Task SaveLastUploadedCoreRecordAsync(RemoteFileTransferResult result, CancellationToken cancellationToken)
    {
        var record = new DeploymentRecord(
            CreateCoreUploadStepName(result.NodeName),
            result.NodeName,
            result.RemotePath,
            result.Status,
            result.CompletedAt);

        await _deploymentRecordService.SaveDeploymentRecordAsync(record, cancellationToken);
    }

    private string GetRemoteCoreFileName()
    {
        return string.IsNullOrWhiteSpace(SelectedLocalCorePath)
            ? NodeConnectionWorkflowHelpers.DefaultRemoteCoreFileName
            : SelectedLocalCoreFileName;
    }

    private static string CreateCoreUploadStepName(string nodeName)
    {
        return $"nodes/{nodeName}/frp-core-upload";
    }

    private void SetConnectionStatus(string title, string message, string severity)
    {
        ConnectionStatusTitle = title;
        ConnectionStatusText = message;
        ConnectionSeverity = severity;
    }

    private void SetDeploymentStatus(string title, string message, string severity)
    {
        DeploymentStatusTitle = title;
        DeploymentStatusText = message;
        DeploymentSeverity = severity;
    }

    private void SetRuntimeStatus(string title, string message, string severity)
    {
        RuntimeStatusTitle = title;
        RuntimeStatusText = message;
        RuntimeSeverity = severity;
    }

    private void ResetRemoteFrpDeleteConfirmation()
    {
        _isRemoteFrpDeleteConfirmationPending = false;
        DeleteRemoteFrpFilesButtonText = "清理远程文件";
    }

    private void ClearSessionSecrets()
    {
        SshSessionPassword = string.Empty;
        SshPrivateKeyPassphrase = string.Empty;
    }

    partial void OnSelectedSshAuthenticationModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsSessionPasswordMode));
        OnPropertyChanged(nameof(IsPrivateKeyMode));
        OnPropertyChanged(nameof(IsSshAgentMode));
        OnPropertyChanged(nameof(CanConnect));
    }

    partial void OnConnectionSeverityChanged(string value)
    {
        OnPropertyChanged(nameof(IsConnectionInfo));
        OnPropertyChanged(nameof(IsConnectionSuccess));
        OnPropertyChanged(nameof(IsConnectionWarning));
        OnPropertyChanged(nameof(IsConnectionError));
    }

    partial void OnDeploymentSeverityChanged(string value)
    {
        OnPropertyChanged(nameof(IsDeploymentInfo));
        OnPropertyChanged(nameof(IsDeploymentSuccess));
        OnPropertyChanged(nameof(IsDeploymentWarning));
        OnPropertyChanged(nameof(IsDeploymentError));
    }

    partial void OnRuntimeSeverityChanged(string value)
    {
        OnPropertyChanged(nameof(IsRuntimeInfo));
        OnPropertyChanged(nameof(IsRuntimeSuccess));
        OnPropertyChanged(nameof(IsRuntimeWarning));
        OnPropertyChanged(nameof(IsRuntimeError));
    }

    partial void OnIsConnectingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanConnect));
        OnPropertyChanged(nameof(ConnectButtonText));
        OnPropertyChanged(nameof(IsConnectionProgressVisible));
    }

    partial void OnIsConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanConnect));
        OnPropertyChanged(nameof(IsDeploymentVisible));
        OnPropertyChanged(nameof(ConnectionBadgeText));
        NotifyDeploymentCommandStateChanged();
        NotifyRuntimeCommandStateChanged();
    }

    partial void OnSelectedLocalCorePathChanged(string value)
    {
        var fileName = NodeConnectionWorkflowHelpers.GetLocalFileName(value);
        SelectedLocalCoreFileName = string.IsNullOrWhiteSpace(fileName) ? "尚未选择" : fileName;
        OnPropertyChanged(nameof(RemoteCorePath));
    }

    partial void OnRemoteCoreDirectoryChanged(string value)
    {
        OnPropertyChanged(nameof(RemoteCorePath));
        OnPropertyChanged(nameof(RemoteServerConfigPath));
        HasDeploymentPresenceChecked = false;
        IsDeploymentReady = false;
        IsDeploymentPreparationVisible = true;
        RemoteFilePresenceText = "远程目录已变更，请重新检查或上传部署文件。";
        ResetRemoteFrpDeleteConfirmation();
    }

    partial void OnIsDeploymentReadyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRemoteFileDeleteVisible));
        NotifyDeploymentCommandStateChanged();
    }

    partial void OnIsDeploymentPresenceCheckingChanged(bool value)
    {
        NotifyDeploymentCommandStateChanged();
        OnPropertyChanged(nameof(IsDeploymentStatusProgressVisible));
    }

    partial void OnIsUploadingCoreChanged(bool value)
    {
        NotifyDeploymentCommandStateChanged();
        OnPropertyChanged(nameof(UploadCoreButtonText));
        OnPropertyChanged(nameof(IsDeploymentStatusProgressVisible));
    }

    partial void OnIsUploadingServerTomlChanged(bool value)
    {
        NotifyDeploymentCommandStateChanged();
        OnPropertyChanged(nameof(UploadServerTomlButtonText));
        OnPropertyChanged(nameof(IsDeploymentStatusProgressVisible));
    }

    partial void OnIsDeletingRemoteFrpFilesChanged(bool value)
    {
        NotifyDeploymentCommandStateChanged();
        OnPropertyChanged(nameof(IsDeploymentStatusProgressVisible));
    }

    partial void OnIsRuntimeCommandRunningChanged(bool value)
    {
        NotifyRuntimeCommandStateChanged();
    }

    private void NotifyDeploymentCommandStateChanged()
    {
        OnPropertyChanged(nameof(IsAnyDeploymentOperationRunning));
        OnPropertyChanged(nameof(IsDeploymentStatusProgressVisible));
        OnPropertyChanged(nameof(CanPickLocalCoreFile));
        OnPropertyChanged(nameof(CanPickRemoteCoreDirectory));
        OnPropertyChanged(nameof(CanUploadCore));
        OnPropertyChanged(nameof(CanUploadServerToml));
        OnPropertyChanged(nameof(CanDeleteRemoteFrpFiles));
        OnPropertyChanged(nameof(IsRemoteFileDeleteVisible));
    }

    private void NotifyRuntimeCommandStateChanged()
    {
        OnPropertyChanged(nameof(CanRunRemoteFrpsCommand));
        OnPropertyChanged(nameof(IsRuntimeProgressVisible));
    }

    private static string ShellQuote(string value)
    {
        return $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
    }

    private sealed record CredentialCreationResult(bool Success, SshCredentialReference Credential)
    {
        public static CredentialCreationResult Failed { get; } =
            new(false, new SshCredentialReference(SshAuthenticationMode.SessionPassword));
    }
}
