using Arturia.FrpNexus.Core.Configuration;
using Cocona;

namespace Arturia.FrpNexus.Cli.Commands;

internal sealed class ConfigCommands(IFrpNexusSettingsStore settingsStore)
{
    [Command("show", Description = "显示 SQLite 持久化配置。")]
    public async Task Show()
    {
        CliOutput.WriteConfigNotice();
        WriteSettings(await settingsStore.LoadAsync());
    }

    [Command("get", Description = "读取配置项。当前支持 frpc-path。")]
    public async Task<int> Get([Argument(Description = "配置键。当前支持 frpc-path。")]
        string key)
    {
        CliOutput.WriteConfigNotice();

        if (!IsFrpcPathKey(key))
        {
            Console.WriteLine($"不支持的配置键: {key}");
            Console.WriteLine("当前支持: frpc-path");
            return 1;
        }

        var settings = await settingsStore.LoadAsync();
        Console.WriteLine(string.IsNullOrWhiteSpace(settings.FrpcPath)
            ? "frpc-path 未设置。"
            : settings.FrpcPath);

        return 0;
    }

    [Command("set", Description = "设置配置项。当前支持 frpc-path。")]
    public async Task<int> Set(
        [Argument(Description = "配置键。当前支持 frpc-path。")]
        string key,
        [Argument(Description = "配置值。")]
        string value)
    {
        CliOutput.WriteConfigNotice();

        if (!IsFrpcPathKey(key))
        {
            Console.WriteLine($"不支持的配置键: {key}");
            Console.WriteLine("当前支持: frpc-path");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            Console.WriteLine("frpc-path 不能为空。");
            return 1;
        }

        var settings = await settingsStore.LoadAsync();
        await settingsStore.SaveAsync(settings with { FrpcPath = value });

        Console.WriteLine($"已保存 frpc-path: {value}");
        Console.WriteLine("未验证该路径是否存在；Phase 8B 不搜索 PATH，不自动下载 frpc。");
        return 0;
    }

    private static bool IsFrpcPathKey(string key)
    {
        return key.Equals("frpc-path", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteSettings(FrpNexusSettings settings)
    {
        Console.WriteLine("FrpNexus 配置");
        Console.WriteLine($"version: {settings.Version}");
        Console.WriteLine($"frpc-path: {(string.IsNullOrWhiteSpace(settings.FrpcPath) ? "未设置" : settings.FrpcPath)}");
        Console.WriteLine($"active-profile-id: {settings.ActiveProfileId ?? "未设置"}");
        Console.WriteLine($"minimize-to-tray-on-close: {settings.MinimizeToTrayOnClose}");
    }
}
