using Arturia.FrpNexus.Core.ExcaliburTunnel;

namespace Arturia.FrpNexus.Application.ExcaliburTunnel;

public sealed class TunnelProfileService(ITunnelProfileRepository repository)
{
    public Task<IReadOnlyList<TunnelProfile>> ListAsync(CancellationToken cancellationToken = default)
    {
        return repository.ListAsync(cancellationToken);
    }

    public Task<TunnelProfile?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return repository.FindByIdAsync(id, cancellationToken);
    }

    public Task SaveAsync(TunnelProfile profile, CancellationToken cancellationToken = default)
    {
        return repository.SaveAsync(profile, cancellationToken);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        return repository.DeleteAsync(id, cancellationToken);
    }
}
