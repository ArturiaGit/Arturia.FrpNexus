using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.Services;
using Arturia.FrpNexus.Desktop.ViewModels;
using Arturia.FrpNexus.Desktop.ViewModels.Nodes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed partial class NodesPageViewModel : PageViewModel
{
    public const string DefaultSshPort = "22";
    public const string DefaultUserName = "deploy";
    public const string DefaultAuthentication = "密钥 (LOCAL_KEY)";
    public const string DefaultOperatingSystem = "Ubuntu 22.04 LTS";
    public const string DefaultFrpVersion = "v0.61.1";
    public const string DefaultConfigPath = "/opt/frp/frps.toml";
    public const string OtherOperatingSystemOption = "其他";
    public const string SessionPasswordAuthenticationOption = "会话密码";
    public const string PrivateKeyAuthenticationOption = "私钥文件";
    public const string SshAgentAuthenticationOption = "SSH Agent（暂未接入）";
    public const string DefaultRemoteCoreDirectory = "/opt/frp";
    public const string DefaultRemoteCoreFileName = "frps";
    public const string LastUploadedCoreEmptyText = "上次上传：暂无记录";
    private static readonly TimeSpan RemoteFrpsVerificationInterval = TimeSpan.FromSeconds(30);

    private readonly INodeManagementService _nodeManagementService;
    private readonly INodeConnectionSessionService _nodeConnectionSessionService;
    private readonly IRemoteRuntimeService _remoteRuntimeService;
    private readonly IRemoteFileTransferService _remoteFileTransferService;
    private readonly ITomlConfigurationService _tomlConfigurationService;
    private readonly IFilePickerService _filePickerService;
    private readonly IRemoteDirectoryPickerService _remoteDirectoryPickerService;
    private readonly INodeCredentialSecretService _nodeCredentialSecretService;
    private readonly IDeploymentRecordService _deploymentRecordService;
    private readonly INodeConnectionWorkflowDialogService _nodeConnectionWorkflowDialogService;
    private readonly IConfirmationDialogService _confirmationDialogService;
    private readonly IFrpLifecycleStateService _frpLifecycleStateService;
    private readonly IRemoteFrpsRetentionService _remoteFrpsRetentionService;
    private readonly HashSet<string> _nodeConnectionOperations = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _deploymentPresenceCheckedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DeploymentPresenceStatusSnapshot> _deploymentPresenceStatusCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _deploymentPresenceCacheVersions = new(StringComparer.OrdinalIgnoreCase);
    private bool _isDeleteConfirmationPending;
    private bool _isRemoteFrpDeleteConfirmationPending;
    private CancellationTokenSource? _remoteFrpsUptimeTickerCts;
    private TimeSpan? _selectedFrpsUptimeBase;
    private DateTimeOffset? _selectedFrpsUptimeCapturedAt;
    private DateTimeOffset? _lastRemoteFrpsVerificationAt;
    private bool _isRemoteFrpsBackgroundRefreshRunning;
    private string? _editingOriginalName;

    [ObservableProperty]
    private NodeProfile? _selectedNode;

    [ObservableProperty]
    private string _nodeCountText = "共 0 个节点";

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private bool _isEditingExistingNode;

    [ObservableProperty]
    private string _editorTitle = "节点详情";

    [ObservableProperty]
    private string _formName = string.Empty;

    [ObservableProperty]
    private string _formHost = string.Empty;

    [ObservableProperty]
    private string _formSshPort = string.Empty;

    [ObservableProperty]
    private string _formUserName = string.Empty;

    [ObservableProperty]
    private string _formAuthentication = string.Empty;

    [ObservableProperty]
    private string _formOperatingSystem = DefaultOperatingSystem;

    [ObservableProperty]
    private string _selectedOperatingSystem = DefaultOperatingSystem;

    [ObservableProperty]
    private string _customOperatingSystem = string.Empty;

    [ObservableProperty]
    private bool _isCustomOperatingSystemSelected;

    [ObservableProperty]
    private string _formFrpVersion = string.Empty;

    [ObservableProperty]
    private string _formConfigPath = string.Empty;

    [ObservableProperty]
    private string _formErrorText = string.Empty;

    [ObservableProperty]
    private string _deleteButtonText = "删除";

    [ObservableProperty]
    private string _selectedSshAuthenticationMode = SessionPasswordAuthenticationOption;

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
    private string _connectionTestStatusText = "尚未连接 SSH 节点。";

    [ObservableProperty]
    private string _connectionTestTitle = "尚未连接";

    [ObservableProperty]
    private string _connectionTestSeverity = "info";

    [ObservableProperty]
    private bool _isTestingConnection;

    [ObservableProperty]
    private string _lastConnectionText = "最后连接：-";

    [ObservableProperty]
    private string _selectedLocalCorePath = string.Empty;

    [ObservableProperty]
    private string _selectedLocalCoreFileName = "尚未选择";

    [ObservableProperty]
    private string _remoteCoreDirectory = DefaultRemoteCoreDirectory;

    [ObservableProperty]
    private string _coreUploadStatusTitle = "尚未上传";

    [ObservableProperty]
    private string _coreUploadStatusText = "选择本地 frps 后上传到远程节点。";

    [ObservableProperty]
    private string _coreUploadSeverity = "info";

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
    private string _remoteFrpsStatusTitle = "尚未启动";

    [ObservableProperty]
    private string _remoteFrpsStatusText = "远程云服务器节点只运行 frps；本地 frpc 由隧道页启动。";

    [ObservableProperty]
    private string _remoteFrpsSeverity = "info";

    [ObservableProperty]
    private bool _isRemoteFrpsCommandRunning;

    [ObservableProperty]
    private bool _isSshSectionExpanded = true;

    [ObservableProperty]
    private bool _isDeploymentSectionExpanded;

    [ObservableProperty]
    private bool _isRuntimeSectionExpanded;

    [ObservableProperty]
    private string _lastUploadedCoreText = LastUploadedCoreEmptyText;

    [ObservableProperty]
    private string _lastUploadedCoreFullText = LastUploadedCoreEmptyText;

    [ObservableProperty]
    private bool _isNodeListEmpty = true;

    public NodesPageViewModel(
        INodeManagementService nodeManagementService,
        INodeConnectionSessionService nodeConnectionSessionService,
        IRemoteRuntimeService remoteRuntimeService,
        IRemoteFileTransferService remoteFileTransferService,
        ITomlConfigurationService tomlConfigurationService,
        IFilePickerService filePickerService,
        IRemoteDirectoryPickerService remoteDirectoryPickerService,
        INodeCredentialSecretService nodeCredentialSecretService,
        IDeploymentRecordService deploymentRecordService,
        INodeConnectionWorkflowDialogService? nodeConnectionWorkflowDialogService = null,
        IConfirmationDialogService? confirmationDialogService = null,
        IFrpLifecycleStateService? frpLifecycleStateService = null,
        IRemoteFrpsRetentionService? remoteFrpsRetentionService = null)
        : base("节点管理", "管理远程 Linux 节点并为 SSH/SFTP 工作流预留入口")
    {
        _nodeManagementService = nodeManagementService;
        _nodeConnectionSessionService = nodeConnectionSessionService;
        _remoteRuntimeService = remoteRuntimeService;
        _remoteFileTransferService = remoteFileTransferService;
        _tomlConfigurationService = tomlConfigurationService;
        _filePickerService = filePickerService;
        _remoteDirectoryPickerService = remoteDirectoryPickerService;
        _nodeCredentialSecretService = nodeCredentialSecretService;
        _deploymentRecordService = deploymentRecordService;
        _nodeConnectionWorkflowDialogService = nodeConnectionWorkflowDialogService
            ?? NoOpNodeConnectionWorkflowDialogService.Instance;
        _confirmationDialogService = confirmationDialogService
            ?? NoOpConfirmationDialogService.Instance;
        _frpLifecycleStateService = frpLifecycleStateService
            ?? NoOpFrpLifecycleStateService.Instance;
        _remoteFrpsRetentionService = remoteFrpsRetentionService
            ?? NoOpRemoteFrpsRetentionService.Instance;
        Nodes = [];
        NodeRows = [];

        _ = LoadNodesAsync();
    }

    public NodesPageViewModel(
        INodeManagementService nodeManagementService,
        ISshConnectionService sshConnectionService)
        : this(
            nodeManagementService,
            new LegacyNodeConnectionSessionService(sshConnectionService),
            new LegacyRemoteRuntimeService(),
            new LegacyRemoteFileTransferService(),
            new Arturia.FrpNexus.Application.Configuration.TomlConfigurationService(),
            new LegacyFilePickerService(),
            new LegacyRemoteDirectoryPickerService(),
            new LegacyNodeCredentialSecretService(),
            new LegacyDeploymentRecordService())
    {
    }

    [RelayCommand]
    private async Task PickLocalCoreFileAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var localPath = await _filePickerService.PickFrpBinaryAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(localPath))
            {
                SetCoreUploadStatus("已取消选择", "未选择本地 frps 文件。", "warning");
                return;
            }

            SelectedLocalCorePath = localPath;
            if (!IsFrpsFileName(SelectedLocalCoreFileName))
            {
                SetCoreUploadStatus("请选择 frps", "远程云服务器节点只需要上传 frps；frpc 将在本地由隧道页启动。", "warning");
                return;
            }

            SetCoreUploadStatus("已选择 frps 核心", $"本次上传文件：{SelectedLocalCoreFileName}，远程路径预览：{RemoteCorePath}", "info");
        }
        catch (OperationCanceledException)
        {
            SetCoreUploadStatus("已取消选择", "frps 文件选择已取消。", "warning");
        }
        catch (Exception ex)
        {
            SetCoreUploadStatus("选择文件失败", ViewModelErrorText.ForUser("FRP 核心文件选择", ex), "error");
        }
    }

    [RelayCommand]
    private async Task PickRemoteCoreDirectoryAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedNode is null)
        {
            SetCoreUploadStatus("请选择节点", "请先选择一个节点。", "warning");
            return;
        }

        var sessionSnapshot = _nodeConnectionSessionService.GetSessionStatus(SelectedNode.Name);
        if (sessionSnapshot.State != NodeConnectionSessionState.Online)
        {
            SetCoreUploadStatus("请先连接节点", "选择远程目录前需要先连接当前节点。", "warning");
            return;
        }

        var credential = _nodeConnectionSessionService.GetConnectedCredential(SelectedNode.Name);
        if (credential is null)
        {
            SetCoreUploadStatus("连接凭据不可用", "当前节点连接凭据已失效，请断开后重新连接。", "error");
            RefreshNodeRows();
            return;
        }

        var currentDirectory = RemoteCoreDirectory.Trim();
        if (string.IsNullOrWhiteSpace(currentDirectory) || !currentDirectory.StartsWith("/", StringComparison.Ordinal))
        {
            currentDirectory = DefaultRemoteCoreDirectory;
        }

        try
        {
            var selectedDirectory = await _remoteDirectoryPickerService.PickRemoteDirectoryAsync(
                SelectedNode,
                credential,
                currentDirectory,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(selectedDirectory))
            {
                SetCoreUploadStatus("已取消选择", "未选择远程目录。", "warning");
                return;
            }

            RemoteCoreDirectory = selectedDirectory;
            SetCoreUploadStatus("已选择远程目录", $"远程核心路径已更新为：{RemoteCorePath}", "info");
        }
        catch (OperationCanceledException)
        {
            SetCoreUploadStatus("已取消选择", "远程目录选择已取消。", "warning");
        }
        catch (Exception ex)
        {
            SetCoreUploadStatus("选择目录失败", ViewModelErrorText.ForUser("远程目录选择", ex), "error");
        }
    }

    [RelayCommand]
    private async Task TestSelectedNodeConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedNode is null)
        {
            SetConnectionTestStatus("请选择节点", "请先选择一个节点。", "warning");
            return;
        }

        var typedSessionPassword = SshSessionPassword;
        var credentialResult = await TryCreateCredentialReferenceAsync(cancellationToken);
        if (!credentialResult.Success)
        {
            return;
        }

        var credential = credentialResult.Credential;
        IsTestingConnection = true;
        SetConnectionTestStatus("正在连接 SSH 节点", $"正在连接 {SelectedNode.Name}...", "info");

        try
        {
            var result = await _nodeConnectionSessionService.ConnectAsync(
                SelectedNode,
                credential,
                cancellationToken);

            if (result.State == NodeConnectionSessionState.Online && result.ConnectedAt is not null)
            {
                await SaveRememberedSessionPasswordIfNeededAsync(
                    SelectedNode.Name,
                    typedSessionPassword,
                    cancellationToken);
                await _nodeManagementService.UpdateLastConnectionAsync(
                    SelectedNode.Name,
                    result.ConnectedAt.Value,
                    cancellationToken);
                UpdateNodeLastConnection(SelectedNode.Name, result.ConnectedAt.Value);
            }

            SetConnectionSessionResult(result);
            if (result.State == NodeConnectionSessionState.Online)
            {
                await RefreshSelectedDeploymentPresenceAsync(cancellationToken, force: false);
                await RefreshSelectedFrpRuntimeAsync(cancellationToken);
                ShowDeploymentSection();
            }

            RefreshNodeRows();
        }
        catch (OperationCanceledException)
        {
            SetConnectionTestStatus("连接已取消", "SSH 节点连接已取消。", "warning");
        }
        catch (Exception ex)
        {
            SetConnectionTestStatus("连接失败", ViewModelErrorText.ForUser("SSH 节点连接", ex), "error");
        }
        finally
        {
            ClearSessionSecrets();
            IsTestingConnection = false;
        }
    }

    [RelayCommand]
    private async Task DisconnectSelectedNodeAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedNode is null)
        {
            SetConnectionTestStatus("请选择节点", "请先选择一个节点。", "warning");
            return;
        }

        try
        {
            var result = await DisconnectNodeAsync(SelectedNode, cancellationToken);
            if (result is null)
            {
                SetConnectionTestStatus("断开已取消", "远程 frps 仍在运行，已取消断开 SSH。", "warning");
                return;
            }

            SetConnectionSessionResult(result);
            ClearSessionSecrets();
            ResetDeploymentPresenceForDisconnectedNode(clearCachedCheck: true);
            RefreshNodeRows();
        }
        catch (OperationCanceledException)
        {
            SetConnectionTestStatus("断开已取消", "SSH 节点断开操作已取消。", "warning");
        }
        catch (Exception ex)
        {
            SetConnectionTestStatus("断开失败", ViewModelErrorText.ForUser("SSH 节点断开", ex), "error");
        }
    }

    [RelayCommand]
    private Task RefreshDeploymentPresenceAsync(CancellationToken cancellationToken = default)
    {
        return RefreshSelectedDeploymentPresenceAsync(cancellationToken, force: true);
    }

    [RelayCommand]
    private async Task OpenDeploymentWorkflowAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedNode is null)
        {
            SetCoreUploadStatus("请选择节点", "请先选择一个节点。", "warning");
            return;
        }

        var nodeName = SelectedNode.Name;
        var session = _nodeConnectionSessionService.GetSessionStatus(nodeName);
        if (session.State != NodeConnectionSessionState.Online)
        {
            ResetDeploymentPresenceForDisconnectedNode();
            return;
        }

        try
        {
            var dialogResult = await _nodeConnectionWorkflowDialogService.ShowAsync(
                SelectedNode,
                NodeConnectionWorkflowOptions.DeployMissingFiles,
                cancellationToken);
            if (!string.Equals(dialogResult.NodeName, nodeName, StringComparison.OrdinalIgnoreCase)
                || SelectedNode is null
                || !string.Equals(SelectedNode.Name, nodeName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (dialogResult.DeploymentChanged)
            {
                await RefreshLastUploadedCoreStateAsync(nodeName, cancellationToken);
            }

            SyncDeploymentPresenceFromWorkflowResult(dialogResult);
            await RefreshSelectedFrpRuntimeAsync(cancellationToken);
            RefreshNodeRows();
        }
        catch (OperationCanceledException)
        {
            SetCoreUploadStatus("部署已取消", "部署准备弹窗已取消。", "warning");
        }
        catch (Exception ex)
        {
            SetCoreUploadStatus("部署入口打开失败", ViewModelErrorText.ForUser("部署准备", ex), "error");
        }
    }

    [RelayCommand]
    private async Task UploadCoreAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedNode is null)
        {
            SetCoreUploadStatus("请选择节点", "请先选择一个节点。", "warning");
            return;
        }

        var sessionSnapshot = _nodeConnectionSessionService.GetSessionStatus(SelectedNode.Name);
        if (sessionSnapshot.State != NodeConnectionSessionState.Online)
        {
            SetCoreUploadStatus("请先连接节点", "上传核心前需要先连接当前节点。", "warning");
            return;
        }

        var credential = _nodeConnectionSessionService.GetConnectedCredential(SelectedNode.Name);
        if (credential is null)
        {
            SetCoreUploadStatus("连接凭据不可用", "当前节点连接凭据已失效，请断开后重新连接。", "error");
            RefreshNodeRows();
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedLocalCorePath))
        {
            SetCoreUploadStatus("请选择 frps 核心", "请先选择本地 frps 文件，再执行上传。", "warning");
            return;
        }

        if (!IsFrpsFileName(SelectedLocalCoreFileName))
        {
            SetCoreUploadStatus("请选择 frps", "远程云服务器节点只支持上传 frps。frpc 会在本地通过隧道页启动。", "warning");
            return;
        }

        var directoryValidation = ValidateRemoteCoreDirectory(RemoteCoreDirectory);
        if (!string.IsNullOrWhiteSpace(directoryValidation))
        {
            SetCoreUploadStatus("远程目录无效", directoryValidation, "warning");
            return;
        }

        var fileNameValidation = ValidateRemoteCoreFileName(GetRemoteCoreFileName());
        if (!string.IsNullOrWhiteSpace(fileNameValidation))
        {
            SetCoreUploadStatus("远程文件名无效", fileNameValidation, "warning");
            return;
        }

        var remotePath = RemoteCorePath;

        IsUploadingCore = true;
        SetCoreUploadStatus("正在上传 frps 核心", $"正在上传到 {SelectedNode.Name}：{remotePath}", "info");

        try
        {
            var result = await _remoteFileTransferService.UploadFrpBinaryAsync(
                new RemoteFileUploadRequest(SelectedNode, credential, SelectedLocalCorePath, remotePath),
                cancellationToken);

            await SetCoreUploadResultAsync(result, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            SetCoreUploadStatus("上传已取消", "FRP 核心上传已取消。", "warning");
        }
        catch (Exception ex)
        {
            SetCoreUploadStatus("上传失败", ViewModelErrorText.ForUser("FRP 核心上传", ex), "error");
        }
        finally
        {
            IsUploadingCore = false;
        }
    }

    [RelayCommand]
    private async Task UploadServerTomlAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedNode is null)
        {
            SetCoreUploadStatus("请选择节点", "请先选择一个节点。", "warning");
            return;
        }

        var sessionSnapshot = _nodeConnectionSessionService.GetSessionStatus(SelectedNode.Name);
        if (sessionSnapshot.State != NodeConnectionSessionState.Online)
        {
            SetCoreUploadStatus("请先连接节点", "上传 frps.toml 前需要先连接当前节点。", "warning");
            return;
        }

        var credential = _nodeConnectionSessionService.GetConnectedCredential(SelectedNode.Name);
        if (credential is null)
        {
            SetCoreUploadStatus("连接凭据不可用", "当前节点连接凭据已失效，请断开后重新连接。", "error");
            RefreshNodeRows();
            return;
        }

        var directoryValidation = ValidateRemoteCoreDirectory(RemoteCoreDirectory);
        if (!string.IsNullOrWhiteSpace(directoryValidation))
        {
            SetCoreUploadStatus("远程目录无效", directoryValidation, "warning");
            return;
        }

        var remotePath = RemoteServerConfigPath;
        var toml = _tomlConfigurationService.GenerateServerToml(7000);

        IsUploadingServerToml = true;
        SetCoreUploadStatus("正在上传 frps.toml", $"已生成本地 frps.toml，正在上传到 {SelectedNode.Name}：{remotePath}", "info");

        try
        {
            var result = await _remoteFileTransferService.UploadConfigurationAsync(
                new RemoteConfigurationUploadRequest(SelectedNode, credential, toml, remotePath),
                cancellationToken);

            if (result.Status == FrpNexusStatus.Error)
            {
                SetCoreUploadStatus("frps.toml 上传失败", result.Message, "error");
                return;
            }

            SetCoreUploadStatus("frps.toml 上传成功", $"已上传 frps.toml 到 {result.RemotePath}。", "success");
            ShowDeploymentSection();
        }
        catch (OperationCanceledException)
        {
            SetCoreUploadStatus("上传已取消", "frps.toml 上传已取消。", "warning");
        }
        catch (Exception ex)
        {
            SetCoreUploadStatus("上传失败", ViewModelErrorText.ForUser("frps.toml 上传", ex), "error");
        }
        finally
        {
            IsUploadingServerToml = false;
        }
    }

    [RelayCommand]
    private async Task DeleteRemoteFrpFilesAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedNode is null)
        {
            SetCoreUploadStatus("请选择节点", "请先选择一个节点。", "warning");
            ResetRemoteFrpDeleteConfirmation();
            return;
        }

        var remoteCorePath = RemoteCorePath;
        var remoteServerConfigPath = RemoteServerConfigPath;
        if (!_isRemoteFrpDeleteConfirmationPending)
        {
            _isRemoteFrpDeleteConfirmationPending = true;
            DeleteRemoteFrpFilesButtonText = "确认清理远程文件";
            SetCoreUploadStatus(
                "请确认清理远程文件",
                $"将删除 {remoteCorePath} 和 {remoteServerConfigPath}；如果 frps 正在运行，会先停止再删除。",
                "warning");
            return;
        }

        var sessionSnapshot = _nodeConnectionSessionService.GetSessionStatus(SelectedNode.Name);
        if (sessionSnapshot.State != NodeConnectionSessionState.Online)
        {
            SetCoreUploadStatus("请先连接节点", "清理远程文件前需要先连接当前节点。", "warning");
            ResetRemoteFrpDeleteConfirmation();
            return;
        }

        var credential = _nodeConnectionSessionService.GetConnectedCredential(SelectedNode.Name);
        if (credential is null)
        {
            SetCoreUploadStatus("连接凭据不可用", "当前节点连接凭据已失效，请断开后重新连接。", "error");
            ResetRemoteFrpDeleteConfirmation();
            RefreshNodeRows();
            return;
        }

        var directoryValidation = ValidateRemoteCoreDirectory(RemoteCoreDirectory);
        if (!string.IsNullOrWhiteSpace(directoryValidation))
        {
            SetCoreUploadStatus("远程目录无效", directoryValidation, "warning");
            ResetRemoteFrpDeleteConfirmation();
            return;
        }

        IsDeletingRemoteFrpFiles = true;
        DeleteRemoteFrpFilesButtonText = "正在清理...";
        SetCoreUploadStatus("正在清理远程文件", $"正在清理 {SelectedNode.Name} 上的 frps 和 frps.toml。", "info");

        try
        {
            await RefreshSelectedFrpRuntimeAsync(cancellationToken);
            if (SelectedNode.FrpStatus == FrpNexusStatus.Running)
            {
                var stopResult = await StopRemoteFrpsForCleanupAsync(credential, cancellationToken);
                if (stopResult.Status == FrpNexusStatus.Error)
                {
                    SetCoreUploadStatus("清理失败", $"停止远程 frps 失败：{stopResult.Message}", "error");
                    return;
                }

                await RefreshSelectedFrpRuntimeAsync(cancellationToken);
            }

            var result = await _remoteFileTransferService.DeleteRemoteFilesAsync(
                new RemoteFileDeleteRequest(
                    SelectedNode,
                    credential,
                    [remoteCorePath, remoteServerConfigPath]),
                cancellationToken);

            if (result.Status == FrpNexusStatus.Error)
            {
                SetCoreUploadStatus("清理失败", result.Message, "error");
                return;
            }

            SetLastUploadedCoreEmpty();
            await _deploymentRecordService.DeleteDeploymentRecordAsync(
                CreateCoreUploadStepName(result.NodeName),
                cancellationToken);
            SetCoreUploadStatus(
                "清理完成",
                $"{result.Message} 目标：{remoteCorePath}，{remoteServerConfigPath}",
                result.MissingPaths.Count > 0 ? "warning" : "success");
            await RefreshSelectedFrpRuntimeAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            SetCoreUploadStatus("清理已取消", "远程 FRP 文件清理已取消。", "warning");
        }
        catch (Exception ex)
        {
            SetCoreUploadStatus("清理失败", ViewModelErrorText.ForUser("远程 FRP 文件清理", ex), "error");
        }
        finally
        {
            IsDeletingRemoteFrpFiles = false;
            ResetRemoteFrpDeleteConfirmation();
        }
    }

    public ObservableCollection<NodeProfile> Nodes { get; }

    public ObservableCollection<NodeListItemViewModel> NodeRows { get; }

    public bool IsSidePanelOpen => SelectedNode is not null || IsEditorOpen;

    public bool IsDetailsPanelOpen => SelectedNode is not null && !IsEditorOpen;

    public string DefaultSshPortText => DefaultSshPort;

    public string DefaultUserNameText => DefaultUserName;

    public string DefaultAuthenticationText => DefaultAuthentication;

    public string DefaultFrpVersionText => DefaultFrpVersion;

    public string DefaultConfigPathText => DefaultConfigPath;

    public IReadOnlyList<string> OperatingSystemOptions { get; } =
    [
        DefaultOperatingSystem,
        "Debian 12",
        "CentOS Stream 9",
        "AlmaLinux 9",
        "Rocky Linux 9",
        OtherOperatingSystemOption
    ];

    public IReadOnlyList<string> SshAuthenticationModeOptions { get; } =
    [
        SessionPasswordAuthenticationOption,
        PrivateKeyAuthenticationOption,
        SshAgentAuthenticationOption
    ];

    public bool IsSessionPasswordMode => TryParseAuthenticationMode(SelectedSshAuthenticationMode, out var mode)
        && mode == SshAuthenticationMode.SessionPassword;

    public bool IsPrivateKeyMode => TryParseAuthenticationMode(SelectedSshAuthenticationMode, out var mode)
        && mode == SshAuthenticationMode.PrivateKey;

    public bool IsSshAgentMode => TryParseAuthenticationMode(SelectedSshAuthenticationMode, out var mode)
        && mode == SshAuthenticationMode.SshAgent;

    public bool CanTestSelectedNodeConnection => SelectedNode is not null && !IsSshAgentMode;

    public bool CanRunConnectionTest => CanTestSelectedNodeConnection && !IsTestingConnection && !IsSelectedNodeOnline;

    public string ConnectionTestButtonText => IsTestingConnection ? "正在连接..." : "连接";

    public bool IsConnectionTestProgressVisible => IsTestingConnection;

    public bool IsSelectedNodeOnline => SelectedNode is not null
        && _nodeConnectionSessionService.GetSessionStatus(SelectedNode.Name).State == NodeConnectionSessionState.Online;

    public bool IsDeploymentStatusVisible => IsSelectedNodeOnline;

    public string SelectedDeploymentSummaryText => IsDeploymentPresenceChecking
        ? "正在检查"
        : IsSelectedNodeOnline
            ? CoreUploadStatusTitle
            : "连接后检查";

    public bool CanDisconnectSelectedNode => SelectedNode is not null && IsSelectedNodeOnline && !IsTestingConnection;

    public bool IsConnectionTestInfo => string.Equals(ConnectionTestSeverity, "info", StringComparison.OrdinalIgnoreCase);

    public bool IsConnectionTestSuccess => string.Equals(ConnectionTestSeverity, "success", StringComparison.OrdinalIgnoreCase);

    public bool IsConnectionTestWarning => string.Equals(ConnectionTestSeverity, "warning", StringComparison.OrdinalIgnoreCase);

    public bool IsConnectionTestError => string.Equals(ConnectionTestSeverity, "error", StringComparison.OrdinalIgnoreCase);

    public bool IsAnyFrpUploadRunning => IsUploadingCore || IsUploadingServerToml;

    public bool IsAnyFrpFileOperationRunning => IsAnyFrpUploadRunning || IsDeletingRemoteFrpFiles;

    public bool CanUploadCore => SelectedNode is not null && IsSelectedNodeOnline && !IsAnyFrpFileOperationRunning;

    public bool CanUploadServerToml => SelectedNode is not null && IsSelectedNodeOnline && !IsAnyFrpFileOperationRunning;

    public bool CanDeleteRemoteFrpFiles => SelectedNode is not null && IsSelectedNodeOnline && !IsAnyFrpFileOperationRunning;

    public bool CanPickRemoteCoreDirectory => SelectedNode is not null && IsSelectedNodeOnline && !IsAnyFrpFileOperationRunning;

    public bool CanRunRemoteFrpsCommand => SelectedNode is not null
        && IsSelectedNodeOnline
        && IsCoreUploadSuccess
        && !IsDeploymentPresenceChecking
        && !IsRemoteFrpsCommandRunning;

    public bool CanOpenDeploymentWorkflow => IsSelectedNodeOnline
        && !IsDeploymentPresenceChecking
        && string.Equals(CoreUploadSeverity, "warning", StringComparison.OrdinalIgnoreCase);

    public bool CanRefreshDeploymentPresence => IsSelectedNodeOnline && !IsDeploymentPresenceChecking;

    public bool CanClearSavedSessionPassword => SelectedNode is not null && HasSavedSessionPassword;

    public string RemoteCorePath => CombineRemotePath(RemoteCoreDirectory, DefaultRemoteCoreFileName);

    public string RemoteServerConfigPath => CombineRemotePath(RemoteCoreDirectory, "frps.toml");

    public string SelectedSshConnectionText
    {
        get
        {
            if (SelectedNode is null)
            {
                return "-";
            }

            return _nodeConnectionSessionService.GetSessionStatus(SelectedNode.Name).State switch
            {
                NodeConnectionSessionState.Online => "在线",
                NodeConnectionSessionState.Connecting => "连接中",
                NodeConnectionSessionState.Error => "错误",
                _ => "离线"
            };
        }
    }

    public string SelectedFrpUptimeText
    {
        get
        {
            if (SelectedNode is null)
            {
                return "-";
            }

            if (!IsSelectedNodeOnline)
            {
                return "未运行";
            }

            return SelectedNode.FrpStatus switch
            {
                FrpNexusStatus.Running => FormatSelectedFrpsUptime(),
                FrpNexusStatus.Stopped => "未运行",
                FrpNexusStatus.Warning => "刷新失败",
                FrpNexusStatus.Error => "异常",
                _ => "-"
            };
        }
    }

    public string RemoteFrpsToggleButtonText => IsRemoteFrpsCommandRunning
        ? "处理中..."
        : SelectedNode?.FrpStatus == FrpNexusStatus.Running
            ? "停止"
            : "启动";

    public string CoreUploadButtonText => IsUploadingCore ? "正在上传..." : "上传 frps 核心";

    public string ServerTomlUploadButtonText => IsUploadingServerToml ? "正在上传..." : "上传 frps.toml";

    public bool IsRemoteFrpsProgressVisible => IsRemoteFrpsCommandRunning;

    public bool IsRemoteFrpsInfo => string.Equals(RemoteFrpsSeverity, "info", StringComparison.OrdinalIgnoreCase);

    public bool IsRemoteFrpsSuccess => string.Equals(RemoteFrpsSeverity, "success", StringComparison.OrdinalIgnoreCase);

    public bool IsRemoteFrpsWarning => string.Equals(RemoteFrpsSeverity, "warning", StringComparison.OrdinalIgnoreCase);

    public bool IsRemoteFrpsError => string.Equals(RemoteFrpsSeverity, "error", StringComparison.OrdinalIgnoreCase);

    public bool IsCoreUploadProgressVisible => IsAnyFrpFileOperationRunning;

    public bool IsCoreUploadInfo => string.Equals(CoreUploadSeverity, "info", StringComparison.OrdinalIgnoreCase);

    public bool IsCoreUploadSuccess => string.Equals(CoreUploadSeverity, "success", StringComparison.OrdinalIgnoreCase);

    public bool IsCoreUploadWarning => string.Equals(CoreUploadSeverity, "warning", StringComparison.OrdinalIgnoreCase);

    public bool IsCoreUploadError => string.Equals(CoreUploadSeverity, "error", StringComparison.OrdinalIgnoreCase);

    public string SshCredentialHelpText
    {
        get
        {
            if (IsSessionPasswordMode)
            {
                return "请输入本次 SSH 连接使用的密码；勾选记住后会用 Windows DPAPI 加密保存。已保存密码不会回填显示，可直接连接。";
            }

            if (IsPrivateKeyMode)
            {
                return "请输入本机私钥文件路径；如果私钥有 passphrase，可在下方临时输入。私钥内容和 passphrase 不会保存。";
            }

            return "SSH Agent 认证暂未接入，请先使用会话密码或私钥文件路径。";
        }
    }

    public string SelectedFrpProcessText
    {
        get
        {
            if (SelectedNode is null)
            {
                return "-";
            }

            if (!IsSelectedNodeOnline)
            {
                return "离线，未刷新";
            }

            return SelectedNode.FrpStatus switch
            {
                FrpNexusStatus.Running => "frps 运行中",
                FrpNexusStatus.Stopped => "已停止",
                FrpNexusStatus.Warning => "警告",
                FrpNexusStatus.Error => "异常",
                _ => "未刷新"
            };
        }
    }

    public bool IsSelectedFrpSuccess => SelectedNode is not null
        && IsSelectedNodeOnline
        && SelectedNode.FrpStatus == FrpNexusStatus.Running;

    public bool IsSelectedFrpWarning => SelectedNode is not null
        && IsSelectedNodeOnline
        && SelectedNode.FrpStatus == FrpNexusStatus.Stopped;

    public bool IsSelectedFrpError => SelectedNode is not null
        && IsSelectedNodeOnline
        && SelectedNode.FrpStatus == FrpNexusStatus.Error;

    public bool IsSelectedFrpNeutral => !IsSelectedFrpSuccess && !IsSelectedFrpWarning && !IsSelectedFrpError;

    [RelayCommand]
    public async Task LoadNodesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<NodeProfile> nodes;
        try
        {
            nodes = await _nodeManagementService.ListNodesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            ConnectionTestStatusText = "节点列表加载已取消。";
            return;
        }
        catch (Exception ex)
        {
            NodeCountText = "节点加载失败";
            ConnectionTestStatusText = ViewModelErrorText.ForUser("节点列表加载", ex);
            return;
        }

        Nodes.Clear();
        NodeRows.Clear();
        foreach (var node in nodes)
        {
            Nodes.Add(node);
            NodeRows.Add(CreateNodeRow(node));
        }

        var selectedName = SelectedNode?.Name;
        SelectedNode = string.IsNullOrWhiteSpace(selectedName)
            ? null
            : Nodes.FirstOrDefault(node => string.Equals(node.Name, selectedName, StringComparison.OrdinalIgnoreCase));
        SyncSelectedRows();
        NotifySelectedSessionChanged();
        _ = RefreshSavedSessionPasswordStateAsync();
        NodeCountText = $"共 {Nodes.Count} 个节点";
        IsNodeListEmpty = Nodes.Count == 0;
        await RefreshLastUploadedCoreStateAsync(SelectedNode?.Name, cancellationToken);
        await RefreshSelectedFrpRuntimeAsync(cancellationToken);
    }

    [RelayCommand]
    private void SelectNode(NodeListItemViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        if (SelectedNode is not null
            && string.Equals(SelectedNode.Name, row.Node.Name, StringComparison.OrdinalIgnoreCase))
        {
            DismissSidePanelState();
            return;
        }

        SelectedNode = row.Node;
        IsEditorOpen = false;
    }

    [RelayCommand]
    private async Task ToggleNodeConnectionAsync(NodeListItemViewModel? row, CancellationToken cancellationToken = default)
    {
        if (row is null)
        {
            return;
        }

        var nodeName = row.Node.Name;
        if (!_nodeConnectionOperations.Add(nodeName))
        {
            return;
        }

        RefreshNodeRows();

        try
        {
            var session = _nodeConnectionSessionService.GetSessionStatus(nodeName);
            if (session.State == NodeConnectionSessionState.Online)
            {
                var result = await DisconnectNodeAsync(row.Node, cancellationToken);
                if (result is null)
                {
                    if (SelectedNode is not null
                        && string.Equals(SelectedNode.Name, nodeName, StringComparison.OrdinalIgnoreCase))
                    {
                        SetConnectionTestStatus("断开已取消", "远程 frps 仍在运行，已取消断开 SSH。", "warning");
                    }

                    return;
                }

                ClearDeploymentPresenceCacheForNode(nodeName);
                if (SelectedNode is not null
                    && string.Equals(SelectedNode.Name, nodeName, StringComparison.OrdinalIgnoreCase))
                {
                    SetConnectionSessionResult(result);
                    ClearSessionSecrets();
                    ResetDeploymentPresenceForDisconnectedNode();
                }

                return;
            }

            var dialogResult = await _nodeConnectionWorkflowDialogService.ShowAsync(
                row.Node,
                cancellationToken: cancellationToken);
            if (dialogResult.IsConnected)
            {
                var connectedAt = _nodeConnectionSessionService.GetSessionStatus(nodeName).ConnectedAt;
                if (connectedAt is not null)
                {
                    await _nodeManagementService.UpdateLastConnectionAsync(
                        nodeName,
                        connectedAt.Value,
                        cancellationToken);
                    UpdateNodeLastConnection(nodeName, connectedAt.Value);
                }

                if (SelectedNode is not null
                    && string.Equals(SelectedNode.Name, nodeName, StringComparison.OrdinalIgnoreCase))
                {
                    SetConnectionTestStatus("连接成功", "SSH 节点连接在线。", "success");
                    ShowDeploymentSection();
                    await RefreshLastUploadedCoreStateAsync(nodeName, cancellationToken);
                }

                SyncDeploymentPresenceFromWorkflowResultForNode(nodeName, dialogResult);
                await RefreshFrpRuntimeForNodeAsync(nodeName, cancellationToken);

                if (SelectedNode is not null
                    && string.Equals(SelectedNode.Name, nodeName, StringComparison.OrdinalIgnoreCase))
                {
                    NotifySelectedSessionChanged();
                }
            }

            if (dialogResult.DeploymentChanged
                && SelectedNode is not null
                && string.Equals(SelectedNode.Name, nodeName, StringComparison.OrdinalIgnoreCase))
            {
                await RefreshLastUploadedCoreStateAsync(nodeName, cancellationToken);
                SyncDeploymentPresenceFromWorkflowResultForNode(nodeName, dialogResult);
            }
        }
        catch (OperationCanceledException)
        {
            if (SelectedNode is not null
                && string.Equals(SelectedNode.Name, nodeName, StringComparison.OrdinalIgnoreCase))
            {
                SetConnectionTestStatus("操作已取消", "节点连接操作已取消。", "warning");
            }
        }
        catch (Exception ex)
        {
            if (SelectedNode is not null
                && string.Equals(SelectedNode.Name, nodeName, StringComparison.OrdinalIgnoreCase))
            {
                SetConnectionTestStatus("连接操作失败", ViewModelErrorText.ForUser("节点连接操作", ex), "error");
            }
        }
        finally
        {
            _nodeConnectionOperations.Remove(nodeName);
            RefreshNodeRows();
        }
    }

    [RelayCommand]
    private void StartCreateNode()
    {
        _editingOriginalName = null;
        ResetDeleteConfirmation();
        SelectedNode = null;
        IsEditingExistingNode = false;
        EditorTitle = "添加节点";
        FormName = string.Empty;
        FormHost = string.Empty;
        FormSshPort = string.Empty;
        FormUserName = string.Empty;
        FormAuthentication = string.Empty;
        FormOperatingSystem = DefaultOperatingSystem;
        SelectedOperatingSystem = DefaultOperatingSystem;
        CustomOperatingSystem = string.Empty;
        FormFrpVersion = string.Empty;
        FormConfigPath = string.Empty;
        RemoteCoreDirectory = DefaultRemoteCoreDirectory;
        FormErrorText = string.Empty;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void StartEditSelectedNode()
    {
        if (SelectedNode is null)
        {
            FormErrorText = "请先选择一个节点。";
            return;
        }

        _editingOriginalName = SelectedNode.Name;
        ResetDeleteConfirmation();
        IsEditingExistingNode = true;
        EditorTitle = "编辑节点";
        FormName = SelectedNode.Name;
        FormHost = SelectedNode.Host;
        FormSshPort = SelectedNode.SshPort.ToString();
        FormUserName = SelectedNode.UserName;
        FormAuthentication = SelectedNode.Authentication;
        FormOperatingSystem = SelectedNode.OperatingSystem;
        SetOperatingSystemSelection(SelectedNode.OperatingSystem);
        FormFrpVersion = SelectedNode.FrpVersion;
        FormConfigPath = SelectedNode.ConfigPath;
        RemoteCoreDirectory = ResolveFrpDirectoryFromConfigPath(SelectedNode.ConfigPath);
        FormErrorText = string.Empty;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private async Task SaveNodeAsync()
    {
        ResetDeleteConfirmation();

        if (!TryCreateNode(out var node))
        {
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(_editingOriginalName)
                && !string.Equals(_editingOriginalName, node.Name, System.StringComparison.OrdinalIgnoreCase))
            {
                await _nodeManagementService.DeleteNodeAsync(_editingOriginalName);
                await _nodeCredentialSecretService.DeleteSessionPasswordAsync(_editingOriginalName);
                ClearDeploymentPresenceCacheForNode(_editingOriginalName);
            }

            await _nodeManagementService.SaveNodeAsync(node);
            ClearDeploymentPresenceCacheForNode(node.Name);
            await LoadNodesAsync();

            SelectedNode = Nodes.FirstOrDefault(item => item.Name == node.Name);
            IsEditorOpen = false;
            IsEditingExistingNode = false;
            FormErrorText = string.Empty;
            _editingOriginalName = null;
        }
        catch (Exception ex)
        {
            FormErrorText = ViewModelErrorText.ForUser("节点保存", ex);
            IsEditorOpen = true;
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        ResetDeleteConfirmation();
        IsEditorOpen = false;
        IsEditingExistingNode = false;
        FormErrorText = string.Empty;
        _editingOriginalName = null;
    }

    [RelayCommand]
    private void DismissSidePanel()
    {
        DismissSidePanelState();
    }

    private void DismissSidePanelState()
    {
        ResetDeleteConfirmation();
        IsEditorOpen = false;
        IsEditingExistingNode = false;
        FormErrorText = string.Empty;
        _editingOriginalName = null;
        SelectedNode = null;
        SyncSelectedRows();
    }

    [RelayCommand]
    private async Task DeleteSelectedNodeAsync()
    {
        if (SelectedNode is null)
        {
            FormErrorText = "请先选择一个节点。";
            return;
        }

        if (!_isDeleteConfirmationPending)
        {
            _isDeleteConfirmationPending = true;
            DeleteButtonText = "确认删除";
            FormErrorText = $"将删除本地节点记录 `{SelectedNode.Name}`，再次点击确认。";
            return;
        }

        try
        {
            var deletedNodeName = SelectedNode.Name;
            await _nodeManagementService.DeleteNodeAsync(deletedNodeName);
            await _nodeCredentialSecretService.DeleteSessionPasswordAsync(deletedNodeName);
            await _deploymentRecordService.DeleteDeploymentRecordAsync(CreateCoreUploadStepName(deletedNodeName));
            ResetDeleteConfirmation();
            await LoadNodesAsync();
            IsEditorOpen = false;
            IsEditingExistingNode = false;
            FormErrorText = string.Empty;
        }
        catch (Exception ex)
        {
            FormErrorText = ViewModelErrorText.ForUser("节点删除", ex);
            ResetDeleteConfirmation();
        }
    }

    partial void OnSelectedNodeChanged(NodeProfile? value)
    {
        ResetDeleteConfirmation();
        ResetRemoteFrpDeleteConfirmation();
        ResetSelectedFrpsUptimeTracking();
        RemoteCoreDirectory = value is null
            ? DefaultRemoteCoreDirectory
            : ResolveFrpDirectoryFromConfigPath(value.ConfigPath);
        SetDetailsSectionForSelectedNode(value);
        SyncSelectedRows();
        _ = RefreshSavedSessionPasswordStateAsync();
        _ = RefreshLastUploadedCoreStateAsync(value?.Name);
        OnPropertyChanged(nameof(CanTestSelectedNodeConnection));
        OnPropertyChanged(nameof(CanRunConnectionTest));
        OnPropertyChanged(nameof(IsSelectedNodeOnline));
        OnPropertyChanged(nameof(CanDisconnectSelectedNode));
        OnPropertyChanged(nameof(CanUploadCore));
        OnPropertyChanged(nameof(CanUploadServerToml));
        OnPropertyChanged(nameof(CanDeleteRemoteFrpFiles));
        OnPropertyChanged(nameof(CanPickRemoteCoreDirectory));
        OnPropertyChanged(nameof(CanRunRemoteFrpsCommand));
        OnPropertyChanged(nameof(RemoteFrpsToggleButtonText));
        OnPropertyChanged(nameof(IsDeploymentStatusVisible));
        OnPropertyChanged(nameof(SelectedDeploymentSummaryText));
        OnPropertyChanged(nameof(CanOpenDeploymentWorkflow));
        OnPropertyChanged(nameof(CanRefreshDeploymentPresence));
        OnPropertyChanged(nameof(CanClearSavedSessionPassword));
        OnPropertyChanged(nameof(IsSidePanelOpen));
        OnPropertyChanged(nameof(IsDetailsPanelOpen));
        LastConnectionText = ResolveLastConnectionText(value?.Name);
        NotifySelectedFrpStatusChanged();
        SyncRemoteFrpsStatusFromNode(value);

        if (value is not null && IsSelectedNodeOnline)
        {
            _ = RefreshSelectedDeploymentPresenceAsync(force: false);
        }
        else
        {
            ResetDeploymentPresenceForDisconnectedNode();
        }
    }

    partial void OnIsEditorOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(IsSidePanelOpen));
        OnPropertyChanged(nameof(IsDetailsPanelOpen));
    }

    partial void OnSelectedOperatingSystemChanged(string value)
    {
        IsCustomOperatingSystemSelected = string.Equals(value, OtherOperatingSystemOption, StringComparison.Ordinal);
        if (!IsCustomOperatingSystemSelected)
        {
            CustomOperatingSystem = string.Empty;
            FormOperatingSystem = value;
        }
    }

    partial void OnSelectedSshAuthenticationModeChanged(string value)
    {
        if (IsSessionPasswordMode)
        {
            SshPrivateKeyPath = string.Empty;
            SshPrivateKeyPassphrase = string.Empty;
        }
        else if (IsPrivateKeyMode)
        {
            SshSessionPassword = string.Empty;
        }
        else if (IsSshAgentMode)
        {
            SshPrivateKeyPath = string.Empty;
            SshSessionPassword = string.Empty;
            SshPrivateKeyPassphrase = string.Empty;
            SetConnectionTestStatus("暂未接入", "SSH Agent 认证暂未接入，请先使用会话密码或私钥文件路径。", "warning");
        }

        OnPropertyChanged(nameof(IsSessionPasswordMode));
        OnPropertyChanged(nameof(IsPrivateKeyMode));
        OnPropertyChanged(nameof(IsSshAgentMode));
        OnPropertyChanged(nameof(CanTestSelectedNodeConnection));
        OnPropertyChanged(nameof(CanRunConnectionTest));
        OnPropertyChanged(nameof(CanDisconnectSelectedNode));
        OnPropertyChanged(nameof(CanUploadCore));
        OnPropertyChanged(nameof(CanUploadServerToml));
        OnPropertyChanged(nameof(CanDeleteRemoteFrpFiles));
        OnPropertyChanged(nameof(CanPickRemoteCoreDirectory));
        OnPropertyChanged(nameof(SshCredentialHelpText));
        OnPropertyChanged(nameof(CanClearSavedSessionPassword));
    }

    [RelayCommand]
    private async Task ClearSavedSessionPasswordAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedNode is null)
        {
            SetConnectionTestStatus("请选择节点", "请先选择一个节点。", "warning");
            return;
        }

        try
        {
            await _nodeCredentialSecretService.DeleteSessionPasswordAsync(SelectedNode.Name, cancellationToken);
            HasSavedSessionPassword = false;
            RememberSessionPassword = false;
            SavedSessionPasswordText = "未保存会话密码。";
            SetConnectionTestStatus("已清除保存密码", "该节点已保存的会话密码已删除。", "info");
            OnPropertyChanged(nameof(CanClearSavedSessionPassword));
        }
        catch (OperationCanceledException)
        {
            SetConnectionTestStatus("清除已取消", "清除保存密码操作已取消。", "warning");
        }
        catch (Exception ex)
        {
            SetConnectionTestStatus("清除失败", ViewModelErrorText.ForUser("保存密码清除", ex), "error");
        }
    }

    private NodeListItemViewModel CreateNodeRow(NodeProfile node)
    {
        return new NodeListItemViewModel(
            node,
            _nodeConnectionSessionService.GetSessionStatus(node.Name),
            _nodeConnectionOperations.Contains(node.Name));
    }

    private void UpdateNodeLastConnection(string nodeName, DateTimeOffset connectedAt)
    {
        for (var index = 0; index < Nodes.Count; index++)
        {
            if (!string.Equals(Nodes[index].Name, nodeName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var updated = Nodes[index] with { LastConnectionTestedAt = connectedAt };
            Nodes[index] = updated;
            if (SelectedNode is not null
                && string.Equals(SelectedNode.Name, nodeName, StringComparison.OrdinalIgnoreCase))
            {
                SelectedNode = updated;
            }

            return;
        }
    }

    private async Task RefreshSelectedFrpRuntimeAsync(CancellationToken cancellationToken)
    {
        await RefreshSelectedFrpRuntimeAsync(cancellationToken, allowStatusMessage: true);
    }

    private async Task RefreshSelectedFrpRuntimeAsync(CancellationToken cancellationToken, bool allowStatusMessage)
    {
        if (SelectedNode is null)
        {
            StopRemoteFrpsUptimeTicker();
            return;
        }

        await RefreshFrpRuntimeForNodeAsync(SelectedNode.Name, cancellationToken, allowStatusMessage);
    }

    private async Task RefreshFrpRuntimeForNodeAsync(
        string nodeName,
        CancellationToken cancellationToken,
        bool allowStatusMessage = true)
    {
        var node = Nodes.FirstOrDefault(item => string.Equals(item.Name, nodeName, StringComparison.OrdinalIgnoreCase));
        if (node is null)
        {
            return;
        }

        var session = _nodeConnectionSessionService.GetSessionStatus(nodeName);
        if (session.State != NodeConnectionSessionState.Online)
        {
            UpdateNodeFrpRuntime(nodeName, FrpNexusStatus.Stopped, "-");
            return;
        }

        var credential = _nodeConnectionSessionService.GetConnectedCredential(nodeName);
        if (credential is null)
        {
            if (allowStatusMessage)
            {
                SetConnectionTestStatus("FRP 状态刷新失败", "当前节点连接凭据已失效，请断开后重新连接。", "warning");
            }

            return;
        }

        try
        {
            var processes = await _remoteRuntimeService.GetProcessesAsync(
                new RemoteRuntimeQueryRequest(node, credential),
                cancellationToken);
            var match = ResolveRemoteFrpsRuntimeProcess(processes, node.ConfigPath);
            if (match.IsAmbiguous)
            {
                UpdateNodeFrpRuntime(nodeName, FrpNexusStatus.Warning, node.Uptime);
                if (allowStatusMessage)
                {
                    SetConnectionTestStatus(
                        "frps 状态需要确认",
                        "检测到多个远程 frps 进程，且无法按当前配置路径唯一匹配，请在节点页手动处理。",
                        "warning");
                }

                RefreshNodeRows();
                return;
            }

            var displayProcess = match.Process;

            var frpStatus = displayProcess is null ? FrpNexusStatus.Stopped : FrpNexusStatus.Running;
            var uptime = displayProcess is null || string.IsNullOrWhiteSpace(displayProcess.Uptime)
                ? "-"
                : displayProcess.Uptime;

            UpdateNodeFrpRuntime(nodeName, frpStatus, uptime);
            await _remoteFrpsRetentionService.ClearAsync(nodeName, cancellationToken);
            _lastRemoteFrpsVerificationAt = DateTimeOffset.UtcNow;
            RefreshNodeRows();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            UpdateNodeFrpRuntime(nodeName, FrpNexusStatus.Warning, node.Uptime);
            if (allowStatusMessage)
            {
                SetConnectionTestStatus("FRP 状态刷新失败", ViewModelErrorText.ForUser("FRP 运行状态刷新", ex), "warning");
            }

            RefreshNodeRows();
        }
    }

    private static RemoteFrpsRuntimeProcessMatch ResolveRemoteFrpsRuntimeProcess(
        IReadOnlyList<RuntimeProcess> processes,
        string configPath)
    {
        var frpsProcesses = processes
            .Where(process => string.Equals(process.ProcessKind, "frps", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (frpsProcesses.Length == 0)
        {
            return new RemoteFrpsRuntimeProcessMatch(null, false);
        }

        var matchedByConfig = frpsProcesses
            .Where(process => CommandUsesConfigPath(process.CommandLine, configPath))
            .ToArray();
        if (matchedByConfig.Length == 1)
        {
            return new RemoteFrpsRuntimeProcessMatch(matchedByConfig[0], false);
        }

        if (frpsProcesses.Length == 1)
        {
            return new RemoteFrpsRuntimeProcessMatch(frpsProcesses[0], false);
        }

        return new RemoteFrpsRuntimeProcessMatch(null, true);
    }

    private static bool CommandUsesConfigPath(string commandLine, string expectedConfigPath)
    {
        if (string.IsNullOrWhiteSpace(commandLine) || string.IsNullOrWhiteSpace(expectedConfigPath))
        {
            return false;
        }

        var normalizedCommand = NormalizeLinuxPathForCommandMatch(commandLine);
        var normalizedExpectedPath = NormalizeLinuxPathForCommandMatch(expectedConfigPath);
        return normalizedCommand.Contains(normalizedExpectedPath, StringComparison.Ordinal);
    }

    private static string NormalizeLinuxPathForCommandMatch(string value)
    {
        return value.Trim().Trim('"', '\'').Replace('\\', '/');
    }

    private sealed record RemoteFrpsRuntimeProcessMatch(RuntimeProcess? Process, bool IsAmbiguous);

    private async Task<NodeConnectionSessionResult?> DisconnectNodeAsync(
        NodeProfile node,
        CancellationToken cancellationToken)
    {
        if (!await ConfirmDisconnectRemoteFrpsAsync(node, cancellationToken))
        {
            return null;
        }

        return await _nodeConnectionSessionService.DisconnectAsync(node.Name, cancellationToken);
    }

    private async Task<bool> ConfirmDisconnectRemoteFrpsAsync(
        NodeProfile node,
        CancellationToken cancellationToken)
    {
        var sessionSnapshot = _nodeConnectionSessionService.GetSessionStatus(node.Name);
        if (sessionSnapshot.State != NodeConnectionSessionState.Online)
        {
            return true;
        }

        var credential = _nodeConnectionSessionService.GetConnectedCredential(node.Name);
        if (credential is null)
        {
            return true;
        }

        try
        {
            var processes = await _remoteRuntimeService.GetProcessesAsync(
                new RemoteRuntimeQueryRequest(node, credential),
                cancellationToken);
            if (!processes.Any(process =>
                string.Equals(process.ProcessKind, "frps", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            UpdateNodeFrpRuntime(node.Name, FrpNexusStatus.Running, "-");
            return await _confirmationDialogService.ShowAsync(
                new ConfirmationDialogRequest(
                    "远程 frps 仍在运行",
                    "断开 SSH 不会停止远程 frps。需要关闭服务时，请先点击“停止”。",
                    "继续断开",
                    "返回停止",
                    "warning"),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return true;
        }
    }

    private void UpdateNodeFrpRuntime(string nodeName, FrpNexusStatus frpStatus, string uptime)
    {
        for (var index = 0; index < Nodes.Count; index++)
        {
            if (!string.Equals(Nodes[index].Name, nodeName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var updated = Nodes[index] with
            {
                FrpStatus = frpStatus,
                Uptime = string.IsNullOrWhiteSpace(uptime) ? "-" : uptime
            };
            Nodes[index] = updated;
            _frpLifecycleStateService.UpdateRemoteFrpsState(
                nodeName,
                _nodeConnectionSessionService.GetSessionStatus(nodeName).State == NodeConnectionSessionState.Online,
                frpStatus,
                updated.ConfigPath);
            if (SelectedNode is not null
                && string.Equals(SelectedNode.Name, nodeName, StringComparison.OrdinalIgnoreCase))
            {
                SelectedNode = updated;
                UpdateSelectedFrpsUptimeTracking(updated);
                SyncRemoteFrpsStatusFromNode(updated);
            }

            NotifySelectedFrpStatusChanged();
            return;
        }
    }

    [RelayCommand]
    private Task ToggleRemoteFrpsAsync(CancellationToken cancellationToken = default)
    {
        ShowRuntimeSection();
        var action = SelectedNode?.FrpStatus == FrpNexusStatus.Running ? "停止" : "启动";
        return ExecuteRemoteFrpsCommandAsync(action, cancellationToken);
    }

    [RelayCommand]
    private Task StartRemoteFrpsAsync(CancellationToken cancellationToken = default)
    {
        ShowRuntimeSection();
        return ExecuteRemoteFrpsCommandAsync("启动", cancellationToken);
    }

    [RelayCommand]
    private Task StopRemoteFrpsAsync(CancellationToken cancellationToken = default)
    {
        ShowRuntimeSection();
        return ExecuteRemoteFrpsCommandAsync("停止", cancellationToken);
    }

    [RelayCommand]
    private Task RestartRemoteFrpsAsync(CancellationToken cancellationToken = default)
    {
        ShowRuntimeSection();
        return ExecuteRemoteFrpsCommandAsync("重启", cancellationToken);
    }

    [RelayCommand]
    private async Task RefreshRemoteFrpsStatusAsync(CancellationToken cancellationToken = default)
    {
        ShowRuntimeSection();
        if (SelectedNode is null)
        {
            SetRemoteFrpsStatus("请选择节点", "请先选择一个节点。", "warning");
            return;
        }

        IsRemoteFrpsCommandRunning = true;
        SetRemoteFrpsStatus("正在刷新 frps", $"正在读取 {SelectedNode.Name} 的远程 frps 进程状态。", "info");

        try
        {
            await RefreshSelectedFrpRuntimeAsync(cancellationToken);
            ShowRuntimeSection();
            SetRemoteFrpsStatus(
                SelectedNode.FrpStatus == FrpNexusStatus.Running ? "frps 正在运行" : "frps 未运行",
                SelectedNode.FrpStatus == FrpNexusStatus.Running ? "已刷新远程 frps 进程状态。" : "未发现远程 frps 进程。",
                SelectedNode.FrpStatus == FrpNexusStatus.Running ? "success" : "warning");
        }
        catch (OperationCanceledException)
        {
            SetRemoteFrpsStatus("刷新已取消", "远程 frps 状态刷新已取消。", "warning");
        }
        finally
        {
            IsRemoteFrpsCommandRunning = false;
        }
    }

    private async Task ExecuteRemoteFrpsCommandAsync(string action, CancellationToken cancellationToken)
    {
        if (SelectedNode is null)
        {
            SetRemoteFrpsStatus("请选择节点", "请先选择一个节点。", "warning");
            return;
        }

        var sessionSnapshot = _nodeConnectionSessionService.GetSessionStatus(SelectedNode.Name);
        if (sessionSnapshot.State != NodeConnectionSessionState.Online)
        {
            SetRemoteFrpsStatus("请先连接节点", $"{action} frps 前需要先连接当前节点。", "warning");
            return;
        }

        var credential = _nodeConnectionSessionService.GetConnectedCredential(SelectedNode.Name);
        if (credential is null)
        {
            SetRemoteFrpsStatus("连接凭据不可用", "当前节点连接凭据已失效，请断开后重新连接。", "error");
            return;
        }

        var command = action switch
        {
            "启动" => BuildStartFrpsCommand(),
            "停止" => "pkill -f '[f]rps' || true",
            _ => $"pkill -f '[f]rps' || true; sleep 1; {BuildStartFrpsCommand()}"
        };

        IsRemoteFrpsCommandRunning = true;
        SetRemoteFrpsStatus($"正在{action} frps", $"正在 {SelectedNode.Name} 上执行远程 frps {action}。", "info");

        try
        {
            var request = new RemoteRuntimeCommandRequest(
                SelectedNode,
                credential,
                $"frps-{SelectedNode.Name}",
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
                SetRemoteFrpsStatus($"{action}失败", result.Message, "error");
                await RefreshSelectedFrpRuntimeAsync(cancellationToken);
                return;
            }

            await RefreshSelectedFrpRuntimeAsync(cancellationToken);
            ShowRuntimeSection();
            if (action is "启动" or "重启" && SelectedNode.FrpStatus != FrpNexusStatus.Running)
            {
                SetRemoteFrpsStatus(
                    $"{action}失败",
                    "远程命令已返回，但未发现 frps 进程，请查看启动日志。",
                    "error");
                return;
            }

            SetRemoteFrpsStatus($"{action}完成", result.Message, "success");
        }
        catch (OperationCanceledException)
        {
            SetRemoteFrpsStatus($"{action}已取消", $"远程 frps {action}已取消。", "warning");
        }
        catch (Exception ex)
        {
            SetRemoteFrpsStatus($"{action}失败", ViewModelErrorText.ForUser($"远程 frps {action}", ex), "error");
        }
        finally
        {
            IsRemoteFrpsCommandRunning = false;
        }
    }

    private Task<RemoteRuntimeCommandResult> StopRemoteFrpsForCleanupAsync(
        SshCredentialReference credential,
        CancellationToken cancellationToken)
    {
        if (SelectedNode is null)
        {
            return Task.FromResult(new RemoteRuntimeCommandResult(
                string.Empty,
                "frps",
                FrpNexusStatus.Error,
                DateTimeOffset.UtcNow,
                "请先选择一个节点。"));
        }

        var request = new RemoteRuntimeCommandRequest(
            SelectedNode,
            credential,
            $"frps-{SelectedNode.Name}",
            "frps",
            "pkill -f '[f]rps' || true");

        return _remoteRuntimeService.StopAsync(request, cancellationToken);
    }

    private string BuildStartFrpsCommand()
    {
        var binaryPath = ResolveLastUploadedFrpsPath();
        var configPath = string.IsNullOrWhiteSpace(SelectedNode?.ConfigPath)
            ? DefaultConfigPath
            : SelectedNode.ConfigPath.Trim();

        return string.Join(
            " ",
            $"chmod +x {ShellQuote(binaryPath)} &&",
            $"(nohup {ShellQuote(binaryPath)} -c {ShellQuote(configPath)} >/tmp/frpnexus-frps.log 2>&1 &",
            "frps_pid=$!;",
            "sleep 1;",
            "if kill -0 \"$frps_pid\" 2>/dev/null && ps -p \"$frps_pid\" -o args= | grep -q '[f]rps'; then exit 0; fi;",
            "echo 'frps 启动后未保持运行。';",
            "tail -n 20 /tmp/frpnexus-frps.log 2>/dev/null || true;",
            "exit 1; )");
    }

    private string ResolveLastUploadedFrpsPath()
    {
        if (!string.IsNullOrWhiteSpace(LastUploadedCoreFullText))
        {
            var commaIndex = LastUploadedCoreFullText.IndexOf('，', StringComparison.Ordinal);
            var path = commaIndex > 0 ? LastUploadedCoreFullText[..commaIndex] : LastUploadedCoreFullText;
            if (path.StartsWith("/", StringComparison.Ordinal) && path.EndsWith("/frps", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return CombineRemotePath(DefaultRemoteCoreDirectory, DefaultRemoteCoreFileName);
    }

    private void RefreshNodeRows()
    {
        var selectedName = SelectedNode?.Name;
        NodeRows.Clear();
        foreach (var node in Nodes)
        {
            NodeRows.Add(CreateNodeRow(node));
        }

        if (!string.IsNullOrWhiteSpace(selectedName))
        {
            SelectedNode = Nodes.FirstOrDefault(node => string.Equals(node.Name, selectedName, StringComparison.OrdinalIgnoreCase));
        }

        SyncSelectedRows();
        NotifySelectedSessionChanged();
    }

    private void SyncSelectedRows()
    {
        foreach (var row in NodeRows)
        {
            row.IsSelected = SelectedNode is not null
                && string.Equals(row.Node.Name, SelectedNode.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    private bool TryCreateNode(out NodeProfile node)
    {
        node = new NodeProfile(string.Empty, string.Empty, 22, string.Empty, string.Empty, string.Empty, FrpNexusStatus.Offline, FrpNexusStatus.Stopped, string.Empty, "-", string.Empty);

        if (string.IsNullOrWhiteSpace(FormName))
        {
            FormErrorText = "节点名称不能为空。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(FormHost))
        {
            FormErrorText = "Host 不能为空。";
            return false;
        }

        var userName = ValueOrDefault(FormUserName, DefaultUserName);
        var sshPortText = ValueOrDefault(FormSshPort, DefaultSshPort);
        if (!int.TryParse(sshPortText, out var sshPort) || sshPort is < 1 or > 65535)
        {
            FormErrorText = "SSH 端口必须是 1 到 65535 之间的数字。";
            return false;
        }

        if (!TryResolveServerConfigPath(out var configPath))
        {
            return false;
        }

        node = new NodeProfile(
            FormName.Trim(),
            FormHost.Trim(),
            sshPort,
            userName,
            ValueOrDefault(FormAuthentication, DefaultAuthentication),
            ResolveOperatingSystem(),
            FrpNexusStatus.Offline,
            FrpNexusStatus.Stopped,
            ValueOrDefault(FormFrpVersion, DefaultFrpVersion),
            "-",
            configPath);

        return true;
    }

    private void SetOperatingSystemSelection(string operatingSystem)
    {
        if (OperatingSystemOptions.Contains(operatingSystem))
        {
            SelectedOperatingSystem = operatingSystem;
            CustomOperatingSystem = string.Empty;
            FormOperatingSystem = operatingSystem;
            return;
        }

        SelectedOperatingSystem = OtherOperatingSystemOption;
        CustomOperatingSystem = operatingSystem;
        FormOperatingSystem = operatingSystem;
    }

    private string ResolveOperatingSystem()
    {
        if (string.Equals(SelectedOperatingSystem, OtherOperatingSystemOption, StringComparison.Ordinal))
        {
            return ValueOrDefault(CustomOperatingSystem, "Linux");
        }

        return ValueOrDefault(SelectedOperatingSystem, DefaultOperatingSystem);
    }

    private static string ValueOrDefault(string value, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }

    private bool TryCreateCredentialReference(out SshCredentialReference credential)
    {
        return TryCreateCredentialReference(
            out credential,
            SshSessionPassword,
            (title, message, severity) => SetConnectionTestStatus(title, message, severity));
    }

    private bool TryCreateCredentialReference(
        out SshCredentialReference credential,
        string? resolvedSessionPassword,
        Action<string, string, string> setStatus)
    {
        credential = new SshCredentialReference(SshAuthenticationMode.SessionPassword);

        if (!TryParseAuthenticationMode(SelectedSshAuthenticationMode, out var mode))
        {
            setStatus("请选择认证方式", "请选择有效的 SSH 认证方式。", "warning");
            return false;
        }

        if (mode == SshAuthenticationMode.SessionPassword && string.IsNullOrWhiteSpace(resolvedSessionPassword))
        {
            setStatus("缺少会话密码", "请输入本次会话使用的 SSH 密码，或使用已保存的会话密码。", "warning");
            return false;
        }

        if (mode == SshAuthenticationMode.PrivateKey && string.IsNullOrWhiteSpace(SshPrivateKeyPath))
        {
            setStatus("缺少私钥路径", "请输入私钥文件路径，私钥内容和 passphrase 不会保存到 SQLite。", "warning");
            return false;
        }

        credential = new SshCredentialReference(
            mode,
            string.IsNullOrWhiteSpace(SshPrivateKeyPath) ? null : SshPrivateKeyPath.Trim(),
            string.IsNullOrWhiteSpace(resolvedSessionPassword) ? null : resolvedSessionPassword,
            string.IsNullOrWhiteSpace(SshPrivateKeyPassphrase) ? null : SshPrivateKeyPassphrase);

        return true;
    }

    private async Task<CredentialCreationResult> TryCreateCredentialReferenceAsync(CancellationToken cancellationToken)
    {
        var resolvedSessionPassword = SshSessionPassword;
        if (TryParseAuthenticationMode(SelectedSshAuthenticationMode, out var mode)
            && mode == SshAuthenticationMode.SessionPassword
            && string.IsNullOrWhiteSpace(resolvedSessionPassword)
            && SelectedNode is not null)
        {
            try
            {
                resolvedSessionPassword = await _nodeCredentialSecretService.GetSessionPasswordAsync(
                    SelectedNode.Name,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                SetConnectionTestStatus("读取保存密码失败", ViewModelErrorText.ForUser("保存密码读取", ex), "error");
                return CredentialCreationResult.Failed;
            }
        }

        return TryCreateCredentialReference(
            out var credential,
            resolvedSessionPassword,
            (title, message, severity) => SetConnectionTestStatus(title, message, severity))
            ? new CredentialCreationResult(true, credential)
            : CredentialCreationResult.Failed;
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
        OnPropertyChanged(nameof(CanClearSavedSessionPassword));
    }

    private async Task RefreshSavedSessionPasswordStateAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedNode is null)
        {
            HasSavedSessionPassword = false;
            RememberSessionPassword = false;
            SavedSessionPasswordText = "未保存会话密码。";
            OnPropertyChanged(nameof(CanClearSavedSessionPassword));
            return;
        }

        try
        {
            HasSavedSessionPassword = await _nodeCredentialSecretService.HasSessionPasswordAsync(
                SelectedNode.Name,
                cancellationToken);
            SavedSessionPasswordText = HasSavedSessionPassword
                ? "已保存会话密码，可直接连接。"
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
        finally
        {
            OnPropertyChanged(nameof(CanClearSavedSessionPassword));
        }
    }

    private async Task SetCoreUploadResultAsync(RemoteFileTransferResult result, CancellationToken cancellationToken)
    {
        if (result.Status is FrpNexusStatus.Ready or FrpNexusStatus.Online or FrpNexusStatus.Running)
        {
            await SaveLastUploadedCoreRecordAsync(result, cancellationToken);
            SetCoreUploadStatus("上传成功", $"{result.Message} 目标：{result.RemotePath}", "success");
            ShowDeploymentSection();
            return;
        }

        if (result.Message.Contains("不存在", StringComparison.Ordinal)
            || result.Message.Contains("路径", StringComparison.Ordinal))
        {
            SetCoreUploadStatus("上传失败", result.Message, "warning");
            return;
        }

        SetCoreUploadStatus("上传失败", result.Message, "error");
    }

    private async Task RefreshSelectedDeploymentPresenceAsync(CancellationToken cancellationToken = default, bool force = false)
    {
        var node = SelectedNode;
        if (node is null)
        {
            ResetDeploymentPresenceForDisconnectedNode();
            return;
        }

        var nodeName = node.Name;
        var sessionSnapshot = _nodeConnectionSessionService.GetSessionStatus(nodeName);
        if (sessionSnapshot.State != NodeConnectionSessionState.Online)
        {
            ResetDeploymentPresenceForDisconnectedNode();
            return;
        }

        var cacheKey = CreateDeploymentPresenceCacheKey(node);
        if (!force && _deploymentPresenceCheckedKeys.Contains(cacheKey))
        {
            RestoreDeploymentPresenceStatus(cacheKey);
            return;
        }

        var cacheVersionAtRequestStart = GetDeploymentPresenceCacheVersion(cacheKey);

        var credential = _nodeConnectionSessionService.GetConnectedCredential(nodeName);
        if (credential is null)
        {
            SetCoreUploadStatus("部署检查失败", "当前节点连接凭据已失效，请断开后重新连接。", "error");
            RefreshNodeRows();
            return;
        }

        var remoteCorePath = CombineRemotePath(RemoteCoreDirectory, DefaultRemoteCoreFileName);
        var remoteServerConfigPath = RemoteServerConfigPath;

        IsDeploymentPresenceChecking = true;
        SetCoreUploadStatus("正在检查", $"正在检查 {remoteCorePath} 和 {remoteServerConfigPath}。", "info");

        try
        {
            var result = await _remoteFileTransferService.CheckRemoteFilesAsync(
                new RemoteFilePresenceRequest(node, credential, [remoteCorePath, remoteServerConfigPath]),
                cancellationToken);

            if (SelectedNode is null
                || !string.Equals(SelectedNode.Name, nodeName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!force && TryRestoreNewerDeploymentPresenceStatus(cacheKey, cacheVersionAtRequestStart))
            {
                return;
            }

            if (result.Status == FrpNexusStatus.Error)
            {
                SetCoreUploadStatus("部署检查失败", result.Message, "error");
                CacheDeploymentPresenceStatus(cacheKey);
                return;
            }

            var missingFiles = result.Files
                .Where(file => !file.Exists)
                .Select(file => Path.GetFileName(file.RemotePath))
                .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (missingFiles.Length == 0)
            {
                SetCoreUploadStatus("部署文件已就绪", "远程 frps 和 frps.toml 已就绪。", "success");
                CacheDeploymentPresenceStatus(cacheKey);
                return;
            }

            SetCoreUploadStatus(
                "需要部署准备",
                $"缺少：{string.Join("、", missingFiles)}。请通过连接弹窗补齐部署文件。",
                "warning");
            CacheDeploymentPresenceStatus(cacheKey);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (!force && TryRestoreNewerDeploymentPresenceStatus(cacheKey, cacheVersionAtRequestStart))
            {
                return;
            }

            SetCoreUploadStatus("部署检查失败", ViewModelErrorText.ForUser("远程部署文件检查", ex), "error");
            CacheDeploymentPresenceStatus(cacheKey);
        }
        finally
        {
            IsDeploymentPresenceChecking = false;
        }
    }

    private void ResetDeploymentPresenceForDisconnectedNode(bool clearCachedCheck = false)
    {
        if (clearCachedCheck && SelectedNode is not null)
        {
            ClearDeploymentPresenceCacheForNode(SelectedNode.Name);
        }

        SetCoreUploadStatus("连接后检查", "SSH 连接后将检查远程 frps 和 frps.toml。", "info");
    }

    private void SetCoreUploadStatus(string title, string message, string severity)
    {
        CoreUploadStatusTitle = title;
        CoreUploadStatusText = message;
        CoreUploadSeverity = severity;
        OnPropertyChanged(nameof(CanRunRemoteFrpsCommand));
    }

    private static string CreateDeploymentPresenceCacheKey(NodeProfile node)
    {
        var normalizedConfigPath = NormalizeRemotePath(
            string.IsNullOrWhiteSpace(node.ConfigPath) ? DefaultConfigPath : node.ConfigPath);
        return $"{node.Name}|{normalizedConfigPath}";
    }

    private void ClearDeploymentPresenceCacheForNode(string nodeName)
    {
        _deploymentPresenceCheckedKeys.RemoveWhere(
            key => key.StartsWith($"{nodeName}|", StringComparison.OrdinalIgnoreCase));

        foreach (var key in _deploymentPresenceStatusCache.Keys
            .Where(key => key.StartsWith($"{nodeName}|", StringComparison.OrdinalIgnoreCase))
            .ToArray())
        {
            _deploymentPresenceStatusCache.Remove(key);
        }

        foreach (var key in _deploymentPresenceCacheVersions.Keys
            .Where(key => key.StartsWith($"{nodeName}|", StringComparison.OrdinalIgnoreCase))
            .ToArray())
        {
            _deploymentPresenceCacheVersions.Remove(key);
        }
    }

    private void CacheDeploymentPresenceStatus(string cacheKey)
    {
        CacheDeploymentPresenceStatus(cacheKey, CoreUploadStatusTitle, CoreUploadStatusText, CoreUploadSeverity);
    }

    private void CacheDeploymentPresenceStatus(string cacheKey, string title, string text, string severity)
    {
        _deploymentPresenceCheckedKeys.Add(cacheKey);
        _deploymentPresenceStatusCache[cacheKey] = new DeploymentPresenceStatusSnapshot(title, text, severity);
        _deploymentPresenceCacheVersions[cacheKey] = GetDeploymentPresenceCacheVersion(cacheKey) + 1;
    }

    private void RestoreDeploymentPresenceStatus(string cacheKey)
    {
        if (_deploymentPresenceStatusCache.TryGetValue(cacheKey, out var status))
        {
            SetCoreUploadStatus(status.Title, status.Text, status.Severity);
        }
    }

    private int GetDeploymentPresenceCacheVersion(string cacheKey)
    {
        return _deploymentPresenceCacheVersions.TryGetValue(cacheKey, out var version) ? version : 0;
    }

    private bool TryRestoreNewerDeploymentPresenceStatus(string cacheKey, int previousVersion)
    {
        if (GetDeploymentPresenceCacheVersion(cacheKey) <= previousVersion)
        {
            return false;
        }

        RestoreDeploymentPresenceStatus(cacheKey);
        return true;
    }

    private void SyncDeploymentPresenceFromWorkflowResult(NodeConnectionWorkflowResult result)
    {
        if (SelectedNode is null
            || !string.Equals(SelectedNode.Name, result.NodeName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!result.IsConnected)
        {
            ResetDeploymentPresenceForDisconnectedNode();
            return;
        }

        if (result.DeploymentReady)
        {
            SetCoreUploadStatus("部署文件已就绪", "远程 frps 和 frps.toml 已就绪。", "success");
            CacheDeploymentPresenceStatus(CreateDeploymentPresenceCacheKey(SelectedNode));
            return;
        }

        if (result.DeploymentChecked || result.DeploymentChanged)
        {
            SetCoreUploadStatus("需要部署准备", "远程 frps 或 frps.toml 缺失，请通过连接弹窗补齐部署文件。", "warning");
            CacheDeploymentPresenceStatus(CreateDeploymentPresenceCacheKey(SelectedNode));
        }
    }

    private void SyncDeploymentPresenceFromWorkflowResultForNode(string nodeName, NodeConnectionWorkflowResult result)
    {
        if (!result.IsConnected)
        {
            if (SelectedNode is not null
                && string.Equals(SelectedNode.Name, nodeName, StringComparison.OrdinalIgnoreCase))
            {
                ResetDeploymentPresenceForDisconnectedNode();
            }

            return;
        }

        var node = Nodes.FirstOrDefault(item => string.Equals(item.Name, nodeName, StringComparison.OrdinalIgnoreCase));
        if (node is null)
        {
            return;
        }

        var cacheKey = CreateDeploymentPresenceCacheKey(node);
        if (result.DeploymentReady)
        {
            CacheDeploymentPresenceStatus(cacheKey, "部署文件已就绪", "远程 frps 和 frps.toml 已就绪。", "success");
            RestoreDeploymentPresenceStatusIfSelected(nodeName, cacheKey);
            return;
        }

        if (result.DeploymentChecked || result.DeploymentChanged)
        {
            CacheDeploymentPresenceStatus(cacheKey, "需要部署准备", "远程 frps 或 frps.toml 缺失，请通过连接弹窗补齐部署文件。", "warning");
            RestoreDeploymentPresenceStatusIfSelected(nodeName, cacheKey);
        }
    }

    private void RestoreDeploymentPresenceStatusIfSelected(string nodeName, string cacheKey)
    {
        if (SelectedNode is not null
            && string.Equals(SelectedNode.Name, nodeName, StringComparison.OrdinalIgnoreCase))
        {
            RestoreDeploymentPresenceStatus(cacheKey);
        }
    }

    private void SetDetailsSectionForSelectedNode(NodeProfile? node)
    {
        if (node is null)
        {
            IsSshSectionExpanded = true;
            IsDeploymentSectionExpanded = false;
            IsRuntimeSectionExpanded = false;
            return;
        }

        if (node.ConnectionStatus != FrpNexusStatus.Offline && node.FrpStatus == FrpNexusStatus.Running)
        {
            ShowRuntimeSection();
            return;
        }

        if (_nodeConnectionSessionService.GetSessionStatus(node.Name).State == NodeConnectionSessionState.Online)
        {
            ShowDeploymentSection();
            return;
        }

        ShowSshSection();
    }

    private void ShowSshSection()
    {
        IsSshSectionExpanded = true;
        IsDeploymentSectionExpanded = false;
        IsRuntimeSectionExpanded = false;
    }

    private void ShowDeploymentSection()
    {
        IsSshSectionExpanded = false;
        IsDeploymentSectionExpanded = true;
        IsRuntimeSectionExpanded = false;
    }

    private void ShowRuntimeSection()
    {
        IsSshSectionExpanded = false;
        IsDeploymentSectionExpanded = false;
        IsRuntimeSectionExpanded = true;
    }

    private void SetConnectionSessionResult(NodeConnectionSessionResult result)
    {
        if (result.State == NodeConnectionSessionState.Online)
        {
            SyncRemoteFrpsLifecycleSnapshot(result.NodeName);
            SetConnectionTestStatus("连接成功", result.Message, "success");
            LastConnectionText = ResolveLastConnectionText(result.NodeName);
            return;
        }

        if (result.State == NodeConnectionSessionState.Disconnected)
        {
            SyncRemoteFrpsLifecycleSnapshot(result.NodeName);
            SetConnectionTestStatus("已断开", result.Message, "info");
            LastConnectionText = ResolveLastConnectionText(result.NodeName);
            return;
        }

        if (result.Message.Contains("认证失败", StringComparison.Ordinal))
        {
            SetConnectionTestStatus("认证失败", result.Message, "error");
            return;
        }

        if (result.Message.Contains("连接超时", StringComparison.Ordinal))
        {
            SetConnectionTestStatus("连接超时", result.Message, "error");
            return;
        }

        if (result.Message.Contains("连接失败", StringComparison.Ordinal))
        {
            SetConnectionTestStatus("连接失败", result.Message, "error");
            return;
        }

        if (result.Message.Contains("暂未接入", StringComparison.Ordinal))
        {
            SetConnectionTestStatus("暂未接入", result.Message, "warning");
            return;
        }

        SetConnectionTestStatus("连接失败", result.Message, "error");
        SyncRemoteFrpsLifecycleSnapshot(result.NodeName);
    }

    private void SyncRemoteFrpsLifecycleSnapshot(string nodeName)
    {
        var node = Nodes.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, nodeName, StringComparison.OrdinalIgnoreCase));
        var session = _nodeConnectionSessionService.GetSessionStatus(nodeName);
        if (node is null && session.State != NodeConnectionSessionState.Online)
        {
            _frpLifecycleStateService.RemoveRemoteFrpsState(nodeName);
            return;
        }

        _frpLifecycleStateService.UpdateRemoteFrpsState(
            nodeName,
            session.State == NodeConnectionSessionState.Online,
            node?.FrpStatus ?? FrpNexusStatus.Stopped,
            node?.ConfigPath ?? string.Empty);
    }

    private void SetConnectionTestStatus(string title, string message, string severity)
    {
        ConnectionTestTitle = title;
        ConnectionTestStatusText = message;
        ConnectionTestSeverity = severity;
    }

    partial void OnConnectionTestSeverityChanged(string value)
    {
        OnPropertyChanged(nameof(IsConnectionTestInfo));
        OnPropertyChanged(nameof(IsConnectionTestSuccess));
        OnPropertyChanged(nameof(IsConnectionTestWarning));
        OnPropertyChanged(nameof(IsConnectionTestError));
    }

    partial void OnHasSavedSessionPasswordChanged(bool value)
    {
        OnPropertyChanged(nameof(CanClearSavedSessionPassword));
    }

    partial void OnSelectedLocalCorePathChanged(string value)
    {
        var fileName = GetLocalFileName(value);
        SelectedLocalCoreFileName = string.IsNullOrWhiteSpace(fileName) ? "尚未选择" : fileName;
        OnPropertyChanged(nameof(RemoteCorePath));
    }

    partial void OnRemoteCoreDirectoryChanged(string value)
    {
        OnPropertyChanged(nameof(RemoteCorePath));
        OnPropertyChanged(nameof(RemoteServerConfigPath));
        ResetRemoteFrpDeleteConfirmation();
    }

    partial void OnIsTestingConnectionChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRunConnectionTest));
        OnPropertyChanged(nameof(ConnectionTestButtonText));
        OnPropertyChanged(nameof(IsConnectionTestProgressVisible));
        OnPropertyChanged(nameof(CanDisconnectSelectedNode));
    }

    partial void OnCoreUploadStatusTitleChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedDeploymentSummaryText));
    }

    partial void OnCoreUploadSeverityChanged(string value)
    {
        OnPropertyChanged(nameof(IsCoreUploadInfo));
        OnPropertyChanged(nameof(IsCoreUploadSuccess));
        OnPropertyChanged(nameof(IsCoreUploadWarning));
        OnPropertyChanged(nameof(IsCoreUploadError));
        OnPropertyChanged(nameof(CanOpenDeploymentWorkflow));
        OnPropertyChanged(nameof(CanRefreshDeploymentPresence));
        OnPropertyChanged(nameof(CanRunRemoteFrpsCommand));
    }

    partial void OnIsDeploymentPresenceCheckingChanged(bool value)
    {
        OnPropertyChanged(nameof(SelectedDeploymentSummaryText));
        OnPropertyChanged(nameof(CanOpenDeploymentWorkflow));
        OnPropertyChanged(nameof(CanRefreshDeploymentPresence));
        OnPropertyChanged(nameof(CanRunRemoteFrpsCommand));
    }

    partial void OnRemoteFrpsSeverityChanged(string value)
    {
        OnPropertyChanged(nameof(IsRemoteFrpsInfo));
        OnPropertyChanged(nameof(IsRemoteFrpsSuccess));
        OnPropertyChanged(nameof(IsRemoteFrpsWarning));
        OnPropertyChanged(nameof(IsRemoteFrpsError));
    }

    partial void OnIsUploadingCoreChanged(bool value)
    {
        OnPropertyChanged(nameof(IsAnyFrpUploadRunning));
        OnPropertyChanged(nameof(IsAnyFrpFileOperationRunning));
        OnPropertyChanged(nameof(CanUploadCore));
        OnPropertyChanged(nameof(CanUploadServerToml));
        OnPropertyChanged(nameof(CanDeleteRemoteFrpFiles));
        OnPropertyChanged(nameof(CanPickRemoteCoreDirectory));
        OnPropertyChanged(nameof(CoreUploadButtonText));
        OnPropertyChanged(nameof(ServerTomlUploadButtonText));
        OnPropertyChanged(nameof(DeleteRemoteFrpFilesButtonText));
        OnPropertyChanged(nameof(IsCoreUploadProgressVisible));
    }

    partial void OnIsUploadingServerTomlChanged(bool value)
    {
        OnPropertyChanged(nameof(IsAnyFrpUploadRunning));
        OnPropertyChanged(nameof(IsAnyFrpFileOperationRunning));
        OnPropertyChanged(nameof(CanUploadCore));
        OnPropertyChanged(nameof(CanUploadServerToml));
        OnPropertyChanged(nameof(CanDeleteRemoteFrpFiles));
        OnPropertyChanged(nameof(CanPickRemoteCoreDirectory));
        OnPropertyChanged(nameof(CoreUploadButtonText));
        OnPropertyChanged(nameof(ServerTomlUploadButtonText));
        OnPropertyChanged(nameof(DeleteRemoteFrpFilesButtonText));
        OnPropertyChanged(nameof(IsCoreUploadProgressVisible));
    }

    partial void OnIsDeletingRemoteFrpFilesChanged(bool value)
    {
        OnPropertyChanged(nameof(IsAnyFrpFileOperationRunning));
        OnPropertyChanged(nameof(CanUploadCore));
        OnPropertyChanged(nameof(CanUploadServerToml));
        OnPropertyChanged(nameof(CanDeleteRemoteFrpFiles));
        OnPropertyChanged(nameof(CanPickRemoteCoreDirectory));
        OnPropertyChanged(nameof(DeleteRemoteFrpFilesButtonText));
        OnPropertyChanged(nameof(IsCoreUploadProgressVisible));
    }

    partial void OnIsRemoteFrpsCommandRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRunRemoteFrpsCommand));
        OnPropertyChanged(nameof(IsRemoteFrpsProgressVisible));
        OnPropertyChanged(nameof(RemoteFrpsToggleButtonText));
    }

    private string ResolveLastConnectionText(string? nodeName)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            return "最后连接：-";
        }

        var node = Nodes.FirstOrDefault(item => string.Equals(item.Name, nodeName, StringComparison.OrdinalIgnoreCase));
        return node?.LastConnectionTestedAt is null
            ? "最后连接：-"
            : $"最后连接：{FormatLastConnection(node.LastConnectionTestedAt.Value, includeSeconds: true)}";
    }

    private static string FormatLastConnection(DateTimeOffset value, bool includeSeconds = false)
    {
        return value.ToLocalTime().ToString(includeSeconds ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm");
    }

    private async Task RefreshLastUploadedCoreStateAsync(string? nodeName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            SetLastUploadedCoreEmpty();
            return;
        }

        try
        {
            var record = await _deploymentRecordService.GetDeploymentRecordAsync(
                CreateCoreUploadStepName(nodeName),
                cancellationToken);

            if (record is null)
            {
                SetLastUploadedCoreEmpty();
                return;
            }

            SetLastUploadedCore(record);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            LastUploadedCoreText = "上次上传：读取失败";
            LastUploadedCoreFullText = "上次上传记录读取失败。";
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
        SetLastUploadedCore(record);
    }

    private void SetLastUploadedCore(DeploymentRecord record)
    {
        var uploadedAt = FormatLastConnection(record.UpdatedAt);
        LastUploadedCoreText = $"上次上传：{uploadedAt}";
        LastUploadedCoreFullText = $"{record.Description}，{FormatLastConnection(record.UpdatedAt, includeSeconds: true)}";
    }

    private void SetLastUploadedCoreEmpty()
    {
        LastUploadedCoreText = LastUploadedCoreEmptyText;
        LastUploadedCoreFullText = LastUploadedCoreEmptyText;
    }

    private static string CreateCoreUploadStepName(string nodeName)
    {
        return $"nodes/{nodeName}/frp-core-upload";
    }

    private void NotifySelectedSessionChanged()
    {
        OnPropertyChanged(nameof(IsSelectedNodeOnline));
        OnPropertyChanged(nameof(SelectedSshConnectionText));
        OnPropertyChanged(nameof(CanRunConnectionTest));
        OnPropertyChanged(nameof(CanDisconnectSelectedNode));
        OnPropertyChanged(nameof(CanUploadCore));
        OnPropertyChanged(nameof(CanUploadServerToml));
        OnPropertyChanged(nameof(CanDeleteRemoteFrpFiles));
        OnPropertyChanged(nameof(CanPickRemoteCoreDirectory));
        OnPropertyChanged(nameof(CanRunRemoteFrpsCommand));
        OnPropertyChanged(nameof(RemoteFrpsToggleButtonText));
        OnPropertyChanged(nameof(IsDeploymentStatusVisible));
        OnPropertyChanged(nameof(SelectedDeploymentSummaryText));
        OnPropertyChanged(nameof(CanOpenDeploymentWorkflow));
        OnPropertyChanged(nameof(CanRefreshDeploymentPresence));
        LastConnectionText = ResolveLastConnectionText(SelectedNode?.Name);
    }

    private void ClearSessionSecrets()
    {
        SshSessionPassword = string.Empty;
        SshPrivateKeyPassphrase = string.Empty;
    }

    private static bool TryParseAuthenticationMode(string value, out SshAuthenticationMode mode)
    {
        if (string.Equals(value, "PrivateKey", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "私钥文件", System.StringComparison.OrdinalIgnoreCase))
        {
            mode = SshAuthenticationMode.PrivateKey;
            return true;
        }

        if (string.Equals(value, "SshAgent", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "SSH Agent", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, SshAgentAuthenticationOption, System.StringComparison.OrdinalIgnoreCase))
        {
            mode = SshAuthenticationMode.SshAgent;
            return true;
        }

        if (string.Equals(value, "SessionPassword", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "会话密码", System.StringComparison.OrdinalIgnoreCase))
        {
            mode = SshAuthenticationMode.SessionPassword;
            return true;
        }

        mode = SshAuthenticationMode.SessionPassword;
        return false;
    }

    private static string CombineRemotePath(string directory, string fileName)
    {
        var normalizedDirectory = NormalizeRemotePath(directory);
        var normalizedFileName = string.IsNullOrWhiteSpace(fileName) ? DefaultRemoteCoreFileName : fileName.Trim();
        return string.Equals(normalizedDirectory, "/", StringComparison.Ordinal)
            ? $"/{normalizedFileName}"
            : $"{normalizedDirectory.TrimEnd('/')}/{normalizedFileName}";
    }

    private static string NormalizeRemotePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var normalized = path.Trim().Replace('\\', '/');
        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = "/" + normalized;
        }

        return normalized.Length > 1 ? normalized.TrimEnd('/') : normalized;
    }

    private static string ResolveServerConfigPathFromRemoteDirectory(string directory)
    {
        return CombineRemotePath(
            string.IsNullOrWhiteSpace(directory) ? DefaultRemoteCoreDirectory : directory,
            "frps.toml");
    }

    private bool TryResolveServerConfigPath(out string configPath)
    {
        configPath = NormalizeRemotePath(string.IsNullOrWhiteSpace(FormConfigPath) ? DefaultConfigPath : FormConfigPath);
        var validationMessage = ValidateServerConfigPath(configPath);
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            FormErrorText = validationMessage;
            return false;
        }

        return true;
    }

    private static string ResolveFrpDirectoryFromConfigPath(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return DefaultRemoteCoreDirectory;
        }

        var normalized = NormalizeRemotePath(configPath);
        var lastSlash = normalized.LastIndexOf('/');
        if (lastSlash <= 0)
        {
            return DefaultRemoteCoreDirectory;
        }

        return normalized[..lastSlash];
    }

    private static string GetLocalFileName(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFileName(path.Trim());
        }
        catch
        {
            var normalized = path.Trim().Replace('\\', '/');
            var lastSlash = normalized.LastIndexOf('/');
            return lastSlash >= 0 ? normalized[(lastSlash + 1)..] : normalized;
        }
    }

    private static bool IsFrpsFileName(string fileName)
    {
        return string.Equals(fileName, "frps", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "frps.exe", StringComparison.OrdinalIgnoreCase);
    }

    private string GetRemoteCoreFileName()
    {
        return string.IsNullOrWhiteSpace(SelectedLocalCorePath)
            ? DefaultRemoteCoreFileName
            : SelectedLocalCoreFileName;
    }

    private static string ValidateRemoteCoreDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return "远程目录不能为空，例如 /opt/frp。";
        }

        var trimmed = directory.Trim();
        if (!trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            return "远程目录必须是 Linux 绝对路径，例如 /opt/frp。";
        }

        if (trimmed.Contains('\0'))
        {
            return "远程目录不能包含空字符。";
        }

        return string.Empty;
    }

    private static string ValidateServerConfigPath(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return "配置路径不能为空，例如 /etc/frp/frps.toml。";
        }

        if (!configPath.StartsWith("/", StringComparison.Ordinal))
        {
            return "配置路径必须是 Linux 绝对路径，例如 /etc/frp/frps.toml。";
        }

        if (configPath.Contains('\0'))
        {
            return "配置路径不能包含空字符。";
        }

        var lastSlash = configPath.LastIndexOf('/');
        if (lastSlash <= 0 || lastSlash == configPath.Length - 1)
        {
            return "配置路径必须包含文件名，例如 /etc/frp/frps.toml。";
        }

        return string.Empty;
    }

    private static string ValidateRemoteCoreFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "远程文件名不能为空，例如 frps。";
        }

        if (fileName.Contains('/', StringComparison.Ordinal)
            || fileName.Contains('\\', StringComparison.Ordinal)
            || fileName.Contains('\0'))
        {
            return "远程文件名不能包含路径分隔符，请只填写文件名 frps。";
        }

        if (!IsFrpsFileName(fileName))
        {
            return "远程云服务器节点只支持上传 frps。";
        }

        return string.Empty;
    }

    private void SetRemoteFrpsStatus(string title, string message, string severity)
    {
        RemoteFrpsStatusTitle = title;
        RemoteFrpsStatusText = message;
        RemoteFrpsSeverity = severity;
    }

    private void SyncRemoteFrpsStatusFromNode(NodeProfile? node)
    {
        if (node is null)
        {
            SetRemoteFrpsStatus("尚未启动", "远程云服务器节点只运行 frps；本地 frpc 由隧道页启动。", "info");
            return;
        }

        if (_nodeConnectionSessionService.GetSessionStatus(node.Name).State != NodeConnectionSessionState.Online)
        {
            SetRemoteFrpsStatus("尚未连接", "连接 SSH 节点后将刷新远程 frps 状态。", "info");
            return;
        }

        switch (node.FrpStatus)
        {
            case FrpNexusStatus.Running:
                SetRemoteFrpsStatus("frps 运行中", "远程 frps 已接入运行状态；本地 frpc 请在隧道页启动和管理。", "success");
                break;
            case FrpNexusStatus.Stopped:
                SetRemoteFrpsStatus("frps 未运行", "未发现远程 frps 进程；部署文件就绪后可启动。", "warning");
                break;
            case FrpNexusStatus.Warning:
                SetRemoteFrpsStatus("frps 状态需要确认", "远程 frps 状态刷新失败或存在多个进程，请重试刷新或手动处理。", "warning");
                break;
            case FrpNexusStatus.Error:
                SetRemoteFrpsStatus("frps 状态异常", "远程 frps 操作失败，请查看日志并重试。", "error");
                break;
            default:
                SetRemoteFrpsStatus("frps 状态未知", "尚未获得远程 frps 运行状态。", "info");
                break;
        }
    }

    private string FormatSelectedFrpsUptime()
    {
        if (_selectedFrpsUptimeBase is null || _selectedFrpsUptimeCapturedAt is null)
        {
            return string.IsNullOrWhiteSpace(SelectedNode?.Uptime) || SelectedNode.Uptime == "-"
                ? "刚刚启动"
                : SelectedNode.Uptime;
        }

        var elapsed = DateTimeOffset.UtcNow - _selectedFrpsUptimeCapturedAt.Value;
        return FormatFrpsUptime(_selectedFrpsUptimeBase.Value + elapsed);
    }

    private void UpdateSelectedFrpsUptimeTracking(NodeProfile node)
    {
        if (node.FrpStatus != FrpNexusStatus.Running || !IsSelectedNodeOnline)
        {
            ResetSelectedFrpsUptimeTracking();
            return;
        }

        _selectedFrpsUptimeBase = TryParseFrpsUptime(node.Uptime, out var uptime)
            ? uptime
            : TimeSpan.Zero;
        _selectedFrpsUptimeCapturedAt = DateTimeOffset.UtcNow;
        _lastRemoteFrpsVerificationAt = DateTimeOffset.UtcNow;
        EnsureRemoteFrpsUptimeTicker();
    }

    private void ResetSelectedFrpsUptimeTracking()
    {
        _selectedFrpsUptimeBase = null;
        _selectedFrpsUptimeCapturedAt = null;
        _lastRemoteFrpsVerificationAt = null;
        StopRemoteFrpsUptimeTicker();
        OnPropertyChanged(nameof(SelectedFrpUptimeText));
    }

    private void EnsureRemoteFrpsUptimeTicker()
    {
        if (_remoteFrpsUptimeTickerCts is not null)
        {
            return;
        }

        _remoteFrpsUptimeTickerCts = new CancellationTokenSource();
        _ = RunRemoteFrpsUptimeTickerAsync(_remoteFrpsUptimeTickerCts.Token);
    }

    private void StopRemoteFrpsUptimeTicker()
    {
        var cts = _remoteFrpsUptimeTickerCts;
        if (cts is null)
        {
            return;
        }

        _remoteFrpsUptimeTickerCts = null;
        cts.Cancel();
        cts.Dispose();
    }

    private async Task RunRemoteFrpsUptimeTickerAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                RaiseOnUiThread(() => OnPropertyChanged(nameof(SelectedFrpUptimeText)));

                if (ShouldVerifyRemoteFrpsRuntime())
                {
                    _ = RefreshSelectedFrpRuntimeInBackgroundAsync(cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private bool ShouldVerifyRemoteFrpsRuntime()
    {
        return SelectedNode?.FrpStatus == FrpNexusStatus.Running
            && IsSelectedNodeOnline
            && !IsRemoteFrpsCommandRunning
            && !_isRemoteFrpsBackgroundRefreshRunning
            && (_lastRemoteFrpsVerificationAt is null
                || DateTimeOffset.UtcNow - _lastRemoteFrpsVerificationAt.Value >= RemoteFrpsVerificationInterval);
    }

    private async Task RefreshSelectedFrpRuntimeInBackgroundAsync(CancellationToken cancellationToken)
    {
        if (_isRemoteFrpsBackgroundRefreshRunning)
        {
            return;
        }

        _isRemoteFrpsBackgroundRefreshRunning = true;
        try
        {
            await RefreshSelectedFrpRuntimeAsync(cancellationToken, allowStatusMessage: false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _isRemoteFrpsBackgroundRefreshRunning = false;
        }
    }

    internal void TickSelectedFrpsUptimeForTest(TimeSpan elapsed)
    {
        if (_selectedFrpsUptimeCapturedAt is null)
        {
            return;
        }

        _selectedFrpsUptimeCapturedAt -= elapsed;
        OnPropertyChanged(nameof(SelectedFrpUptimeText));
    }

    private static bool TryParseFrpsUptime(string value, out TimeSpan uptime)
    {
        uptime = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(value) || value == "-")
        {
            return false;
        }

        var normalized = value.Trim();
        var days = 0;
        var daySeparator = normalized.IndexOf('-', StringComparison.Ordinal);
        if (daySeparator >= 0)
        {
            if (!int.TryParse(normalized[..daySeparator], out days))
            {
                return false;
            }

            normalized = normalized[(daySeparator + 1)..];
        }

        var parts = normalized.Split(':');
        if (parts.Length == 2
            && int.TryParse(parts[0], out var minutes)
            && int.TryParse(parts[1], out var seconds))
        {
            uptime = new TimeSpan(days, 0, minutes, seconds);
            return true;
        }

        if (parts.Length == 3
            && int.TryParse(parts[0], out var hours)
            && int.TryParse(parts[1], out minutes)
            && int.TryParse(parts[2], out seconds))
        {
            uptime = new TimeSpan(days, hours, minutes, seconds);
            return true;
        }

        return false;
    }

    private static string FormatFrpsUptime(TimeSpan uptime)
    {
        if (uptime < TimeSpan.Zero)
        {
            uptime = TimeSpan.Zero;
        }

        return uptime.Days > 0
            ? $"{uptime.Days}-{uptime.Hours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}"
            : uptime.Hours > 0
                ? $"{uptime.Hours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}"
                : $"{uptime.Minutes:00}:{uptime.Seconds:00}";
    }

    private static void RaiseOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }

    private static string ShellQuote(string value)
    {
        return $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
    }

    private void NotifySelectedFrpStatusChanged()
    {
        OnPropertyChanged(nameof(SelectedFrpProcessText));
        OnPropertyChanged(nameof(SelectedFrpUptimeText));
        OnPropertyChanged(nameof(IsSelectedFrpSuccess));
        OnPropertyChanged(nameof(IsSelectedFrpWarning));
        OnPropertyChanged(nameof(IsSelectedFrpError));
        OnPropertyChanged(nameof(IsSelectedFrpNeutral));
        OnPropertyChanged(nameof(CanRunRemoteFrpsCommand));
        OnPropertyChanged(nameof(RemoteFrpsToggleButtonText));
    }

    private void ResetDeleteConfirmation()
    {
        _isDeleteConfirmationPending = false;
        DeleteButtonText = "删除";
    }

    private void ResetRemoteFrpDeleteConfirmation()
    {
        _isRemoteFrpDeleteConfirmationPending = false;
        DeleteRemoteFrpFilesButtonText = "清理远程文件";
    }

    private sealed record CredentialCreationResult(bool Success, SshCredentialReference Credential)
    {
        public static CredentialCreationResult Failed { get; } =
            new(false, new SshCredentialReference(SshAuthenticationMode.SessionPassword));
    }

    private sealed record DeploymentPresenceStatusSnapshot(string Title, string Text, string Severity);

    private sealed class NoOpNodeConnectionWorkflowDialogService : INodeConnectionWorkflowDialogService
    {
        public static NoOpNodeConnectionWorkflowDialogService Instance { get; } = new();

        public Task<NodeConnectionWorkflowResult> ShowAsync(
            NodeProfile node,
            NodeConnectionWorkflowOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new NodeConnectionWorkflowResult(node.Name, false, false, false));
        }
    }

    private sealed class NoOpConfirmationDialogService : IConfirmationDialogService
    {
        public static NoOpConfirmationDialogService Instance { get; } = new();

        public Task<bool> ShowAsync(
            ConfirmationDialogRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<ConfirmationDialogResult> ShowChoiceAsync(
            ConfirmationDialogChoiceRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ConfirmationDialogResult.Confirm);
        }
    }

    private sealed class NoOpFrpLifecycleStateService : IFrpLifecycleStateService
    {
        public static NoOpFrpLifecycleStateService Instance { get; } = new();

        public IReadOnlyList<RemoteFrpsLifecycleSnapshot> ListRemoteFrpsSnapshots()
        {
            return [];
        }

        public void UpdateRemoteFrpsState(
            string nodeName,
            bool isSshOnline,
            FrpNexusStatus frpsStatus,
            string configPath = "")
        {
        }

        public void RemoveRemoteFrpsState(string nodeName)
        {
        }
    }

    private sealed class NoOpRemoteFrpsRetentionService : IRemoteFrpsRetentionService
    {
        public static NoOpRemoteFrpsRetentionService Instance { get; } = new();

        public Task<IReadOnlyList<RemoteFrpsRetentionRecord>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RemoteFrpsRetentionRecord>>([]);
        }

        public Task<RemoteFrpsRetentionRecord?> GetAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<RemoteFrpsRetentionRecord?>(null);
        }

        public Task SaveAsync(RemoteFrpsRetentionRecord record, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ClearAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class LegacyNodeConnectionSessionService(
        ISshConnectionService sshConnectionService) : INodeConnectionSessionService
    {
        private readonly Dictionary<string, NodeConnectionSessionSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SshCredentialReference> _credentials = new(StringComparer.OrdinalIgnoreCase);

        public async Task<NodeConnectionSessionResult> ConnectAsync(
            NodeProfile node,
            SshCredentialReference credential,
            CancellationToken cancellationToken = default)
        {
            var result = await sshConnectionService.TestConnectionAsync(
                new SshConnectionTestRequest(node, credential),
                cancellationToken);

            var state = result.Status == FrpNexusStatus.Online
                ? NodeConnectionSessionState.Online
                : NodeConnectionSessionState.Error;
            if (state == NodeConnectionSessionState.Online)
            {
                _credentials[node.Name] = credential;
            }

            _snapshots[node.Name] = new NodeConnectionSessionSnapshot(
                node.Name,
                state,
                result.TestedAt,
                result.Message);

            return new NodeConnectionSessionResult(node.Name, state, result.TestedAt, result.Message);
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

        public IReadOnlyList<NodeConnectionSessionSnapshot> ListActiveSessions()
        {
            return _snapshots.Values
                .Where(snapshot => snapshot.State == NodeConnectionSessionState.Online)
                .ToArray();
        }
    }

    private sealed class LegacyRemoteRuntimeService : IRemoteRuntimeService
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
            return Task.FromResult(CreateCommandResult(request));
        }

        public Task<RemoteRuntimeCommandResult> StopAsync(
            RemoteRuntimeCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateCommandResult(request));
        }

        public Task<RemoteRuntimeCommandResult> RestartAsync(
            RemoteRuntimeCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateCommandResult(request));
        }

        private static RemoteRuntimeCommandResult CreateCommandResult(RemoteRuntimeCommandRequest request)
        {
            return new RemoteRuntimeCommandResult(
                request.Node.Name,
                request.ProcessKind,
                FrpNexusStatus.Ready,
                DateTimeOffset.UtcNow,
                "远程运行命令已执行。");
        }
    }

    private sealed class LegacyRemoteFileTransferService : IRemoteFileTransferService
    {
        public Task<RemoteFilePresenceResult> CheckRemoteFilesAsync(
            RemoteFilePresenceRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RemoteFilePresenceResult(
                request.Node.Name,
                request.RemotePaths.Select(path => new RemoteFilePresenceEntry(path, true)).ToArray(),
                FrpNexusStatus.Ready,
                DateTimeOffset.UtcNow,
                "远程 frps 和 frps.toml 已就绪。"));
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
                "已清理远程 frps 核心和 frps.toml。"));
        }
    }

    private sealed class LegacyFilePickerService : IFilePickerService
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

    private sealed class LegacyRemoteDirectoryPickerService : IRemoteDirectoryPickerService
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

    private sealed class LegacyNodeCredentialSecretService : INodeCredentialSecretService
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
            string sessionPassword,
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

    private sealed class LegacyDeploymentRecordService : IDeploymentRecordService
    {
        public Task<IReadOnlyList<DeploymentRecord>> ListDeploymentRecordsAsync(CancellationToken cancellationToken = default)
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

public sealed partial class NodeListItemViewModel : ObservableObject
{
    public NodeListItemViewModel(
        NodeProfile node,
        NodeConnectionSessionSnapshot session,
        bool isConnectionOperationRunning = false)
    {
        Node = node;
        Session = session;
        IsConnectionOperationRunning = isConnectionOperationRunning;
    }

    public NodeProfile Node { get; }

    public NodeConnectionSessionSnapshot Session { get; }

    [ObservableProperty]
    private bool _isSelected;

    public bool IsConnectionOperationRunning { get; }

    public string Name => Node.Name;

    public string Host => Node.Host;

    public string Version => string.IsNullOrWhiteSpace(Node.FrpVersion) ? "-" : Node.FrpVersion;

    public string LastConnectionText => Node.LastConnectionTestedAt is null
        ? "-"
        : Node.LastConnectionTestedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public string LastConnectionFullText => Node.LastConnectionTestedAt is null
        ? "暂无最后连接记录"
        : Node.LastConnectionTestedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public string ConnectionStatusText => Session.State switch
    {
        NodeConnectionSessionState.Online => "在线",
        NodeConnectionSessionState.Error => "错误",
        _ => "离线"
    };

    public bool IsConnectionSuccess => Session.State == NodeConnectionSessionState.Online;

    public bool IsConnectionError => Session.State == NodeConnectionSessionState.Error;

    public bool IsConnectionNeutral => !IsConnectionSuccess && !IsConnectionError;

    public string ConnectionActionText
    {
        get
        {
            if (IsConnectionOperationRunning)
            {
                return "处理中...";
            }

            return Session.State == NodeConnectionSessionState.Online ? "断开" : "连接";
        }
    }

    public bool CanRunConnectionAction => !IsConnectionOperationRunning
        && Session.State != NodeConnectionSessionState.Connecting;

    public bool IsConnectionActionPrimary => Session.State != NodeConnectionSessionState.Online;

    public string FrpServiceText
    {
        get
        {
            if (Version == "-")
            {
                return "未安装";
            }

            return Node.FrpStatus switch
            {
                FrpNexusStatus.Running => "运行中",
                FrpNexusStatus.Stopped => "已停止",
                FrpNexusStatus.Warning => "警告",
                FrpNexusStatus.Error => "异常",
                _ => "未知"
            };
        }
    }

    public bool IsFrpSuccess => Node.FrpStatus == FrpNexusStatus.Running && Version != "-";

    public bool IsFrpWarning => Node.FrpStatus == FrpNexusStatus.Stopped && Version != "-";

    public bool IsFrpError => Node.FrpStatus == FrpNexusStatus.Error;

    public bool IsFrpNeutral => !IsFrpSuccess && !IsFrpWarning && !IsFrpError;
}
