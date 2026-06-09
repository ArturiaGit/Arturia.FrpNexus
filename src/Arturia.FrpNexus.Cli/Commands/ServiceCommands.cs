using Arturia.FrpNexus.Core.InvisibleAirService;
using Cocona;

namespace Arturia.FrpNexus.Cli.Commands;

internal sealed class ServiceCommands(IInvisibleAirService service)
{
    [Command("status", Description = "显示 Phase 7A service/platform 能力说明。")]
    public async Task Status()
    {
        CliOutput.WriteServicePreviewNotice();

        var status = await service.GetStatusAsync();

        Console.WriteLine("InvisibleAirService Phase 7A status");
        Console.WriteLine($"服务状态: {status.ServiceStatus}");
        Console.WriteLine($"运行模式: {status.ServiceMode}");
        Console.WriteLine($"托盘状态: {status.TrayVisibility}");
        Console.WriteLine($"状态信息: {status.StatusMessage}");
        Console.WriteLine();
        WriteSafetyBoundary();
    }

    [Command("preview", Description = "生成 Linux user-level systemd unit 文本预览，不写文件。")]
    public int Preview(
        [Argument(Description = "Tunnel profile id.")] string profileId,
        [Option(Description = "显式 frpnexus 可执行文件路径；Phase 7A 不搜索 PATH。")]
        string frpnexusPath,
        [Option(Description = "显式 frpc 可执行文件路径；Phase 7A 不搜索 PATH。")]
        string frpcPath)
    {
        CliOutput.WriteServicePreviewNotice();

        var preview = service.PreviewUserServiceUnit(new SystemdServiceUnitRequest(profileId, frpnexusPath, frpcPath));

        Console.WriteLine($"Unit 名称: {preview.UnitName}");
        Console.WriteLine();

        if (!preview.IsValid)
        {
            Console.WriteLine("Validation failed:");
            foreach (var error in preview.Errors)
            {
                Console.WriteLine($"- {error}");
            }

            Console.WriteLine();
            WriteSafetyNotes(preview.SafetyNotes);
            return 1;
        }

        Console.WriteLine("----- systemd user service unit preview -----");
        Console.Write(preview.UnitContent);
        Console.WriteLine("----- end preview -----");
        Console.WriteLine();
        WriteSafetyNotes(preview.SafetyNotes);

        return 0;
    }

    [Command("explain", Description = "说明 Phase 7A service preview 的安全边界。")]
    public void Explain()
    {
        CliOutput.WriteServicePreviewNotice();

        Console.WriteLine("Phase 7A 范围:");
        Console.WriteLine("- service status: 只输出 .NET 平台检测和说明性状态。");
        Console.WriteLine("- service preview: 只输出 Linux user-level systemd unit 文本到 stdout。");
        Console.WriteLine("- service explain: 说明安全边界和后续手动步骤。");
        Console.WriteLine();
        Console.WriteLine("systemd preview 使用显式路径:");
        Console.WriteLine("- --frpnexus-path <path> 必填，不假设 frpnexus 在 PATH 中。");
        Console.WriteLine("- --frpc-path <path> 必填，不假设 frpc 在 PATH 中。");
        Console.WriteLine("- Phase 7A 只做路径字符串基本校验，不要求文件真实存在。");
        Console.WriteLine();
        WriteSafetyBoundary();
    }

    private static void WriteSafetyBoundary()
    {
        Console.WriteLine("Phase 7A 安全边界:");
        Console.WriteLine("- 未安装 service。");
        Console.WriteLine("- 未写入 unit 文件，包括 ~/.config/systemd/user 和任何系统目录。");
        Console.WriteLine("- 未执行 systemctl，未 daemon-reload，未 enable，未 start，未 stop。");
        Console.WriteLine("- 未启用自启动，未启动后台服务。");
        Console.WriteLine("- 未调用 sudo、pkexec、UAC、PowerShell elevation 或任何提权操作。");
        Console.WriteLine("- 未实现 system-level service、Windows Service、macOS LaunchAgent、GUI tray 或后台常驻 daemon。");
        Console.WriteLine("- 未下载 frpc，未搜索 PATH，未 kill、attach、搜索或接管外部进程。");
    }

    private static void WriteSafetyNotes(IReadOnlyList<string> safetyNotes)
    {
        Console.WriteLine("Safety notes:");
        foreach (var note in safetyNotes)
        {
            Console.WriteLine($"- {note}");
        }
        Console.WriteLine("- 未安装 service，未写入 unit 文件，未执行 systemctl，未启用自启动，未启动后台服务。");
    }
}
