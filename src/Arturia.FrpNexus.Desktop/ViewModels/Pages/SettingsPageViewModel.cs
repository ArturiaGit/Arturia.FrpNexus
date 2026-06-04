using System.Collections.ObjectModel;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed class SettingsPageViewModel : PageViewModel
{
    public SettingsPageViewModel()
        : base("设置", "配置界面偏好、FRP 下载源、本地路径和 SSH 密钥")
    {
        SshKeys =
        [
            new("id_rsa_prod_server", "SHA256: 4a6x9p...L8Q=", "生产服务器"),
            new("id_ed25519_home_nas", "SHA256: zT91bc...K3M=", "家庭 NAS")
        ];
    }

    public ObservableCollection<SshKeyViewModel> SshKeys { get; }
}
