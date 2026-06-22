using Arturia.FrpNexus.Infrastructure.Maintenance;
using Arturia.FrpNexus.Infrastructure.Releases;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class LocalCacheMaintenanceServiceTests
{
    [Fact]
    public async Task ClearDefaultFrpReleaseCacheAsync_ShouldReturnZeroWhenCacheDirectoryIsMissing()
    {
        var cacheDirectory = Path.Combine(Path.GetTempPath(), "FrpNexusTests", Guid.NewGuid().ToString("N"), "core", "releases");
        var service = new LocalCacheMaintenanceService(new TestReleaseCachePathProvider(cacheDirectory));

        var result = await service.ClearDefaultFrpReleaseCacheAsync();

        Assert.Equal(0, result.DeletedFileCount);
        Assert.Equal(0, result.DeletedByteCount);
        Assert.Equal(cacheDirectory, result.CacheDirectory);
    }

    [Fact]
    public async Task ClearDefaultFrpReleaseCacheAsync_ShouldDeleteOnlyDefaultReleaseCacheContents()
    {
        var root = Path.Combine(Path.GetTempPath(), "FrpNexusTests", Guid.NewGuid().ToString("N"));
        var cacheDirectory = Path.Combine(root, "core", "releases");
        var logsDirectory = Path.Combine(root, "logs");
        var dataDirectory = Path.Combine(root, "data");
        var configsDirectory = Path.Combine(root, "configs");
        Directory.CreateDirectory(Path.Combine(cacheDirectory, "v0.61.1", "windows_amd64"));
        Directory.CreateDirectory(logsDirectory);
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(configsDirectory);
        await File.WriteAllTextAsync(Path.Combine(cacheDirectory, "v0.61.1", "windows_amd64", "frpc.exe"), "binary");
        await File.WriteAllTextAsync(Path.Combine(cacheDirectory, "v0.61.1", "windows_amd64", "README.txt"), "readme");
        await File.WriteAllTextAsync(Path.Combine(logsDirectory, "frpnexus-20260101.log"), "log");
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "frpnexus.db"), "sqlite");
        await File.WriteAllTextAsync(Path.Combine(configsDirectory, "node.frpc.toml"), "toml");
        var service = new LocalCacheMaintenanceService(new TestReleaseCachePathProvider(cacheDirectory));

        var result = await service.ClearDefaultFrpReleaseCacheAsync();

        Assert.Equal(2, result.DeletedFileCount);
        Assert.True(result.DeletedByteCount > 0);
        Assert.Empty(Directory.EnumerateFileSystemEntries(cacheDirectory));
        Assert.True(File.Exists(Path.Combine(logsDirectory, "frpnexus-20260101.log")));
        Assert.True(File.Exists(Path.Combine(dataDirectory, "frpnexus.db")));
        Assert.True(File.Exists(Path.Combine(configsDirectory, "node.frpc.toml")));
    }

    private sealed class TestReleaseCachePathProvider(string cacheDirectory) : IFrpReleaseCachePathProvider
    {
        public string GetReleaseCacheDirectory()
        {
            return cacheDirectory;
        }
    }
}
