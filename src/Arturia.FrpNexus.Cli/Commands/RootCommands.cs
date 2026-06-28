using Arturia.FrpNexus.Core.AvalonDaemon;
using Arturia.FrpNexus.Core.Configuration;
using Arturia.FrpNexus.Core.ExcaliburTunnel;
using Cocona;

namespace Arturia.FrpNexus.Cli.Commands;

internal sealed class RootCommands(IAvalonDaemon daemon, ITunnelProfileRepository profileRepository, IFrpNexusSettingsStore settingsStore)
{
    [Command("run", Description = "从 SQLite profile 以前台模式启动当前 CLI 进程管理的 frpc client。")]
    public async Task<int> Run(
        [Argument(Description = "要启动的隧道 profile Id。")] string profileId,
        [Option(Description = "frpc binary 路径；优先级高于 SQLite settings.frpcPath 和 FRPNEXUS_FRPC_PATH。")] string? frpcPath = null,
        [Option(Description = "保留 OS temp/FrpNexus 下生成的临时 TOML 配置。")]
        bool keepGeneratedConfig = false)
    {
        CliOutput.WritePhaseNotice();

        var profile = await profileRepository.FindByIdAsync(profileId);
        if (profile is null)
        {
            Console.WriteLine($"未找到 profile: {profileId}");
            Console.WriteLine("请先使用 profile add 创建持久化 profile。Phase 8B 不再 fallback 到默认 profile。");
            return 1;
        }

        var settings = await settingsStore.LoadAsync();
        var resolvedFrpcPath = ResolveFrpcPath(frpcPath, settings);
        if (string.IsNullOrWhiteSpace(resolvedFrpcPath))
        {
            Console.WriteLine("frpc 路径未设置。");
            Console.WriteLine("请使用 --frpc-path、config set frpc-path <path> 或 FRPNEXUS_FRPC_PATH 提供路径。");
            Console.WriteLine("Phase 8B 不搜索 PATH，不自动下载 frpc，也不会启动 frpc。");
            return 1;
        }

        Console.WriteLine($"Profile: {profile.Id}");
        Console.WriteLine($"协议: {profile.Protocol}");
        Console.WriteLine($"本地: {profile.LocalHost}:{profile.LocalPort}");
        Console.WriteLine($"远端端口: {profile.RemotePort}");
        Console.WriteLine($"FRP 服务端: {profile.ServerAddress}:{profile.ServerPort}");
        Console.WriteLine($"frpc 路径: {(string.IsNullOrWhiteSpace(resolvedFrpcPath) ? "未提供" : resolvedFrpcPath)}");
        Console.WriteLine();

        await daemon.StartAsync(new StartTunnelRequest(profile, resolvedFrpcPath, keepGeneratedConfig));
        var snapshot = await daemon.GetSnapshotAsync();

        Console.WriteLine($"状态: {snapshot.Status}");
        Console.WriteLine($"活动 Profile: {snapshot.ActiveProfileId ?? "无"}");
        Console.WriteLine($"健康信息: {snapshot.HealthMessage}");

        WriteLogs(snapshot.RecentLogs);

        if (snapshot.Status != RuntimeStatus.Running)
        {
            return 1;
        }

        Console.WriteLine("frpc 正以前台模式运行。按 Ctrl+C 停止当前 FrpNexus CLI 进程启动的 frpc 子进程。");
        await WaitUntilStoppedByCtrlC();
        return 0;
    }

    [Command("help", Description = "显示 Phase 6 CLI 说明。")]
    public void Help()
    {
        CliOutput.WritePhaseNotice();

        Console.WriteLine("可用命令:");
        Console.WriteLine("  run <profileId> --frpc-path <path>   前台启动 frpc client");
        Console.WriteLine("  daemon status                      说明 Phase 6 不支持跨进程 daemon 状态");
        Console.WriteLine("  daemon stop                        说明 Phase 6 不支持跨进程 daemon 停止");
        Console.WriteLine("  tunnel preview                     输出 Phase 6 TCP/UDP FRP TOML 预览");
        Console.WriteLine("  tunnel validate                    执行 Phase 6 TCP/UDP validation");
        Console.WriteLine("  config show                        显示 SQLite 持久化配置");
        Console.WriteLine("  profile list                       列出 SQLite 持久化 profiles");
        Console.WriteLine("  service status       显示 InvisibleAirService mock 状态");
        Console.WriteLine();
        Console.WriteLine("run profile 来源: SQLite tunnel_profiles。找不到 profile 时失败，不 fallback 到默认 profile。");
        Console.WriteLine("frpc 路径解析顺序: --frpc-path -> SQLite settings.frpcPath -> FRPNEXUS_FRPC_PATH。Phase 8B 不搜索 PATH，不自动下载 frpc。");
    }

    private async Task WaitUntilStoppedByCtrlC()
    {
        var stopRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        ConsoleCancelEventHandler handler = (_, args) =>
        {
            args.Cancel = true;
            stopRequested.TrySetResult();
        };

        Console.CancelKeyPress += handler;
        try
        {
            while (true)
            {
                var snapshot = await daemon.GetSnapshotAsync();
                if (snapshot.Status is not (RuntimeStatus.Running or RuntimeStatus.Starting))
                {
                    Console.WriteLine($"frpc 已退出，状态: {snapshot.Status}，健康信息: {snapshot.HealthMessage}");
                    WriteLogs(snapshot.RecentLogs);
                    return;
                }

                var delay = Task.Delay(TimeSpan.FromSeconds(1));
                var completed = await Task.WhenAny(stopRequested.Task, delay);
                if (completed == stopRequested.Task)
                {
                    Console.WriteLine("收到 Ctrl+C，正在停止当前 FrpNexus CLI 进程启动的 frpc 子进程...");
                    await daemon.StopAsync();
                    var finalSnapshot = await daemon.GetSnapshotAsync();
                    Console.WriteLine($"状态: {finalSnapshot.Status}");
                    Console.WriteLine($"健康信息: {finalSnapshot.HealthMessage}");
                    WriteLogs(finalSnapshot.RecentLogs);
                    return;
                }
            }
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }

    private static string ResolveFrpcPath(string? frpcPath, FrpNexusSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(frpcPath))
        {
            return frpcPath;
        }

        if (!string.IsNullOrWhiteSpace(settings.FrpcPath))
        {
            return settings.FrpcPath;
        }

        return Environment.GetEnvironmentVariable("FRPNEXUS_FRPC_PATH") ?? string.Empty;
    }

    private static void WriteLogs(IReadOnlyList<DaemonLogEntry> logs)
    {
        if (logs.Count == 0)
        {
            return;
        }

        Console.WriteLine("最近日志:");
        foreach (var log in logs.TakeLast(10))
        {
            Console.WriteLine($"  [{log.Timestamp:HH:mm:ss} {log.Level}] {log.Source}: {log.Message}");
        }
    }
}
