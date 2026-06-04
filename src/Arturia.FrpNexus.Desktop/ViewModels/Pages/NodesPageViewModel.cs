using System.Collections.ObjectModel;
using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed class NodesPageViewModel : PageViewModel
{
    public NodesPageViewModel()
        : base("节点管理", "管理远程 Linux 节点并为 SSH/SFTP 工作流预留入口")
    {
        Nodes =
        [
            new("Web-Server-HK", "103.114.160.22", 22, "root", "密钥 (ID_RSA_HK)", "Linux x86_64 (Ubuntu 22.04 LTS)", FrpNexusStatus.Online, FrpNexusStatus.Running, "v0.51.3", "4d 12h 30m", "/etc/frp/frpc.toml"),
            new("DB-Node-SH", "47.101.44.112", 22, "deploy", "密钥 (ID_RSA_SH)", "Debian 12", FrpNexusStatus.Online, FrpNexusStatus.Stopped, "v0.51.3", "-", "/opt/frp/frpc.toml"),
            new("Edge-Router-BJ", "123.56.77.89", 2222, "root", "密码", "Ubuntu 20.04 LTS", FrpNexusStatus.Offline, FrpNexusStatus.Stopped, "-", "-", "/etc/frp/frpc.toml")
        ];

        SelectedNode = Nodes[0];
    }

    public ObservableCollection<NodeProfile> Nodes { get; }

    public NodeProfile SelectedNode { get; }
}
