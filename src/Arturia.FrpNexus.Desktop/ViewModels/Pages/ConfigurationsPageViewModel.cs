using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed class ConfigurationsPageViewModel : PageViewModel
{
    public ConfigurationsPageViewModel()
        : base("配置", "通过表单生成 TOML，并预览即将上传的 frpc 配置")
    {
        Preview = new ConfigurationPreview(
            "web_proxy_01",
            TunnelProtocol.Http,
            "127.0.0.1",
            8080,
            "example.com",
            """
            [[proxies]]
            name = "web_proxy_01"
            type = "http"
            localIP = "127.0.0.1"
            localPort = 8080
            customDomains = ["example.com"]

            # Advanced settings would appear here
            # encryption = true
            """);
    }

    public ConfigurationPreview Preview { get; }
}
