using Arturia.FrpNexus.Core.ExcaliburTunnel;

namespace Arturia.FrpNexus.Infrastructure.ExcaliburTunnel;

public sealed class FrpExcaliburTunnel : IExcaliburTunnel
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

        if (profile.Protocol is TunnelProtocol.Http or TunnelProtocol.Https)
        {
            errors.Add("Phase 6 只支持 TCP/UDP 隧道；HTTP/HTTPS 将在后续 Profile 字段扩展后实现。");
        }

        if (string.IsNullOrWhiteSpace(profile.LocalHost))
        {
            errors.Add("本地地址不能为空。");
        }

        if (string.IsNullOrWhiteSpace(profile.ServerAddress))
        {
            errors.Add("FRP 服务端地址不能为空。");
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
            errors.Add("FRP 服务端端口必须在 1-65535 范围内。");
        }

        return errors.Count == 0 ? TunnelValidationResult.Success : new TunnelValidationResult(false, errors);
    }

    public string PreviewConfiguration(TunnelProfile profile)
    {
        var validation = Validate(profile);
        if (!validation.IsValid)
        {
            return string.Join(Environment.NewLine, validation.Errors.Select(error => $"# ERROR: {error}"));
        }

        return FrpTomlSerializer.Serialize(profile);
    }

    public TunnelConfigurationParseResult ParseConfiguration(string configuration)
    {
        return FrpTomlParser.Parse(configuration);
    }

    private static bool IsPort(int port) => port is >= 1 and <= 65535;
}
