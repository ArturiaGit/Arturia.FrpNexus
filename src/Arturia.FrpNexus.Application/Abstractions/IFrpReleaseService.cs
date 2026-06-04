namespace Arturia.FrpNexus.Application.Abstractions;

public interface IFrpReleaseService
{
    Task<IReadOnlyList<string>> ListAvailableVersionsAsync(CancellationToken cancellationToken = default);

    Task PrepareReleaseAsync(string version, string targetRuntime, CancellationToken cancellationToken = default);
}
