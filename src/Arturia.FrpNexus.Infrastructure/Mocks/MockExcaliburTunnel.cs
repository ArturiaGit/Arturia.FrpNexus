using Arturia.FrpNexus.Core.ExcaliburTunnel;

namespace Arturia.FrpNexus.Infrastructure.Mocks;

public sealed class MockExcaliburTunnel : IExcaliburTunnel
{
    public TunnelValidationResult Validate(TunnelProfile profile)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            errors.Add("隧道 Id 不能为空。");
        }

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            errors.Add("隧道名称不能为空。");
        }

        if (string.IsNullOrWhiteSpace(profile.LocalHost))
        {
            errors.Add("本地地址不能为空。");
        }

        if (string.IsNullOrWhiteSpace(profile.ServerAddress))
        {
            errors.Add("服务端地址不能为空。");
        }

        if (!IsPort(profile.LocalPort))
        {
            errors.Add("本地端口必须在 1-65535 范围内。");
        }

        if (!IsPort(profile.RemotePort))
        {
            errors.Add("远端端口必须在 1-65535 范围内。");
        }

        if (!IsPort(profile.ServerPort))
        {
            errors.Add("服务端端口必须在 1-65535 范围内。");
        }

        return errors.Count == 0 ? TunnelValidationResult.Success : new TunnelValidationResult(false, errors);
    }

    public string PreviewConfiguration(TunnelProfile profile)
    {
        return string.Join(Environment.NewLine,
        [
            "# Mock preview only. This is not production FRP TOML.",
            $"name = \"{profile.Name}\"",
            $"protocol = \"{profile.Protocol.ToString().ToLowerInvariant()}\"",
            $"local = \"{profile.LocalHost}:{profile.LocalPort}\"",
            $"remote_port = {profile.RemotePort}",
            $"server = \"{profile.ServerAddress}:{profile.ServerPort}\""
        ]);
    }

    public TunnelConfigurationParseResult ParseConfiguration(string configuration)
    {
        return TunnelConfigurationParseResult.Failed("MockExcaliburTunnel 不解析真实 TOML 配置。");
    }

    private static bool IsPort(int port) => port is >= 1 and <= 65535;
}
