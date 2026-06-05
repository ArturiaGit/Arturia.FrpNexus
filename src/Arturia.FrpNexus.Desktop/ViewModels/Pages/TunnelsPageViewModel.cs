using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed partial class TunnelsPageViewModel : PageViewModel
{
    public const string DefaultLocalAddress = "127.0.0.1";
    public const string DefaultLocalPort = "8080";
    public const string DefaultStatusDetail = "本地记录";

    private readonly ITunnelManagementService _tunnelManagementService;
    private bool _isDeleteConfirmationPending;
    private string? _editingOriginalName;

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
    private string _formStatusDetail = string.Empty;

    [ObservableProperty]
    private string _formErrorText = string.Empty;

    [ObservableProperty]
    private string _deleteButtonText = "删除";

    public TunnelsPageViewModel(ITunnelManagementService tunnelManagementService)
        : base("隧道管理", "创建和检查 TCP、UDP、HTTP、HTTPS 隧道配置")
    {
        _tunnelManagementService = tunnelManagementService;
        Tunnels = [];
        TunnelRows = [];

        _ = LoadTunnelsAsync();
    }

    public ObservableCollection<TunnelProfile> Tunnels { get; }

    public ObservableCollection<TunnelListItemViewModel> TunnelRows { get; }

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

    public async Task LoadTunnelsAsync(CancellationToken cancellationToken = default)
    {
        var tunnels = await _tunnelManagementService.ListTunnelsAsync(cancellationToken);

        if (tunnels.Count == 0)
        {
            tunnels = CreateSeedTunnels();

            foreach (var tunnel in tunnels)
            {
                await _tunnelManagementService.SaveTunnelAsync(tunnel, cancellationToken);
            }
        }

        Tunnels.Clear();
        foreach (var tunnel in tunnels)
        {
            Tunnels.Add(tunnel);
        }

        SelectedTunnel = Tunnels.FirstOrDefault();
        ApplyFilters();
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
    private void StartEditTunnel(TunnelListItemViewModel? row)
    {
        if (row is not null)
        {
            SelectedTunnel = row.Tunnel;
        }

        StartEditSelectedTunnel();
    }

    [RelayCommand]
    private void StartCreateTunnel()
    {
        _editingOriginalName = null;
        ResetDeleteConfirmation();
        IsEditingExistingTunnel = false;
        EditorTitle = "新建隧道";
        FormName = string.Empty;
        FormProtocol = TunnelProtocol.Http;
        FormNodeName = string.Empty;
        FormLocalAddress = string.Empty;
        FormLocalPort = string.Empty;
        FormRemoteEndpoint = string.Empty;
        FormStatusDetail = string.Empty;
        FormErrorText = string.Empty;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void StartEditSelectedTunnel()
    {
        if (SelectedTunnel is null)
        {
            FormErrorText = "请先选择一条隧道记录。";
            return;
        }

        _editingOriginalName = SelectedTunnel.Name;
        ResetDeleteConfirmation();
        IsEditingExistingTunnel = true;
        EditorTitle = "编辑隧道";
        FormName = SelectedTunnel.Name;
        FormProtocol = SelectedTunnel.Protocol;
        FormNodeName = SelectedTunnel.NodeName;
        FormLocalAddress = SelectedTunnel.LocalAddress;
        FormLocalPort = SelectedTunnel.LocalPort.ToString();
        FormRemoteEndpoint = SelectedTunnel.RemoteEndpoint;
        FormStatusDetail = SelectedTunnel.StatusDetail;
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

    private static IReadOnlyList<TunnelProfile> CreateSeedTunnels()
    {
        return
        [
            new("web-dev-portal", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中"),
            new("ssh-bastion", TunnelProtocol.Tcp, "Node-Beta-SG", "127.0.0.1", 22, "60022", FrpNexusStatus.Running, "运行中"),
            new("database-test", TunnelProtocol.Tcp, "Node-Alpha-HK", "127.0.0.1", 3306, "13306", FrpNexusStatus.Stopped, "已停止"),
            new("udp-game-server", TunnelProtocol.Udp, "Node-Gamma-JP", "127.0.0.1", 7777, "7777", FrpNexusStatus.Error, "端口被占用")
        ];
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
            TunnelRows.Add(new TunnelListItemViewModel(tunnel));
        }

        SyncSelectedRows();
        TunnelCountText = $"共 {TunnelRows.Count} 条记录";
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

        if (string.IsNullOrWhiteSpace(FormNodeName))
        {
            FormErrorText = "关联节点不能为空。";
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
            ValueOrDefault(FormStatusDetail, DefaultStatusDetail));

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
        FrpNexusStatus.Running => "运行中",
        FrpNexusStatus.Stopped => "已停止",
        FrpNexusStatus.Error => "异常",
        FrpNexusStatus.Warning => "警告",
        _ => Tunnel.StatusDetail
    };

    public string StatusDetail => Tunnel.StatusDetail;

    public bool HasStatusDetail => Tunnel.Status == FrpNexusStatus.Error && !string.IsNullOrWhiteSpace(Tunnel.StatusDetail);

    public bool IsStatusSuccess => Tunnel.Status == FrpNexusStatus.Running;

    public bool IsStatusWarning => Tunnel.Status == FrpNexusStatus.Warning;

    public bool IsStatusError => Tunnel.Status == FrpNexusStatus.Error;

    public bool IsStatusNeutral => Tunnel.Status == FrpNexusStatus.Stopped;

    public string RuntimeActionIcon => Tunnel.Status switch
    {
        FrpNexusStatus.Running => "stop_circle",
        FrpNexusStatus.Stopped => "play_circle",
        FrpNexusStatus.Error => "refresh",
        _ => "refresh"
    };

    public string RuntimeActionLabel => Tunnel.Status switch
    {
        FrpNexusStatus.Running => "停止",
        FrpNexusStatus.Stopped => "启动",
        FrpNexusStatus.Error => "重试",
        _ => "刷新"
    };
}
