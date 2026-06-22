using System.IO.Compression;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Infrastructure.Releases;
using Serilog.Core;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class FrpReleaseServiceTests
{
    [Fact]
    public async Task ListAvailableVersionsAsync_ShouldReturnVersionsFromClient()
    {
        var releaseClient = new FakeReleaseClient();
        var service = new FrpReleaseService(
            releaseClient,
            new TestReleaseCachePathProvider(),
            Logger.None);

        var sourceOptions = new FrpReleaseSourceOptions(
            "Custom",
            "https://mirror.example.com/repos/fatedier/frp/releases");
        var versions = await service.ListAvailableVersionsAsync(sourceOptions);

        Assert.Contains(versions, version => version.Version == "v0.61.1");
        Assert.Equal(sourceOptions, releaseClient.LastListSourceOptions);
    }

    [Fact]
    public async Task PrepareReleaseAsync_ShouldDownloadExtractAndSelectBinary()
    {
        var cachePathProvider = new TestReleaseCachePathProvider();
        var releaseClient = new FakeReleaseClient(CreateZipArchive("frp_0.61.1_linux_amd64/frpc", "binary"));
        var service = new FrpReleaseService(releaseClient, cachePathProvider, Logger.None);

        var result = await service.PrepareReleaseAsync(new FrpReleasePreparationRequest(
            "v0.61.1",
            "linux_amd64",
            "frpc"));

        Assert.Equal("FRP 二进制准备完成。", result.Message);
        Assert.EndsWith("frpc", result.LocalPath);
        Assert.True(File.Exists(result.LocalPath));
        Assert.Equal(1, releaseClient.DownloadCount);
        Assert.StartsWith(cachePathProvider.GetReleaseCacheDirectory(), result.LocalPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareReleaseAsync_ShouldUseLocalCacheWhenBinaryExists()
    {
        var cachePathProvider = new TestReleaseCachePathProvider();
        var targetDirectory = Path.Combine(cachePathProvider.GetReleaseCacheDirectory(), "v0.61.1", "linux_amd64", "frp");
        Directory.CreateDirectory(targetDirectory);
        var binaryPath = Path.Combine(targetDirectory, "frps");
        await File.WriteAllTextAsync(binaryPath, "cached-binary");
        var releaseClient = new FakeReleaseClient(CreateZipArchive("unused/frps", "binary"));
        var service = new FrpReleaseService(releaseClient, cachePathProvider, Logger.None);

        var result = await service.PrepareReleaseAsync(new FrpReleasePreparationRequest(
            "v0.61.1",
            "linux_amd64",
            "frps"));

        Assert.Equal("FRP 二进制已在本地缓存中准备好。", result.Message);
        Assert.Equal(binaryPath, result.LocalPath);
        Assert.Equal(0, releaseClient.DownloadCount);
    }

    [Fact]
    public async Task PrepareReleaseAsync_ShouldUseRequestedDownloadDirectoryWhenProvided()
    {
        var cachePathProvider = new TestReleaseCachePathProvider();
        var downloadDirectory = Path.Combine(
            Path.GetTempPath(),
            "FrpNexusTests",
            Guid.NewGuid().ToString("N"),
            "selected-downloads");
        var releaseClient = new FakeReleaseClient(CreateZipArchive("frp_0.61.1_windows_amd64/frpc.exe", "binary"));
        var service = new FrpReleaseService(releaseClient, cachePathProvider, Logger.None);

        var result = await service.PrepareReleaseAsync(new FrpReleasePreparationRequest(
            "v0.61.1",
            "windows_amd64",
            "frpc",
            downloadDirectory));

        Assert.StartsWith(downloadDirectory, result.LocalPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine("v0.61.1", "windows_amd64"), result.LocalPath);
        Assert.False(
            result.LocalPath.StartsWith(cachePathProvider.GetReleaseCacheDirectory(), StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(result.LocalPath));
    }

    [Fact]
    public async Task PrepareReleaseAsync_ShouldPassSourceOptionsToReleaseClient()
    {
        var cachePathProvider = new TestReleaseCachePathProvider();
        var releaseClient = new FakeReleaseClient(CreateZipArchive("frp_0.61.1_windows_amd64/frpc.exe", "binary"));
        var service = new FrpReleaseService(releaseClient, cachePathProvider, Logger.None);
        var sourceOptions = new FrpReleaseSourceOptions(
            "Custom",
            "https://mirror.example.com/repos/fatedier/frp/releases");

        await service.PrepareReleaseAsync(new FrpReleasePreparationRequest(
            "v0.61.1",
            "windows_amd64",
            "frpc",
            DownloadDirectory: null,
            SourceOptions: sourceOptions));

        Assert.Equal(sourceOptions, releaseClient.LastDownloadSourceOptions);
    }

    [Fact]
    public async Task PrepareReleaseAsync_ShouldRejectUnexpectedBinaryName()
    {
        var service = new FrpReleaseService(
            new FakeReleaseClient(),
            new TestReleaseCachePathProvider(),
            Logger.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PrepareReleaseAsync(new FrpReleasePreparationRequest(
                "v0.61.1",
                "linux_amd64",
                "frpc;rm -rf")));

        Assert.Equal("FRP 二进制只能选择 frpc 或 frps。", exception.Message);
    }

    private static MemoryStream CreateZipArchive(string entryName, string content)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(entryName);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }

        stream.Position = 0;
        return stream;
    }

    private sealed class FakeReleaseClient(Stream? archiveStream = null) : IFrpReleaseClient
    {
        public int DownloadCount { get; private set; }

        public FrpReleaseSourceOptions? LastListSourceOptions { get; private set; }

        public FrpReleaseSourceOptions? LastDownloadSourceOptions { get; private set; }

        public Task<IReadOnlyList<FrpReleaseVersion>> ListVersionsAsync(
            FrpReleaseSourceOptions? sourceOptions = null,
            CancellationToken cancellationToken = default)
        {
            LastListSourceOptions = sourceOptions;
            IReadOnlyList<FrpReleaseVersion> versions =
            [
                new("v0.61.1", DateTimeOffset.Parse("2026-06-01T00:00:00+00:00"))
            ];

            return Task.FromResult(versions);
        }

        public Task<Stream> DownloadAssetAsync(
            string version,
            string targetRuntime,
            FrpReleaseSourceOptions? sourceOptions = null,
            CancellationToken cancellationToken = default)
        {
            DownloadCount++;
            LastDownloadSourceOptions = sourceOptions;
            return Task.FromResult(archiveStream ?? CreateZipArchive("frp/frpc", "binary") as Stream);
        }
    }

    private sealed class TestReleaseCachePathProvider : IFrpReleaseCachePathProvider
    {
        private readonly string _directory = Path.Combine(
            Path.GetTempPath(),
            "FrpNexusTests",
            Guid.NewGuid().ToString("N"),
            "releases");

        public string GetReleaseCacheDirectory()
        {
            return _directory;
        }
    }
}
