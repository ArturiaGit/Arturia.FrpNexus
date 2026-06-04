using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Application.Abstractions;

public interface ITunnelManagementService
{
    Task<IReadOnlyList<TunnelProfile>> ListTunnelsAsync(CancellationToken cancellationToken = default);

    Task<TunnelProfile?> GetTunnelAsync(string tunnelName, CancellationToken cancellationToken = default);

    Task SaveTunnelAsync(TunnelProfile tunnel, CancellationToken cancellationToken = default);

    Task DeleteTunnelAsync(string tunnelName, CancellationToken cancellationToken = default);
}
