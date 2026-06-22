using System.Net;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Infrastructure.Releases;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class GitHubFrpReleaseClientTests
{
    [Fact]
    public async Task ListVersionsAsync_ShouldUseCustomGitHubCompatibleReleasesUrl()
    {
        var handler = new CapturingHandler("""
            [
              {
                "tag_name": "v0.61.1",
                "published_at": "2026-06-01T00:00:00Z",
                "assets": []
              }
            ]
            """);
        var client = new GitHubFrpReleaseClient(new HttpClient(handler));
        var sourceOptions = new FrpReleaseSourceOptions(
            "Custom",
            "https://mirror.example.com/repos/fatedier/frp/releases");

        var versions = await client.ListVersionsAsync(sourceOptions);

        Assert.Single(versions);
        Assert.Equal("v0.61.1", versions[0].Version);
        Assert.Equal("https://mirror.example.com/repos/fatedier/frp/releases", handler.RequestUris[0].ToString());
    }

    [Fact]
    public async Task DownloadAssetAsync_ShouldUseCustomGitHubCompatibleReleasesUrl()
    {
        var handler = new CapturingHandler("""
            [
              {
                "tag_name": "v0.61.1",
                "published_at": "2026-06-01T00:00:00Z",
                "assets": [
                  {
                    "name": "frp_0.61.1_windows_amd64.zip",
                    "browser_download_url": "https://mirror.example.com/download/frp_0.61.1_windows_amd64.zip"
                  }
                ]
              }
            ]
            """);
        handler.AddResponse(
            "https://mirror.example.com/download/frp_0.61.1_windows_amd64.zip",
            "archive-bytes");
        var client = new GitHubFrpReleaseClient(new HttpClient(handler));
        var sourceOptions = new FrpReleaseSourceOptions(
            "Custom",
            "https://mirror.example.com/repos/fatedier/frp/releases");

        await using var stream = await client.DownloadAssetAsync("v0.61.1", "windows_amd64", sourceOptions);
        using var reader = new StreamReader(stream);

        Assert.Equal("archive-bytes", await reader.ReadToEndAsync());
        Assert.Equal("https://mirror.example.com/repos/fatedier/frp/releases", handler.RequestUris[0].ToString());
        Assert.Equal("https://mirror.example.com/download/frp_0.61.1_windows_amd64.zip", handler.RequestUris[1].ToString());
    }

    private sealed class CapturingHandler(string defaultResponse) : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _responses = new(StringComparer.OrdinalIgnoreCase);

        public List<Uri> RequestUris { get; } = [];

        public void AddResponse(string uri, string response)
        {
            _responses[uri] = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!);
            var content = _responses.TryGetValue(request.RequestUri!.ToString(), out var response)
                ? response
                : defaultResponse;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            });
        }
    }
}
