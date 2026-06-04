using System.Collections.ObjectModel;
using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed class RuntimePageViewModel : PageViewModel
{
    public RuntimePageViewModel()
        : base("运行", "查看远程 FRP 进程状态，并预留启动、停止、重启操作")
    {
        Processes =
        [
            new("frps-main", "Web-Server-HK", "frps", FrpNexusStatus.Running, "14022", "4d 12h 30m", "0.0.0.0:7000"),
            new("frpc-web", "Web-Server-HK", "frpc", FrpNexusStatus.Running, "14090", "4d 10h 12m", "127.0.0.1:8080"),
            new("frpc-db", "DB-Node-SH", "frpc", FrpNexusStatus.Stopped, "-", "-", "127.0.0.1:3306"),
            new("frpc-edge", "Edge-Router-BJ", "frpc", FrpNexusStatus.Error, "-", "连接失败", "127.0.0.1:7777")
        ];

        DeploymentSteps =
        [
            new("测试 SSH 连接", "确认远程 Linux 节点凭据可用", FrpNexusStatus.Ready),
            new("下载 FRP Release", "选择适合目标系统的 frpc / frps", FrpNexusStatus.Pending),
            new("通过 SFTP 上传核心", "上传二进制文件与 TOML 配置", FrpNexusStatus.Pending),
            new("启动远程进程", "执行启动命令并读取状态", FrpNexusStatus.Pending)
        ];
    }

    public ObservableCollection<RuntimeProcess> Processes { get; }

    public ObservableCollection<RuntimeStepViewModel> DeploymentSteps { get; }
}
