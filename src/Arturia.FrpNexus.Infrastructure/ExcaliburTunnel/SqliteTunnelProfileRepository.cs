using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.ExcaliburTunnel;
using ApplicationTunnelProfile = Arturia.FrpNexus.Core.Models.TunnelProfile;
using ApplicationTunnelProtocol = Arturia.FrpNexus.Core.Models.TunnelProtocol;
using FrpNexusStatus = Arturia.FrpNexus.Core.Models.FrpNexusStatus;

namespace Arturia.FrpNexus.Infrastructure.ExcaliburTunnel;

public sealed class SqliteTunnelProfileRepository(ITunnelManagementService tunnelManagementService) : ITunnelProfileRepository
{
    private const int DefaultServerPort = 7000;
    private const string CliRemarkPrefix = "cli:";
    private const string ServerPortKey = "serverPort=";

    public async Task<IReadOnlyList<TunnelProfile>> ListAsync(CancellationToken cancellationToken = default)
    {
        var tunnels = await tunnelManagementService.ListTunnelsAsync(cancellationToken);
        return tunnels.Select(ToCliProfile).ToArray();
    }

    public async Task<TunnelProfile?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var tunnel = await tunnelManagementService.GetTunnelAsync(id, cancellationToken);
        return tunnel is null ? null : ToCliProfile(tunnel);
    }

    public Task SaveAsync(TunnelProfile profile, CancellationToken cancellationToken = default)
    {
        return tunnelManagementService.SaveTunnelAsync(ToApplicationProfile(profile), cancellationToken);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var existing = await tunnelManagementService.GetTunnelAsync(id, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        await tunnelManagementService.DeleteTunnelAsync(id, cancellationToken);
        return true;
    }

    private static ApplicationTunnelProfile ToApplicationProfile(TunnelProfile profile)
    {
        return new ApplicationTunnelProfile(
            profile.Id,
            ToApplicationProtocol(profile.Protocol),
            profile.ServerAddress,
            profile.LocalHost,
            profile.LocalPort,
            profile.RemotePort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            profile.Enabled ? FrpNexusStatus.Pending : FrpNexusStatus.Stopped,
            CreateRemark(profile.ServerPort));
    }

    private static TunnelProfile ToCliProfile(ApplicationTunnelProfile profile)
    {
        return new TunnelProfile(
            profile.Name,
            profile.Name,
            ToCliProtocol(profile.Protocol),
            profile.LocalAddress,
            profile.LocalPort,
            ParseRemotePort(profile.RemoteEndpoint),
            profile.NodeName,
            ParseServerPort(profile.Remark),
            profile.Status != FrpNexusStatus.Stopped);
    }

    private static ApplicationTunnelProtocol ToApplicationProtocol(TunnelProtocol protocol)
    {
        return protocol switch
        {
            TunnelProtocol.Udp => ApplicationTunnelProtocol.Udp,
            TunnelProtocol.Http => ApplicationTunnelProtocol.Http,
            TunnelProtocol.Https => ApplicationTunnelProtocol.Https,
            _ => ApplicationTunnelProtocol.Tcp
        };
    }

    private static TunnelProtocol ToCliProtocol(ApplicationTunnelProtocol protocol)
    {
        return protocol switch
        {
            ApplicationTunnelProtocol.Udp => TunnelProtocol.Udp,
            ApplicationTunnelProtocol.Http => TunnelProtocol.Http,
            ApplicationTunnelProtocol.Https => TunnelProtocol.Https,
            _ => TunnelProtocol.Tcp
        };
    }

    private static int ParseRemotePort(string remoteEndpoint)
    {
        return int.TryParse(remoteEndpoint, out var port) ? port : 0;
    }

    private static string CreateRemark(int serverPort)
    {
        return serverPort == DefaultServerPort
            ? string.Empty
            : $"{CliRemarkPrefix}{ServerPortKey}{serverPort}";
    }

    private static int ParseServerPort(string remark)
    {
        if (!remark.StartsWith(CliRemarkPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return DefaultServerPort;
        }

        var value = remark[CliRemarkPrefix.Length..];
        return value.StartsWith(ServerPortKey, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(value[ServerPortKey.Length..], out var serverPort)
            ? serverPort
            : DefaultServerPort;
    }
}
