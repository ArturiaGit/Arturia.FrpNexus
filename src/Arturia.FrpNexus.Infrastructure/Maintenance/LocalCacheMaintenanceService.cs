using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Infrastructure.Releases;

namespace Arturia.FrpNexus.Infrastructure.Maintenance;

public sealed class LocalCacheMaintenanceService(
    IFrpReleaseCachePathProvider releaseCachePathProvider) : ILocalCacheMaintenanceService
{
    public Task<LocalCacheCleanupResult> ClearDefaultFrpReleaseCacheAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cacheDirectory = releaseCachePathProvider.GetReleaseCacheDirectory();
        if (!Directory.Exists(cacheDirectory))
        {
            return Task.FromResult(new LocalCacheCleanupResult(0, 0, cacheDirectory));
        }

        var deletedFileCount = 0;
        var deletedByteCount = 0L;

        foreach (var filePath in Directory.EnumerateFiles(cacheDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                continue;
            }

            deletedFileCount++;
            deletedByteCount += fileInfo.Length;
        }

        foreach (var filePath in Directory.EnumerateFiles(cacheDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(filePath);
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(cacheDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Delete(directoryPath, recursive: true);
        }

        return Task.FromResult(new LocalCacheCleanupResult(deletedFileCount, deletedByteCount, cacheDirectory));
    }
}
