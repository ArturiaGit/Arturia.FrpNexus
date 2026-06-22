namespace Arturia.FrpNexus.Application.Abstractions;

public interface ILocalCacheMaintenanceService
{
    Task<LocalCacheCleanupResult> ClearDefaultFrpReleaseCacheAsync(
        CancellationToken cancellationToken = default);
}

public sealed record LocalCacheCleanupResult(
    int DeletedFileCount,
    long DeletedByteCount,
    string CacheDirectory);
