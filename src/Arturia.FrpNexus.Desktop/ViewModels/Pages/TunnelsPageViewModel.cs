using System.Collections.ObjectModel;
using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed class TunnelsPageViewModel : PageViewModel
{
    public TunnelsPageViewModel()
        : base("隧道管理", "创建和检查 TCP、UDP、HTTP、HTTPS 隧道配置")
    {
        Tunnels =
        [
            new("web-dev-portal", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中"),
            new("ssh-bastion", TunnelProtocol.Tcp, "Node-Beta-SG", "127.0.0.1", 22, "60022", FrpNexusStatus.Running, "运行中"),
            new("database-test", TunnelProtocol.Tcp, "Node-Alpha-HK", "127.0.0.1", 3306, "63306", FrpNexusStatus.Stopped, "已停止"),
            new("udp-game-server", TunnelProtocol.Udp, "Node-Gamma-JP", "127.0.0.1", 7777, "7777", FrpNexusStatus.Error, "端口被占用"),
            new("secure-api", TunnelProtocol.Https, "Node-Alpha-HK", "127.0.0.1", 8443, "api.example.com", FrpNexusStatus.Warning, "证书待检查")
        ];
    }

    public ObservableCollection<TunnelProfile> Tunnels { get; }
}
