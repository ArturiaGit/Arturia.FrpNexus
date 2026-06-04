using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed partial class NodesPageViewModel : PageViewModel
{
    private readonly INodeManagementService _nodeManagementService;

    [ObservableProperty]
    private NodeProfile? _selectedNode;

    [ObservableProperty]
    private string _nodeCountText = "共 0 个节点";

    public NodesPageViewModel(INodeManagementService nodeManagementService)
        : base("节点管理", "管理远程 Linux 节点并为 SSH/SFTP 工作流预留入口")
    {
        _nodeManagementService = nodeManagementService;
        Nodes = [];

        _ = LoadNodesAsync();
    }

    public ObservableCollection<NodeProfile> Nodes { get; }

    public async Task LoadNodesAsync(CancellationToken cancellationToken = default)
    {
        var nodes = await _nodeManagementService.ListNodesAsync(cancellationToken);

        if (nodes.Count == 0)
        {
            nodes = CreateSeedNodes();

            foreach (var node in nodes)
            {
                await _nodeManagementService.SaveNodeAsync(node, cancellationToken);
            }
        }

        Nodes.Clear();
        foreach (var node in nodes)
        {
            Nodes.Add(node);
        }

        SelectedNode = Nodes.FirstOrDefault();
        NodeCountText = $"共 {Nodes.Count} 个节点";
    }

    private static IReadOnlyList<NodeProfile> CreateSeedNodes()
    {
        return
        [
            new("Web-Server-HK", "103.114.160.22", 22, "root", "密钥 (ID_RSA_HK)", "Linux x86_64 (Ubuntu 22.04 LTS)", FrpNexusStatus.Online, FrpNexusStatus.Running, "v0.51.3", "4d 12h 30m", "/etc/frp/frpc.toml"),
            new("DB-Node-SH", "47.101.44.112", 22, "deploy", "密钥 (ID_RSA_SH)", "Debian 12", FrpNexusStatus.Online, FrpNexusStatus.Stopped, "v0.51.3", "-", "/opt/frp/frpc.toml"),
            new("Edge-Router-BJ", "123.56.77.89", 2222, "root", "密钥 (ID_RSA_BJ)", "Ubuntu 20.04 LTS", FrpNexusStatus.Offline, FrpNexusStatus.Stopped, "-", "-", "/etc/frp/frpc.toml")
        ];
    }
}
