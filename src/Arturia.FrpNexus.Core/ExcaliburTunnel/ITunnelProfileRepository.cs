namespace Arturia.FrpNexus.Core.ExcaliburTunnel;

public interface ITunnelProfileRepository
{
    Task<IReadOnlyList<TunnelProfile>> ListAsync(CancellationToken cancellationToken = default);

    Task<TunnelProfile?> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    Task SaveAsync(TunnelProfile profile, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
