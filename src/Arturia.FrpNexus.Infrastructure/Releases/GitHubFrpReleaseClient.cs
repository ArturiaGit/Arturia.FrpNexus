using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Arturia.FrpNexus.Application.Abstractions;

namespace Arturia.FrpNexus.Infrastructure.Releases;

public sealed class GitHubFrpReleaseClient(HttpClient httpClient) : IFrpReleaseClient
{
    private const string DefaultReleasesUrl = "https://api.github.com/repos/fatedier/frp/releases";

    public async Task<IReadOnlyList<FrpReleaseVersion>> ListVersionsAsync(
        FrpReleaseSourceOptions? sourceOptions = null,
        CancellationToken cancellationToken = default)
    {
        EnsureHeaders();

        var releasesUrl = ResolveReleasesUrl(sourceOptions);
        var releases = await httpClient.GetFromJsonAsync<IReadOnlyList<GitHubRelease>>(releasesUrl, cancellationToken)
            ?? [];

        return releases
            .Where(release => !string.IsNullOrWhiteSpace(release.TagName))
            .Select(release => new FrpReleaseVersion(release.TagName, release.PublishedAt))
            .ToArray();
    }

    public async Task<Stream> DownloadAssetAsync(
        string version,
        string targetRuntime,
        FrpReleaseSourceOptions? sourceOptions = null,
        CancellationToken cancellationToken = default)
    {
        EnsureHeaders();

        var releasesUrl = ResolveReleasesUrl(sourceOptions);
        var releases = await httpClient.GetFromJsonAsync<IReadOnlyList<GitHubRelease>>(releasesUrl, cancellationToken)
            ?? [];
        var release = releases.FirstOrDefault(item => string.Equals(item.TagName, version, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"未找到 FRP Release：{version}");

        var asset = release.Assets.FirstOrDefault(item => IsRuntimeAsset(item.Name, targetRuntime))
            ?? throw new InvalidOperationException($"未找到适配 {targetRuntime} 的 FRP Release 资产。");

        return await httpClient.GetStreamAsync(asset.BrowserDownloadUrl, cancellationToken);
    }

    private static string ResolveReleasesUrl(FrpReleaseSourceOptions? sourceOptions)
    {
        if (sourceOptions is null
            || !string.Equals(sourceOptions.SourceKind, "Custom", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultReleasesUrl;
        }

        if (Uri.TryCreate(sourceOptions.CustomReleasesApiUrl, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https")
        {
            return uri.ToString();
        }

        throw new InvalidOperationException("自定义 FRP 镜像源地址无效。");
    }

    private static bool IsRuntimeAsset(string assetName, string targetRuntime)
    {
        return assetName.Contains(targetRuntime, StringComparison.OrdinalIgnoreCase)
            && (assetName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
                || assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
    }

    private void EnsureHeaders()
    {
        if (!httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FrpNexus/1.0");
        }
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("published_at")] DateTimeOffset PublishedAt,
        [property: JsonPropertyName("assets")] IReadOnlyList<GitHubAsset> Assets);

    private sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}
