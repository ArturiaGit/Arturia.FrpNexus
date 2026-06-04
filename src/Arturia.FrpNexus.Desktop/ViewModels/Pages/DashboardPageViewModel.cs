using System.Collections.ObjectModel;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.Models;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed class DashboardPageViewModel : PageViewModel
{
    public DashboardPageViewModel()
        : base("仪表盘概览", "查看节点、隧道、运行状态和近期告警")
    {
        Metrics =
        [
            new("节点总数", "24", "DNS", FrpNexusStatus.Ready),
            new("在线节点", "22", "OK", FrpNexusStatus.Online),
            new("运行中进程", "15", "RUN", FrpNexusStatus.Running),
            new("活跃隧道", "48", "TCP", FrpNexusStatus.Ready)
        ];

        RecentNodes =
        [
            new("shanghai-prod-1", "192.168.1.101", 22, "root", "密钥 (id_rsa_prod)", "Ubuntu 22.04 LTS", FrpNexusStatus.Online, FrpNexusStatus.Running, "v0.51.3", "14 天 2 小时", "/etc/frp/frpc.toml"),
            new("beijing-dev-db", "10.0.0.55", 22, "admin", "密码", "Debian 12", FrpNexusStatus.Online, FrpNexusStatus.Running, "v0.51.3", "3 天 5 小时", "/opt/frp/frpc.toml"),
            new("guangzhou-test-edge", "172.16.0.12", 2222, "root", "密钥 (id_rsa_test)", "Ubuntu 20.04 LTS", FrpNexusStatus.Offline, FrpNexusStatus.Stopped, "-", "-", "/etc/frp/frpc.toml"),
            new("hangzhou-storage", "192.168.2.200", 22, "ops", "密钥 (nas_ops)", "Rocky Linux 9", FrpNexusStatus.Warning, FrpNexusStatus.Running, "v0.51.3", "45 天 12 小时", "/srv/frp/frpc.toml")
        ];

        Incidents =
        [
            new("授权失败", "10:45", "节点 'guangzhou-test-edge' 尝试连接被拒绝 (Token mismatch)。", FrpNexusStatus.Error),
            new("端口冲突", "09:12", "隧道 'web-admin-ui' 尝试绑定的远程端口 8080 已被占用。", FrpNexusStatus.Error),
            new("连接超时", "昨天 23:30", "FRP 服务端与节点 'backup-server-01' 的控制连接断开。", FrpNexusStatus.Warning)
        ];

        Logs =
        [
            new("[09:55:01]", "INFO", "local", "frps", "frps tcp listen on 0.0.0.0:7000", FrpNexusStatus.Ready),
            new("[10:12:33]", "OK", "shanghai-prod-1", "frpc", "client [shanghai-prod-1] login success", FrpNexusStatus.Online),
            new("[10:15:42]", "INFO", "shanghai-prod-1", "frpc", "[ssh-dev] proxy added: [tcp]", FrpNexusStatus.Ready),
            new("[10:45:00]", "WARN", "guangzhou-test-edge", "frpc", "token is unverified", FrpNexusStatus.Warning),
            new("[10:45:00]", "ERR", "guangzhou-test-edge", "frpc", "client [guangzhou-test-edge] login failed", FrpNexusStatus.Error)
        ];
    }

    public ObservableCollection<MetricTileViewModel> Metrics { get; }

    public ObservableCollection<NodeProfile> RecentNodes { get; }

    public ObservableCollection<IncidentViewModel> Incidents { get; }

    public ObservableCollection<LogEntry> Logs { get; }
}
