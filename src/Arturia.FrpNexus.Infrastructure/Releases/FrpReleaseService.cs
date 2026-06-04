using System.Formats.Tar;
using System.IO.Compression;
using Arturia.FrpNexus.Application.Abstractions;
using Serilog;

namespace Arturia.FrpNexus.Infrastructure.Releases;

public sealed class FrpReleaseService(
    IFrpReleaseClient releaseClient,
    IFrpReleaseCachePathProvider cachePathProvider,
    ILogger logger) : IFrpReleaseService
{
    public Task<IReadOnlyList<FrpReleaseVersion>> ListAvailableVersionsAsync(CancellationToken cancellationToken = default)
    {
        return releaseClient.ListVersionsAsync(cancellationToken);
    }

    public async Task<FrpReleasePreparationResult> PrepareReleaseAsync(
        FrpReleasePreparationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var preparedAt = DateTimeOffset.UtcNow;
        var targetDirectory = GetTargetDirectory(request);
        Directory.CreateDirectory(targetDirectory);

        var existingBinary = FindBinary(targetDirectory, request.BinaryName);
        if (existingBinary is not null)
        {
            return new FrpReleasePreparationResult(
                request.Version,
                request.TargetRuntime,
                request.BinaryName,
                existingBinary,
                preparedAt,
                "FRP 二进制已在本地缓存中准备好。");
        }

        await using var archiveStream = await releaseClient.DownloadAssetAsync(
            request.Version,
            request.TargetRuntime,
            cancellationToken);

        var archivePath = Path.Combine(targetDirectory, $"frp-{request.Version}-{request.TargetRuntime}.archive");
        await using (var fileStream = File.Create(archivePath))
        {
            await archiveStream.CopyToAsync(fileStream, cancellationToken);
        }

        ExtractArchive(archivePath, targetDirectory);
        File.Delete(archivePath);

        var binaryPath = FindBinary(targetDirectory, request.BinaryName)
            ?? throw new InvalidOperationException($"Release 中没有找到 {request.BinaryName}。");

        logger.Information(
            "FRP release prepared: {Version} {TargetRuntime} {BinaryName} at {LocalPath}",
            request.Version,
            request.TargetRuntime,
            request.BinaryName,
            binaryPath);

        return new FrpReleasePreparationResult(
            request.Version,
            request.TargetRuntime,
            request.BinaryName,
            binaryPath,
            preparedAt,
            "FRP 二进制准备完成。");
    }

    private string GetTargetDirectory(FrpReleasePreparationRequest request)
    {
        return Path.Combine(
            cachePathProvider.GetReleaseCacheDirectory(),
            SanitizePathSegment(request.Version),
            SanitizePathSegment(request.TargetRuntime));
    }

    private static void ValidateRequest(FrpReleasePreparationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Version))
        {
            throw new InvalidOperationException("请选择 FRP 版本。");
        }

        if (string.IsNullOrWhiteSpace(request.TargetRuntime))
        {
            throw new InvalidOperationException("请选择目标运行时。");
        }

        if (request.BinaryName is not ("frpc" or "frps"))
        {
            throw new InvalidOperationException("FRP 二进制只能选择 frpc 或 frps。");
        }
    }

    private static void ExtractArchive(string archivePath, string targetDirectory)
    {
        if (LooksLikeZip(archivePath))
        {
            ZipFile.ExtractToDirectory(archivePath, targetDirectory, overwriteFiles: true);
            return;
        }

        using var archive = File.OpenRead(archivePath);
        using var gzip = new GZipStream(archive, CompressionMode.Decompress);
        TarFile.ExtractToDirectory(gzip, targetDirectory, overwriteFiles: true);
    }

    private static bool LooksLikeZip(string archivePath)
    {
        using var stream = File.OpenRead(archivePath);
        Span<byte> signature = stackalloc byte[4];
        var bytesRead = stream.Read(signature);

        return bytesRead >= 4
            && signature[0] == 0x50
            && signature[1] == 0x4B
            && signature[2] == 0x03
            && signature[3] == 0x04;
    }

    private static string? FindBinary(string directory, string binaryName)
    {
        var candidates = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => IsBinary(path, binaryName))
            .OrderBy(path => path.Length)
            .ToArray();

        return candidates.FirstOrDefault();
    }

    private static bool IsBinary(string path, string binaryName)
    {
        var fileName = Path.GetFileName(path);
        return string.Equals(fileName, binaryName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, $"{binaryName}.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(character => invalid.Contains(character) ? '_' : character).ToArray();
        return new string(chars);
    }
}
