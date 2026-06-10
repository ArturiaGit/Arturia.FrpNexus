using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Application.Configuration;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.Services;
using Arturia.FrpNexus.Desktop.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed partial class TunnelsPageViewModel : PageViewModel
{
    public const string DefaultLocalAddress = "127.0.0.1";
    public const string DefaultLocalPort = "8080";
    private const int LocalFrpcStatusPollIntervalMilliseconds = 2000;
    public const string RemarkPlaceholder = "可选备注";

    private readonly ITunnelManagementService _tunnelManagementService;
    private readonly INodeManagementService _nodeManagementService;
    private readonly ILocalFrpcProcessService _localFrpcProcessService;
    private readonly ILocalFrpcConfigurationService _localFrpcConfigurationService;
    private readonly IRuntimeRecordService _runtimeRecordService;
    private readonly IFilePickerService _filePickerService;
    private readonly HashSet<string> _availableNodeNames = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _localFrpcStatusPollingCancellation = new();
    private bool _isDeleteConfirmationPending;
    private string? _editingOriginalName;
    private Task? _localFrpcStatusPollingTask;

    [ObservableProperty]
    private string _tunnelCountText = "共 0 条记录";

    [ObservableProperty]
    private TunnelProfile? _selectedTunnel;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedProtocolFilter = AllProtocolFilter;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private bool _isEditingExistingTunnel;

    [ObservableProperty]
    private string _editorTitle = "隧道详情";

    [ObservableProperty]
    private string _formName = string.Empty;

    [ObservableProperty]
    private TunnelProtocol _formProtocol = TunnelProtocol.Http;

    [ObservableProperty]
    private string _formNodeName = string.Empty;

    [ObservableProperty]
    private string _formLocalAddress = string.Empty;

    [ObservableProperty]
    private string _formLocalPort = string.Empty;

    [ObservableProperty]
    private string _formRemoteEndpoint = string.Empty;

    [ObservableProperty]
    private string _formRemark = string.Empty;

    [ObservableProperty]
    private string _formErrorText = string.Empty;

    [ObservableProperty]
    private string _deleteButtonText = "删除";

    [ObservableProperty]
    private string _runtimeStatusText = "启用/停用隧道会更新本地 frpc 配置；客户端进程由上方节点级操作控制。";

    [ObservableProperty]
    private bool _isTunnelRuntimeBusy;

    [ObservableProperty]
    private string _localFrpcToggleButtonText = "启动 frpc";

    [ObservableProperty]
    private string _selectedClientNodeName = string.Empty;

    [ObservableProperty]
    private string _localFrpcStatusText = "请选择节点";

    [ObservableProperty]
    private string _localFrpcEnabledTunnelCountText = "启用隧道 0 条";

    [ObservableProperty]
    private bool _isLocalFrpcSuccess;

    [ObservableProperty]
    private bool _isLocalFrpcWarning;

    [ObservableProperty]
    private bool _isLocalFrpcError;

    [ObservableProperty]
    private bool _isLocalFrpcNeutral = true;

    private bool _isLocalFrpcManagedRunning;

    [ObservableProperty]
    private string _localFrpcBinaryPath = string.Empty;

    [ObservableProperty]
    private string _localFrpcConfigPath = string.Empty;

    [ObservableProperty]
    private string _localFrpcSuggestedConfigPath = string.Empty;

    public TunnelsPageViewModel(
        ITunnelManagementService tunnelManagementService,
        INodeManagementService nodeManagementService,
        ILocalFrpcProcessService localFrpcProcessService,
        ILocalFrpcConfigurationService localFrpcConfigurationService,
        IRuntimeRecordService runtimeRecordService,
        IFilePickerService filePickerService)
        : base("隧道管理", "创建和检查 TCP、UDP、HTTP、HTTPS 隧道配置")
    {
        _tunnelManagementService = tunnelManagementService;
        _nodeManagementService = nodeManagementService;
        _localFrpcProcessService = localFrpcProcessService;
        _localFrpcConfigurationService = localFrpcConfigurationService;
        _runtimeRecordService = runtimeRecordService;
        _filePickerService = filePickerService;
        Tunnels = [];
        TunnelRows = [];
        NodeOptions = [];
        ClientNodeOptions = [];

        _ = LoadTunnelsAsync();
        StartLocalFrpcStatusPolling();
    }

    public ObservableCollection<TunnelProfile> Tunnels { get; }

    public ObservableCollection<TunnelListItemViewModel> TunnelRows { get; }

    public ObservableCollection<string> NodeOptions { get; }

    public ObservableCollection<string> ClientNodeOptions { get; }

    public const string AllProtocolFilter = "所有协议";

    public IReadOnlyList<string> ProtocolFilterOptions { get; } =
    [
        AllProtocolFilter,
        "TCP",
        "UDP",
        "HTTP",
        "HTTPS"
    ];

    public IReadOnlyList<TunnelProtocol> ProtocolOptions { get; } =
    [
        TunnelProtocol.Tcp,
        TunnelProtocol.Udp,
        TunnelProtocol.Http,
        TunnelProtocol.Https
    ];

    public string RemoteEndpointLabel => FormProtocol is TunnelProtocol.Tcp or TunnelProtocol.Udp
        ? "远程端口"
        : "域名";

    public string RemoteEndpointPlaceholder => FormProtocol is TunnelProtocol.Tcp or TunnelProtocol.Udp
        ? "25565 / 60000"
        : "example.com";

    public async Task LoadTunnelsAsync(CancellationToken cancellationToken = default)
    {
        await LoadNodeOptionsAsync(cancellationToken);

        var tunnels = await _tunnelManagementService.ListTunnelsAsync(cancellationToken);

        Tunnels.Clear();
        foreach (var tunnel in tunnels)
        {
            Tunnels.Add(tunnel);
        }

        SelectedTunnel = Tunnels.FirstOrDefault();
        EnsureSelectedClientNode();
        await RefreshLocalFrpcConfigurationAsync(cancellationToken);
        ApplyFilters();
        RefreshLocalFrpcClientState();
    }

    [RelayCommand]
    private void SelectTunnel(TunnelListItemViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        SelectedTunnel = row.Tunnel;
    }

    [RelayCommand]
    private async Task StartEditTunnelAsync(TunnelListItemViewModel? row)
    {
        if (row is not null)
        {
            SelectedTunnel = row.Tunnel;
        }

        await StartEditSelectedTunnelAsync();
    }

    [RelayCommand]
    private async Task StartCreateTunnelAsync(CancellationToken cancellationToken = default)
    {
        await LoadNodeOptionsAsync(cancellationToken);

        _editingOriginalName = null;
        ResetDeleteConfirmation();
        IsEditingExistingTunnel = false;
        EditorTitle = "新建隧道";
        FormName = string.Empty;
        FormProtocol = TunnelProtocol.Http;
        FormNodeName = SelectedClientNodeName;
        FormLocalAddress = string.Empty;
        FormLocalPort = string.Empty;
        FormRemoteEndpoint = string.Empty;
        FormRemark = string.Empty;
        FormErrorText = NodeOptions.Count == 0 ? "请先在节点页面创建节点。" : string.Empty;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private async Task StartEditSelectedTunnelAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedTunnel is null)
        {
            FormErrorText = "请先选择一条隧道记录。";
            return;
        }

        await LoadNodeOptionsAsync(cancellationToken);

        _editingOriginalName = SelectedTunnel.Name;
        ResetDeleteConfirmation();
        IsEditingExistingTunnel = true;
        EditorTitle = "编辑隧道";
        FormName = SelectedTunnel.Name;
        FormProtocol = SelectedTunnel.Protocol;
        FormNodeName = SelectedTunnel.NodeName;
        EnsureSelectedNodeOptionVisible(FormNodeName);
        FormLocalAddress = SelectedTunnel.LocalAddress;
        FormLocalPort = SelectedTunnel.LocalPort.ToString();
        FormRemoteEndpoint = SelectedTunnel.RemoteEndpoint;
        FormRemark = SelectedTunnel.Remark;
        FormErrorText = string.Empty;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private async Task SaveTunnelAsync()
    {
        ResetDeleteConfirmation();

        if (!TryCreateTunnel(out var tunnel))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_editingOriginalName)
            && !string.Equals(_editingOriginalName, tunnel.Name, System.StringComparison.OrdinalIgnoreCase))
        {
            await _tunnelManagementService.DeleteTunnelAsync(_editingOriginalName);
        }

        await _tunnelManagementService.SaveTunnelAsync(tunnel);
        await LoadTunnelsAsync();

        SelectedTunnel = Tunnels.FirstOrDefault(item => item.Name == tunnel.Name);
        IsEditorOpen = false;
        IsEditingExistingTunnel = false;
        FormErrorText = string.Empty;
        _editingOriginalName = null;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        ResetDeleteConfirmation();
        IsEditorOpen = false;
        IsEditingExistingTunnel = false;
        FormErrorText = string.Empty;
        _editingOriginalName = null;
    }

    [RelayCommand]
    private async Task DeleteSelectedTunnelAsync()
    {
        if (SelectedTunnel is null)
        {
            FormErrorText = "请先选择一条隧道记录。";
            return;
        }

        if (!_isDeleteConfirmationPending)
        {
            _isDeleteConfirmationPending = true;
            DeleteButtonText = "确认删除";
            FormErrorText = $"将删除本地隧道记录 `{SelectedTunnel.Name}`，再次点击确认。";
            return;
        }

        await _tunnelManagementService.DeleteTunnelAsync(SelectedTunnel.Name);
        ResetDeleteConfirmation();
        await LoadTunnelsAsync();
        IsEditorOpen = false;
        IsEditingExistingTunnel = false;
        FormErrorText = string.Empty;
    }

    [RelayCommand]
    private async Task ToggleTunnelEnabledAsync(TunnelListItemViewModel? row, CancellationToken cancellationToken = default)
    {
        if (row is null)
        {
            RuntimeStatusText = "请先选择一条隧道。";
            return;
        }

        SelectedTunnel = row.Tunnel;

        var shouldDisable = row.Tunnel.Status == FrpNexusStatus.Running;
        await ToggleTunnelEnabledStateAsync(
            row.Tunnel,
            shouldDisable ? FrpNexusStatus.Stopped : FrpNexusStatus.Running,
            cancellationToken);
    }

    [RelayCommand]
    private async Task SelectLocalFrpcBinaryAsync(CancellationToken cancellationToken = default)
    {
        var path = await _filePickerService.PickLocalFrpcBinaryAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await _localFrpcConfigurationService.SaveFrpcBinaryPathAsync(path, cancellationToken);
        LocalFrpcBinaryPath = path;
        RuntimeStatusText = "已保存本地 frpc 核心路径。";
    }

    [RelayCommand]
    private async Task SelectLocalFrpcConfigAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SelectedClientNodeName))
        {
            RuntimeStatusText = "请先选择一个客户端节点。";
            return;
        }

        var suggestedFileName = Path.GetFileName(LocalFrpcConfigPath);
        if (string.IsNullOrWhiteSpace(suggestedFileName))
        {
            suggestedFileName = $"{SelectedClientNodeName}.frpc.toml";
        }

        var path = await _filePickerService.PickLocalFrpcConfigPathAsync(suggestedFileName, cancellationToken);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await _localFrpcConfigurationService.SaveNodeConfigPathAsync(SelectedClientNodeName, path, cancellationToken);
        LocalFrpcConfigPath = path;
        RuntimeStatusText = $"已保存节点 {SelectedClientNodeName} 的 frpc.toml 路径。";
    }

    [RelayCommand]
    private async Task StartLocalFrpcAsync(CancellationToken cancellationToken = default)
    {
        await ApplySelectedClientNodeFrpcAsync(requireRunningSession: false, cancellationToken);
    }

    [RelayCommand]
    private async Task ToggleLocalFrpcAsync(CancellationToken cancellationToken = default)
    {
        if (IsTunnelRuntimeBusy)
        {
            return;
        }

        if (_isLocalFrpcManagedRunning)
        {
            await StopLocalFrpcAsync(cancellationToken);
            return;
        }

        await StartLocalFrpcAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task StopLocalFrpcAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SelectedClientNodeName))
        {
            RuntimeStatusText = "请先选择一个客户端节点。";
            RefreshLocalFrpcClientState();
            return;
        }

        IsTunnelRuntimeBusy = true;
        RuntimeStatusText = $"正在停止节点 {SelectedClientNodeName} 的本地 frpc。";
        try
        {
            var result = await _localFrpcProcessService.StopNodeAsync(SelectedClientNodeName, cancellationToken);
            RuntimeStatusText = result.Message;
            await SaveLocalFrpcRuntimeRecordAsync(
                SelectedClientNodeName,
                result.Status,
                result.Message,
                cancellationToken);
        }
        finally
        {
            IsTunnelRuntimeBusy = false;
            await RefreshLocalFrpcStatusFromProcessAsync(cancellationToken);
        }
    }

    [RelayCommand]
    private async Task ReloadLocalFrpcAsync(CancellationToken cancellationToken = default)
    {
        await ApplySelectedClientNodeFrpcAsync(requireRunningSession: true, cancellationToken);
    }

    partial void OnSelectedTunnelChanged(TunnelProfile? value)
    {
        ResetDeleteConfirmation();
        SyncSelectedRows();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedProtocolFilterChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnFormProtocolChanged(TunnelProtocol value)
    {
        OnPropertyChanged(nameof(RemoteEndpointLabel));
        OnPropertyChanged(nameof(RemoteEndpointPlaceholder));
    }

    partial void OnSelectedClientNodeNameChanged(string value)
    {
        _ = RefreshLocalFrpcConfigurationAsync();
        _ = RefreshLocalFrpcStatusFromProcessAsync();
    }

    private async Task LoadNodeOptionsAsync(CancellationToken cancellationToken = default)
    {
        var nodes = await _nodeManagementService.ListNodesAsync(cancellationToken);
        var nodeNames = nodes
            .Select(node => node.Name.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, System.StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _availableNodeNames.Clear();
        foreach (var nodeName in nodeNames)
        {
            _availableNodeNames.Add(nodeName);
        }

        NodeOptions.Clear();
        ClientNodeOptions.Clear();
        foreach (var nodeName in nodeNames)
        {
            NodeOptions.Add(nodeName);
            ClientNodeOptions.Add(nodeName);
        }

        EnsureSelectedClientNode();
    }

    private void EnsureSelectedNodeOptionVisible(string nodeName)
    {
        if (string.IsNullOrWhiteSpace(nodeName)
            || NodeOptions.Contains(nodeName, System.StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        NodeOptions.Insert(0, nodeName);
    }

    private void ApplyFilters()
    {
        var rows = Tunnels.AsEnumerable();
        var search = SearchText.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            rows = rows.Where(tunnel =>
                tunnel.Name.Contains(search, System.StringComparison.OrdinalIgnoreCase)
                || tunnel.NodeName.Contains(search, System.StringComparison.OrdinalIgnoreCase)
                || tunnel.RemoteEndpoint.Contains(search, System.StringComparison.OrdinalIgnoreCase)
                || tunnel.LocalPort.ToString(System.Globalization.CultureInfo.InvariantCulture).Contains(search, System.StringComparison.OrdinalIgnoreCase));
        }

        if (TryParseProtocolFilter(SelectedProtocolFilter, out var protocol))
        {
            rows = rows.Where(tunnel => tunnel.Protocol == protocol);
        }

        TunnelRows.Clear();
        foreach (var tunnel in rows)
        {
            TunnelRows.Add(new TunnelListItemViewModel(ApplyLocalRuntimeStatus(tunnel)));
        }

        SyncSelectedRows();
        TunnelCountText = $"共 {TunnelRows.Count} 条记录";
        RefreshLocalFrpcClientState();
    }

    private async Task ToggleTunnelEnabledStateAsync(
        TunnelProfile tunnel,
        FrpNexusStatus desiredStatus,
        CancellationToken cancellationToken)
    {
        var node = await _nodeManagementService.GetNodeAsync(tunnel.NodeName, cancellationToken);
        if (node is null)
        {
            RuntimeStatusText = $"未找到关联节点 {tunnel.NodeName}，请先修正隧道关联节点。";
            await SaveTunnelRuntimeStatusAsync(tunnel, FrpNexusStatus.Error, cancellationToken);
            return;
        }

        var enabledTunnels = BuildEnabledTunnelsForNode(tunnel, desiredStatus);
        var snapshot = _localFrpcProcessService.GetNodeStatus(node.Name, LocalFrpcConfigPath);

        IsTunnelRuntimeBusy = true;
        RuntimeStatusText = desiredStatus == FrpNexusStatus.Running
            ? $"正在启用隧道 {tunnel.Name}。"
            : $"正在停用隧道 {tunnel.Name}。";

        try
        {
            var nextStatus = desiredStatus;
            if (snapshot.Status == FrpNexusStatus.Running)
            {
                if (enabledTunnels.Count == 0)
                {
                    var stopResult = await _localFrpcProcessService.StopNodeAsync(node.Name, cancellationToken);
                    RuntimeStatusText = $"已停用 {tunnel.Name}，该节点没有启用隧道，{stopResult.Message}";
                    await SaveLocalFrpcRuntimeRecordAsync(
                        node.Name,
                        stopResult.Status,
                        stopResult.Message,
                        cancellationToken);
                }
                else
                {
                    var request = await CreateLocalFrpcProcessRequestAsync(node, enabledTunnels, cancellationToken);
                    if (request is null)
                    {
                        nextStatus = tunnel.Status;
                    }
                    else
                    {
                        var result = await _localFrpcProcessService.ApplyNodeTunnelsAsync(
                            request,
                            cancellationToken);
                        RuntimeStatusText = result.Status == FrpNexusStatus.Error
                            ? result.Message
                            : $"已更新节点 {node.Name} 的本地 frpc 配置。";
                        await SaveLocalFrpcRuntimeRecordAsync(
                            node.Name,
                            result.Status,
                            result.Message,
                            cancellationToken);
                        nextStatus = result.Status == FrpNexusStatus.Error
                            ? FrpNexusStatus.Error
                            : desiredStatus;
                    }
                }
            }
            else
            {
                RuntimeStatusText = desiredStatus == FrpNexusStatus.Running
                    ? $"已启用 {tunnel.Name}。本地 frpc 尚未运行，请在上方启动节点 {node.Name} 的 frpc。"
                    : $"已停用 {tunnel.Name}。本地 frpc 尚未运行。";
            }

            await SaveTunnelRuntimeStatusAsync(tunnel, nextStatus, cancellationToken);
        }
        finally
        {
            IsTunnelRuntimeBusy = false;
            await RefreshLocalFrpcStatusFromProcessAsync(cancellationToken);
        }
    }

    private async Task SaveTunnelRuntimeStatusAsync(
        TunnelProfile tunnel,
        FrpNexusStatus status,
        CancellationToken cancellationToken)
    {
        var updated = tunnel with
        {
            Status = status
        };
        await _tunnelManagementService.SaveTunnelAsync(updated, cancellationToken);
        await LoadTunnelsAsync(cancellationToken);
        SelectedTunnel = Tunnels.FirstOrDefault(item => item.Name == tunnel.Name);
        EnsureSelectedClientNode();
        RefreshLocalFrpcClientState();
    }

    private TunnelProfile ApplyLocalRuntimeStatus(TunnelProfile tunnel)
    {
        return tunnel;
    }

    private IReadOnlyList<TunnelProfile> BuildEnabledTunnelsForNode(TunnelProfile changedTunnel, FrpNexusStatus desiredStatus)
    {
        return Tunnels
            .Where(item => string.Equals(item.NodeName, changedTunnel.NodeName, System.StringComparison.OrdinalIgnoreCase))
            .Select(item => string.Equals(item.Name, changedTunnel.Name, System.StringComparison.OrdinalIgnoreCase)
                ? item with { Status = desiredStatus }
                : item)
            .Where(item => item.Status == FrpNexusStatus.Running)
            .ToArray();
    }

    private IReadOnlyList<TunnelProfile> GetEnabledTunnelsForNode(string nodeName)
    {
        return Tunnels
            .Where(item => string.Equals(item.NodeName, nodeName, System.StringComparison.OrdinalIgnoreCase)
                && item.Status == FrpNexusStatus.Running)
            .ToArray();
    }

    private async Task ApplySelectedClientNodeFrpcAsync(bool requireRunningSession, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SelectedClientNodeName))
        {
            RuntimeStatusText = "请先选择一个客户端节点。";
            RefreshLocalFrpcClientState();
            return;
        }

        var node = await _nodeManagementService.GetNodeAsync(SelectedClientNodeName, cancellationToken);
        if (node is null)
        {
            RuntimeStatusText = $"未找到节点 {SelectedClientNodeName}，请先修正客户端节点。";
            RefreshLocalFrpcClientState();
            return;
        }

        var enabledTunnels = GetEnabledTunnelsForNode(node.Name);
        if (enabledTunnels.Count == 0)
        {
            RuntimeStatusText = $"节点 {node.Name} 没有启用隧道，请先在表格中启用至少一条隧道。";
            RefreshLocalFrpcClientState();
            return;
        }

        await RefreshLocalFrpcConfigurationAsync(cancellationToken);
        var snapshot = _localFrpcProcessService.GetNodeStatus(node.Name, LocalFrpcConfigPath);
        if (requireRunningSession && snapshot.Status != FrpNexusStatus.Running)
        {
            RuntimeStatusText = $"节点 {node.Name} 的本地 frpc 未运行，请先启动 frpc。";
            RefreshLocalFrpcClientState();
            return;
        }

        IsTunnelRuntimeBusy = true;
        RuntimeStatusText = requireRunningSession
            ? $"正在重载节点 {node.Name} 的本地 frpc 配置。"
            : $"正在启动节点 {node.Name} 的本地 frpc。";

        try
        {
            var request = await CreateLocalFrpcProcessRequestAsync(node, enabledTunnels, cancellationToken);
            if (request is null)
            {
                return;
            }

            var result = await _localFrpcProcessService.ApplyNodeTunnelsAsync(
                request,
                cancellationToken);
            RuntimeStatusText = result.Message;
            await SaveLocalFrpcRuntimeRecordAsync(
                node.Name,
                result.Status,
                result.Message,
                cancellationToken);
        }
        finally
        {
            IsTunnelRuntimeBusy = false;
            await RefreshLocalFrpcStatusFromProcessAsync(cancellationToken);
        }
    }

    private void EnsureSelectedClientNode()
    {
        if (!string.IsNullOrWhiteSpace(SelectedClientNodeName)
            && ClientNodeOptions.Contains(SelectedClientNodeName, System.StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        SelectedClientNodeName = SelectedTunnel?.NodeName is { Length: > 0 } selectedNodeName
            && ClientNodeOptions.Contains(selectedNodeName, System.StringComparer.OrdinalIgnoreCase)
                ? selectedNodeName
                : ClientNodeOptions.FirstOrDefault() ?? string.Empty;
    }

    private async Task RefreshLocalFrpcConfigurationAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SelectedClientNodeName))
        {
            LocalFrpcConfigPath = string.Empty;
            LocalFrpcSuggestedConfigPath = string.Empty;
            return;
        }

        var configuration = await _localFrpcConfigurationService.GetConfigurationAsync(
            SelectedClientNodeName,
            cancellationToken);
        LocalFrpcBinaryPath = configuration.FrpcBinaryPath;
        LocalFrpcConfigPath = configuration.FrpcConfigPath;
        LocalFrpcSuggestedConfigPath = configuration.SuggestedFrpcConfigPath;
    }

    private void StartLocalFrpcStatusPolling()
    {
        if (_localFrpcStatusPollingTask is not null)
        {
            return;
        }

        _localFrpcStatusPollingTask = PollLocalFrpcStatusAsync(_localFrpcStatusPollingCancellation.Token);
    }

    private async Task PollLocalFrpcStatusAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(LocalFrpcStatusPollIntervalMilliseconds, cancellationToken);
                await RefreshLocalFrpcStatusFromProcessAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal async Task RefreshLocalFrpcStatusFromProcessAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SelectedClientNodeName))
        {
            RefreshLocalFrpcClientState();
            return;
        }

        await RefreshLocalFrpcConfigurationAsync(cancellationToken);
        var snapshot = _localFrpcProcessService.GetNodeStatus(SelectedClientNodeName, LocalFrpcConfigPath);
        RefreshLocalFrpcClientState(snapshot);

        if (snapshot.Status is FrpNexusStatus.Error or FrpNexusStatus.Warning)
        {
            RuntimeStatusText = snapshot.Message;
            if (snapshot.Status == FrpNexusStatus.Error)
            {
                await SaveLocalFrpcRuntimeRecordAsync(
                    SelectedClientNodeName,
                    snapshot.Status,
                    snapshot.Message,
                    cancellationToken);
            }
        }
    }

    private async Task<LocalFrpcProcessRequest?> CreateLocalFrpcProcessRequestAsync(
        NodeProfile node,
        IReadOnlyList<TunnelProfile> enabledTunnels,
        CancellationToken cancellationToken)
    {
        await RefreshLocalFrpcConfigurationAsync(cancellationToken);

        if (!await ValidateEnabledTunnelsForLocalFrpcAsync(node.Name, enabledTunnels, cancellationToken))
        {
            return null;
        }

        var frpcBinaryPath = LocalFrpcBinaryPath.Trim();
        if (!string.IsNullOrWhiteSpace(frpcBinaryPath) && !File.Exists(frpcBinaryPath))
        {
            RuntimeStatusText = "已选择的本地 frpc 核心文件不存在，请重新选择。";
            await SaveLocalFrpcRuntimeRecordAsync(
                node.Name,
                FrpNexusStatus.Error,
                RuntimeStatusText,
                cancellationToken);
            return null;
        }

        if (!IsCompatibleLocalFrpcBinaryPath(frpcBinaryPath, out var binaryErrorText))
        {
            RuntimeStatusText = binaryErrorText;
            await SaveLocalFrpcRuntimeRecordAsync(
                node.Name,
                FrpNexusStatus.Error,
                RuntimeStatusText,
                cancellationToken);
            return null;
        }

        var configPath = LocalFrpcConfigPath.Trim();
        if (string.IsNullOrWhiteSpace(configPath))
        {
            RuntimeStatusText = "请先选择本地 frpc.toml 路径。";
            await SaveLocalFrpcRuntimeRecordAsync(
                node.Name,
                FrpNexusStatus.Error,
                RuntimeStatusText,
                cancellationToken);
            return null;
        }

        if (!CanWriteConfigPath(configPath, out var errorText))
        {
            RuntimeStatusText = errorText;
            await SaveLocalFrpcRuntimeRecordAsync(
                node.Name,
                FrpNexusStatus.Error,
                RuntimeStatusText,
                cancellationToken);
            return null;
        }

        return new LocalFrpcProcessRequest(node, enabledTunnels, frpcBinaryPath, configPath);
    }

    private async Task<bool> ValidateEnabledTunnelsForLocalFrpcAsync(
        string nodeName,
        IReadOnlyList<TunnelProfile> enabledTunnels,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var tunnel in enabledTunnels)
            {
                FrpTunnelConfigurationValidator.ValidateTunnel(tunnel);
            }

            return true;
        }
        catch (InvalidOperationException ex)
        {
            RuntimeStatusText = ex.Message;
            await SaveLocalFrpcRuntimeRecordAsync(
                nodeName,
                FrpNexusStatus.Error,
                RuntimeStatusText,
                cancellationToken);
            return false;
        }
    }

    private async Task SaveLocalFrpcRuntimeRecordAsync(
        string nodeName,
        FrpNexusStatus status,
        string message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            return;
        }

        var snapshot = _localFrpcProcessService.GetNodeStatus(nodeName, LocalFrpcConfigPath);
        var processId = status == FrpNexusStatus.Running && snapshot.ProcessId is not null
            ? snapshot.ProcessId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "-";
        var uptime = status switch
        {
            FrpNexusStatus.Running => "运行中",
            FrpNexusStatus.Stopped => "-",
            _ => ShortenRuntimeMessage(message)
        };

        await _runtimeRecordService.SaveRuntimeProcessAsync(
            new RuntimeProcess(
                CreateLocalFrpcRuntimeProcessName(nodeName),
                nodeName,
                "frpc",
                status,
                processId,
                uptime,
                "-"),
            cancellationToken);
    }

    private static string CreateLocalFrpcRuntimeProcessName(string nodeName)
    {
        return $"local-frpc:{nodeName}";
    }

    private static bool IsCompatibleLocalFrpcBinaryPath(string frpcBinaryPath, out string errorText)
    {
        errorText = string.Empty;
        if (string.IsNullOrWhiteSpace(frpcBinaryPath))
        {
            return true;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && !string.Equals(Path.GetFileName(frpcBinaryPath), "frpc.exe", System.StringComparison.OrdinalIgnoreCase))
        {
            errorText = "当前系统需要选择 Windows 版 frpc.exe，请重新选择核心文件。";
            return false;
        }

        return true;
    }

    private static string ShortenRuntimeMessage(string message)
    {
        var normalized = string.IsNullOrWhiteSpace(message)
            ? "未返回详细错误。"
            : message.Replace(System.Environment.NewLine, " ", System.StringComparison.Ordinal).Trim();

        const int maxLength = 96;
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength] + "...";
    }

    private static bool CanWriteConfigPath(string configPath, out string errorText)
    {
        errorText = string.Empty;

        try
        {
            var directory = Path.GetDirectoryName(configPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                errorText = "frpc.toml 路径必须包含本地目录。";
                return false;
            }

            Directory.CreateDirectory(directory);
            using var stream = new FileStream(
                configPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read);
            return true;
        }
        catch (Exception ex)
        {
            errorText = $"frpc.toml 路径不可写：{ex.Message}";
            return false;
        }
    }

    private void RefreshLocalFrpcClientState()
    {
        if (string.IsNullOrWhiteSpace(SelectedClientNodeName))
        {
            LocalFrpcStatusText = "请选择节点";
            LocalFrpcEnabledTunnelCountText = "启用隧道 0 条";
            _isLocalFrpcManagedRunning = false;
            SetLocalFrpcStatusClasses(FrpNexusStatus.Stopped);
            RefreshLocalFrpcToggleButtonText();
            return;
        }

        var enabledCount = GetEnabledTunnelsForNode(SelectedClientNodeName).Count;
        var snapshot = _localFrpcProcessService.GetNodeStatus(SelectedClientNodeName, LocalFrpcConfigPath);
        _isLocalFrpcManagedRunning = snapshot.Status == FrpNexusStatus.Running && snapshot.IsManaged;
        LocalFrpcStatusText = snapshot.Status switch
        {
            FrpNexusStatus.Running => "运行中",
            FrpNexusStatus.Error => "异常",
            FrpNexusStatus.Warning => snapshot.IsManaged ? "警告" : "未接管",
            _ => "未运行"
        };
        LocalFrpcEnabledTunnelCountText = $"启用隧道 {enabledCount} 条";
        SetLocalFrpcStatusClasses(snapshot.Status);
        RefreshLocalFrpcToggleButtonText();
    }

    private void RefreshLocalFrpcClientState(LocalFrpcProcessSnapshot snapshot)
    {
        var enabledCount = string.IsNullOrWhiteSpace(SelectedClientNodeName)
            ? 0
            : GetEnabledTunnelsForNode(SelectedClientNodeName).Count;
        _isLocalFrpcManagedRunning = snapshot.Status == FrpNexusStatus.Running && snapshot.IsManaged;
        LocalFrpcStatusText = snapshot.Status switch
        {
            FrpNexusStatus.Running => "运行中",
            FrpNexusStatus.Error => "异常",
            FrpNexusStatus.Warning => snapshot.IsManaged ? "警告" : "未接管",
            _ => "未运行"
        };
        LocalFrpcEnabledTunnelCountText = $"启用隧道 {enabledCount} 条";
        SetLocalFrpcStatusClasses(snapshot.Status);
        RefreshLocalFrpcToggleButtonText();
    }

    private void SetLocalFrpcStatusClasses(FrpNexusStatus status)
    {
        IsLocalFrpcSuccess = status == FrpNexusStatus.Running;
        IsLocalFrpcWarning = status == FrpNexusStatus.Warning;
        IsLocalFrpcError = status == FrpNexusStatus.Error;
        IsLocalFrpcNeutral = !IsLocalFrpcSuccess && !IsLocalFrpcWarning && !IsLocalFrpcError;
    }

    partial void OnIsTunnelRuntimeBusyChanged(bool value)
    {
        RefreshLocalFrpcToggleButtonText();
    }

    private void RefreshLocalFrpcToggleButtonText()
    {
        LocalFrpcToggleButtonText = IsTunnelRuntimeBusy
            ? "处理中..."
            : _isLocalFrpcManagedRunning
                ? "停止 frpc"
                : "启动 frpc";
    }

    private void SyncSelectedRows()
    {
        foreach (var row in TunnelRows)
        {
            row.IsSelected = SelectedTunnel is not null
                && string.Equals(row.Tunnel.Name, SelectedTunnel.Name, System.StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool TryParseProtocolFilter(string value, out TunnelProtocol protocol)
    {
        protocol = TunnelProtocol.Tcp;
        return value switch
        {
            "TCP" => SetProtocol(TunnelProtocol.Tcp, out protocol),
            "UDP" => SetProtocol(TunnelProtocol.Udp, out protocol),
            "HTTP" => SetProtocol(TunnelProtocol.Http, out protocol),
            "HTTPS" => SetProtocol(TunnelProtocol.Https, out protocol),
            _ => false
        };
    }

    private static bool SetProtocol(TunnelProtocol value, out TunnelProtocol protocol)
    {
        protocol = value;
        return true;
    }

    private bool TryCreateTunnel(out TunnelProfile tunnel)
    {
        tunnel = new TunnelProfile(string.Empty, FormProtocol, string.Empty, string.Empty, 0, string.Empty, FrpNexusStatus.Stopped, string.Empty);

        if (string.IsNullOrWhiteSpace(FormName))
        {
            FormErrorText = "隧道名称不能为空。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(FormNodeName) || !_availableNodeNames.Contains(FormNodeName.Trim()))
        {
            FormErrorText = "请选择一个已创建的节点。";
            return false;
        }

        var localAddress = ValueOrDefault(FormLocalAddress, DefaultLocalAddress);
        var localPortText = ValueOrDefault(FormLocalPort, DefaultLocalPort);
        if (!int.TryParse(localPortText, out var localPort) || localPort is < 1 or > 65535)
        {
            FormErrorText = "本地端口必须是 1 到 65535 之间的数字。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(FormRemoteEndpoint))
        {
            FormErrorText = "远程端点不能为空。";
            return false;
        }

        tunnel = new TunnelProfile(
            FormName.Trim(),
            FormProtocol,
            FormNodeName.Trim(),
            localAddress,
            localPort,
            FormRemoteEndpoint.Trim(),
            FrpNexusStatus.Stopped,
            FormRemark.Trim());

        try
        {
            FrpTunnelConfigurationValidator.ValidateTunnel(tunnel);
        }
        catch (InvalidOperationException ex)
        {
            FormErrorText = ex.Message;
            return false;
        }

        return true;
    }

    private static string ValueOrDefault(string value, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }

    private void ResetDeleteConfirmation()
    {
        _isDeleteConfirmationPending = false;
        DeleteButtonText = "删除";
    }
}

public sealed partial class TunnelListItemViewModel : ObservableObject
{
    public TunnelListItemViewModel(TunnelProfile tunnel)
    {
        Tunnel = tunnel;
    }

    [ObservableProperty]
    private bool _isSelected;

    public TunnelProfile Tunnel { get; }

    public string Name => Tunnel.Name;

    public TunnelProtocol Protocol => Tunnel.Protocol;

    public string NodeName => Tunnel.NodeName;

    public string LocalPortText => Tunnel.LocalPort.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string RemoteEndpoint => Tunnel.RemoteEndpoint;

    public string StatusText => Tunnel.Status switch
    {
        FrpNexusStatus.Running => "已启用",
        FrpNexusStatus.Stopped => "已停用",
        FrpNexusStatus.Error => "异常",
        FrpNexusStatus.Warning => "警告",
        _ => "未刷新"
    };

    public bool IsStatusSuccess => Tunnel.Status == FrpNexusStatus.Running;

    public bool IsStatusWarning => Tunnel.Status == FrpNexusStatus.Warning;

    public bool IsStatusError => Tunnel.Status == FrpNexusStatus.Error;

    public bool IsStatusNeutral => Tunnel.Status == FrpNexusStatus.Stopped;

    public string RuntimeActionIcon => Tunnel.Status switch
    {
        FrpNexusStatus.Running => "toggle_off",
        FrpNexusStatus.Stopped => "toggle_on",
        FrpNexusStatus.Error => "refresh",
        _ => "refresh"
    };

    public string RuntimeActionLabel => Tunnel.Status switch
    {
        FrpNexusStatus.Running => "停用",
        FrpNexusStatus.Stopped => "启用",
        FrpNexusStatus.Error => "重试",
        _ => "刷新"
    };
}
