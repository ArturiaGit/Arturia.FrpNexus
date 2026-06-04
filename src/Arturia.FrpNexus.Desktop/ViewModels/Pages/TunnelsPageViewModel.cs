using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed partial class TunnelsPageViewModel : PageViewModel
{
    private readonly ITunnelManagementService _tunnelManagementService;
    private bool _isDeleteConfirmationPending;
    private string? _editingOriginalName;

    [ObservableProperty]
    private string _tunnelCountText = "共 0 条记录";

    [ObservableProperty]
    private TunnelProfile? _selectedTunnel;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private string _editorTitle = "隧道详情";

    [ObservableProperty]
    private string _formName = string.Empty;

    [ObservableProperty]
    private TunnelProtocol _formProtocol = TunnelProtocol.Http;

    [ObservableProperty]
    private string _formNodeName = string.Empty;

    [ObservableProperty]
    private string _formLocalAddress = "127.0.0.1";

    [ObservableProperty]
    private string _formLocalPort = "8080";

    [ObservableProperty]
    private string _formRemoteEndpoint = string.Empty;

    [ObservableProperty]
    private string _formStatusDetail = "本地记录";

    [ObservableProperty]
    private string _formErrorText = string.Empty;

    [ObservableProperty]
    private string _deleteButtonText = "删除";

    public TunnelsPageViewModel(ITunnelManagementService tunnelManagementService)
        : base("隧道管理", "创建和检查 TCP、UDP、HTTP、HTTPS 隧道配置")
    {
        _tunnelManagementService = tunnelManagementService;
        Tunnels = [];

        _ = LoadTunnelsAsync();
    }

    public ObservableCollection<TunnelProfile> Tunnels { get; }

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
        TunnelCountText = $"共 {Tunnels.Count} 条记录";
    }

    [RelayCommand]
    private void StartCreateTunnel()
    {
        _editingOriginalName = null;
        ResetDeleteConfirmation();
        EditorTitle = "新建隧道";
        FormName = string.Empty;
        FormProtocol = TunnelProtocol.Http;
        FormNodeName = SelectedTunnel?.NodeName ?? "Node-Alpha-HK";
        FormLocalAddress = "127.0.0.1";
        FormLocalPort = "8080";
        FormRemoteEndpoint = string.Empty;
        FormStatusDetail = "本地记录";
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
        FormErrorText = string.Empty;
        _editingOriginalName = null;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        ResetDeleteConfirmation();
        IsEditorOpen = false;
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
        FormErrorText = string.Empty;
    }

    partial void OnSelectedTunnelChanged(TunnelProfile? value)
    {
        ResetDeleteConfirmation();
    }

    private static IReadOnlyList<TunnelProfile> CreateSeedTunnels()
    {
        return
        [
            new("web-dev-portal", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中"),
            new("ssh-bastion", TunnelProtocol.Tcp, "Node-Beta-SG", "127.0.0.1", 22, "60022", FrpNexusStatus.Running, "运行中"),
            new("udp-game-server", TunnelProtocol.Udp, "Node-Gamma-JP", "127.0.0.1", 7777, "7777", FrpNexusStatus.Error, "端口被占用"),
            new("secure-api", TunnelProtocol.Https, "Node-Alpha-HK", "127.0.0.1", 8443, "api.example.com", FrpNexusStatus.Warning, "证书待检查")
        ];
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

        if (string.IsNullOrWhiteSpace(FormLocalAddress))
        {
            FormErrorText = "本地地址不能为空。";
            return false;
        }

        if (!int.TryParse(FormLocalPort, out var localPort) || localPort is < 1 or > 65535)
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
            FormLocalAddress.Trim(),
            localPort,
            FormRemoteEndpoint.Trim(),
            FrpNexusStatus.Stopped,
            string.IsNullOrWhiteSpace(FormStatusDetail) ? "本地记录" : FormStatusDetail.Trim());

        return true;
    }

    private void ResetDeleteConfirmation()
    {
        _isDeleteConfirmationPending = false;
        DeleteButtonText = "删除";
    }
}
