using Arturia.FrpNexus.Core.AvalonDaemon;
using Cocona;

namespace Arturia.FrpNexus.Cli.Commands;

internal sealed class DaemonCommands(IAvalonDaemon daemon)
{
    [Command("status", Description = "说明 Phase 6 不支持跨进程 daemon 状态。")]
    public async Task Status()
    {
        CliOutput.WritePhaseNotice();
        CliOutput.WriteCrossProcessNotice();

        var snapshot = await daemon.GetSnapshotAsync();

        Console.WriteLine("当前 CLI 进程内 runtime snapshot");
        Console.WriteLine($"状态: {snapshot.Status}");
        Console.WriteLine($"活动 Profile: {snapshot.ActiveProfileId ?? "无"}");
        Console.WriteLine($"健康信息: {snapshot.HealthMessage}");
        Console.WriteLine("最近日志:");

        foreach (var log in snapshot.RecentLogs)
        {
            Console.WriteLine($"  [{log.Timestamp:HH:mm:ss} {log.Level}] {log.Source}: {log.Message}");
        }
    }

    [Command("stop", Description = "说明 Phase 6 不支持跨进程 daemon 停止。")]
    public void Stop()
    {
        CliOutput.WritePhaseNotice();
        CliOutput.WriteCrossProcessNotice();
        Console.WriteLine("daemon stop 不会查找、attach 或 kill 外部 frpc 进程。");
        Console.WriteLine("只有 run 命令所在的当前 CLI 进程会在 Ctrl+C 时停止自己启动的 frpc 子进程。");
    }
}
