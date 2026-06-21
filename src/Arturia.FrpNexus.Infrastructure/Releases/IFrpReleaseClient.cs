using Arturia.FrpNexus.Application.Abstractions;

namespace Arturia.FrpNexus.Infrastructure.Releases;

public interface IFrpReleaseClient
{
    Task<IReadOnlyList<FrpReleaseVersion>> ListVersionsAsync(
        FrpReleaseSourceOptions? sourceOptions = null,
        CancellationToken cancellationToken = default);

    Task<Stream> DownloadAssetAsync(
        string version,
        string targetRuntime,
        FrpReleaseSourceOptions? sourceOptions = null,
        CancellationToken cancellationToken = default);
}
