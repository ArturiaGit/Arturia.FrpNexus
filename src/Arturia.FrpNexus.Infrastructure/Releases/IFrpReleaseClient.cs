using Arturia.FrpNexus.Application.Abstractions;

namespace Arturia.FrpNexus.Infrastructure.Releases;

public interface IFrpReleaseClient
{
    Task<IReadOnlyList<FrpReleaseVersion>> ListVersionsAsync(CancellationToken cancellationToken = default);

    Task<Stream> DownloadAssetAsync(
        string version,
        string targetRuntime,
        CancellationToken cancellationToken = default);
}
