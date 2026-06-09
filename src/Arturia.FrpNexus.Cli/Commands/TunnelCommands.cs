using Arturia.FrpNexus.Core.ExcaliburTunnel;
using Cocona;

namespace Arturia.FrpNexus.Cli.Commands;

internal sealed class TunnelCommands(IExcaliburTunnel tunnel)
{
    [Command("preview", Description = "输出 Phase 6 TCP/UDP FRP TOML 预览，不写入文件。")]
    public void Preview(
        [Option(Description = "隧道 profile Id。")]
        string profileId = "my-server",
        [Option(Description = "隧道协议。Phase 6 仅支持 tcp/udp。")]
        string protocol = "tcp")
    {
        CliOutput.WritePhaseNotice();

        var profile = CliProfileFactory.Create(profileId, protocol);
        var preview = tunnel.PreviewConfiguration(profile);

        Console.WriteLine("ExcaliburTunnel Phase 6 TOML preview");
        Console.WriteLine("以下内容为本项目最小 TCP/UDP frpc TOML 子集预览，不会写入文件:");
        Console.WriteLine(preview);
    }

    [Command("validate", Description = "执行 Phase 6 TCP/UDP validation。")]
    public void Validate(
        [Option(Description = "隧道 profile Id。")]
        string profileId = "my-server",
        [Option(Description = "隧道协议。Phase 6 仅支持 tcp/udp。")]
        string protocol = "tcp")
    {
        CliOutput.WritePhaseNotice();

        var profile = CliProfileFactory.Create(profileId, protocol);
        var result = tunnel.Validate(profile);

        Console.WriteLine("ExcaliburTunnel Phase 6 validation");
        Console.WriteLine($"Profile: {profile.Id} / {profile.Name}");
        Console.WriteLine($"结果: {(result.IsValid ? "通过" : "失败")}");

        foreach (var error in result.Errors)
        {
            Console.WriteLine($"  - {error}");
        }
    }
}
