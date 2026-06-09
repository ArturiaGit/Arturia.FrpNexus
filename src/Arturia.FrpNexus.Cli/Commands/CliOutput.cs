namespace Arturia.FrpNexus.Cli.Commands;

internal static class CliOutput
{
    public static void WritePhaseNotice()
    {
        Console.WriteLine("FrpNexus CLI - Phase 6 frpc client integration");
        Console.WriteLine("run 命令以前台模式启动当前 CLI 进程管理的 frpc；不提供后台常驻或跨进程 daemon 控制。");
        Console.WriteLine();
    }

    public static void WriteCrossProcessNotice()
    {
        Console.WriteLine("Phase 6 不支持跨进程 daemon 状态/停止；真实后台服务化留到 Phase 7。");
    }

    public static void WriteServicePreviewNotice()
    {
        Console.WriteLine("FrpNexus CLI - Phase 7A systemd preview only");
        Console.WriteLine("只生成 Linux user-level systemd unit 文本预览；不安装 service，不写 unit 文件，不执行 systemctl。");
        Console.WriteLine();
    }

    public static void WriteConfigNotice()
    {
        Console.WriteLine("FrpNexus CLI - Phase 8B LiteDB config/profile persistence");
        Console.WriteLine("CLI 使用本地 LiteDB 配置；不修改 Desktop GUI，不启动 frpc，不执行 systemctl。");
        Console.WriteLine();
    }
}
