using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed partial class TunnelsPageViewModel : PageViewModel
{
    private readonly ITunnelManagementService _tunnelManagementService;

    [ObservableProperty]
    private string _tunnelCountText = "共 0 条记录";

    public TunnelsPageViewModel(ITunnelManagementService tunnelManagementService)
        : base("隧道管理", "创建和检查 TCP、UDP、HTTP、HTTPS 隧道配置")
    {
        _tunnelManagementService = tunnelManagementService;
        Tunnels = [];

        _ = LoadTunnelsAsync();
    }

    public ObservableCollection<TunnelProfile> Tunnels { get; }

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

        TunnelCountText = $"共 {Tunnels.Count} 条记录";
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
}
