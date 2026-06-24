using Arturia.FrpNexus.Core.ExcaliburTunnel;
using Cocona;

namespace Arturia.FrpNexus.Cli.Commands;

internal sealed class ProfileCommands(ITunnelProfileRepository repository, IExcaliburTunnel tunnel)
{
    [Command("list", Description = "列出 SQLite 持久化 tunnel profiles。")]
    public async Task List()
    {
        CliOutput.WriteConfigNotice();

        var profiles = await repository.ListAsync();
        if (profiles.Count == 0)
        {
            Console.WriteLine("尚未创建 tunnel profile。");
            return;
        }

        foreach (var profile in profiles.OrderBy(profile => profile.Id, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"{profile.Id} | {profile.Protocol} | {profile.LocalHost}:{profile.LocalPort} -> remote:{profile.RemotePort} | server:{profile.ServerAddress}:{profile.ServerPort} | enabled:{profile.Enabled}");
        }
    }

    [Command("show", Description = "显示指定 tunnel profile。")]
    public async Task<int> Show([Argument(Description = "Tunnel profile id.")] string id)
    {
        CliOutput.WriteConfigNotice();

        var profile = await repository.FindByIdAsync(id);
        if (profile is null)
        {
            Console.WriteLine($"未找到 profile: {id}");
            return 1;
        }

        WriteProfile(profile);
        return 0;
    }

    [Command("add", Description = "新增或更新 SQLite tunnel profile。")]
    public async Task<int> Add(
        [Argument(Description = "Tunnel profile id.")] string id,
        [Option(Description = "Profile 显示名称。")]
        string? name = null,
        [Option(Description = "协议。Phase 8B 沿用 Phase 6 validation，仅 tcp/udp 可通过。")]
        string protocol = "tcp",
        [Option(Description = "本地服务地址。")]
        string localHost = "127.0.0.1",
        [Option(Description = "本地服务端口。")]
        int localPort = 8080,
        [Option(Description = "远端端口。")]
        int remotePort = 18080,
        [Option(Description = "FRP 服务端地址。")]
        string serverAddress = "frp.example.internal",
        [Option(Description = "FRP 服务端端口。")]
        int serverPort = 7000,
        [Option(Description = "保存为禁用状态。")]
        bool disabled = false)
    {
        CliOutput.WriteConfigNotice();

        if (string.IsNullOrWhiteSpace(id))
        {
            Console.WriteLine("profile id 不能为空。");
            return 1;
        }

        var profile = new TunnelProfile(
            id,
            string.IsNullOrWhiteSpace(name) ? id : name,
            ParseProtocol(protocol),
            localHost,
            localPort,
            remotePort,
            serverAddress,
            serverPort,
            !disabled);

        var validation = tunnel.Validate(profile);
        if (!validation.IsValid)
        {
            Console.WriteLine("profile validation 失败:");
            foreach (var error in validation.Errors)
            {
                Console.WriteLine($"- {error}");
            }

            return 1;
        }

        await repository.SaveAsync(profile);

        Console.WriteLine($"已保存 profile: {profile.Id}");
        WriteProfile(profile);
        return 0;
    }

    [Command("remove", Description = "删除 SQLite tunnel profile。")]
    public async Task<int> Remove([Argument(Description = "Tunnel profile id.")] string id)
    {
        CliOutput.WriteConfigNotice();

        var deleted = await repository.DeleteAsync(id);
        if (!deleted)
        {
            Console.WriteLine($"未找到 profile: {id}");
            return 1;
        }

        Console.WriteLine($"已删除 profile: {id}");
        return 0;
    }

    private static TunnelProtocol ParseProtocol(string protocol)
    {
        return protocol.ToLowerInvariant() switch
        {
            "tcp" => TunnelProtocol.Tcp,
            "udp" => TunnelProtocol.Udp,
            "http" => TunnelProtocol.Http,
            "https" => TunnelProtocol.Https,
            _ => TunnelProtocol.Tcp
        };
    }

    private static void WriteProfile(TunnelProfile profile)
    {
        Console.WriteLine($"id: {profile.Id}");
        Console.WriteLine($"name: {profile.Name}");
        Console.WriteLine($"protocol: {profile.Protocol}");
        Console.WriteLine($"local: {profile.LocalHost}:{profile.LocalPort}");
        Console.WriteLine($"remote-port: {profile.RemotePort}");
        Console.WriteLine($"server: {profile.ServerAddress}:{profile.ServerPort}");
        Console.WriteLine($"enabled: {profile.Enabled}");
    }
}
