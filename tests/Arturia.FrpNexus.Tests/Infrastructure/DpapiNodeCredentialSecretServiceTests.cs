using Arturia.FrpNexus.Infrastructure.Credentials;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class DpapiNodeCredentialSecretServiceTests
{
    [Fact]
    public async Task SaveSessionPasswordAsync_ShouldProtectReadAndDeletePassword()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var directory = CreateTempDirectory();
        var service = new DpapiNodeCredentialSecretService(directory);

        await service.SaveSessionPasswordAsync("凭据节点", "SESSION_PASSWORD_PLACEHOLDER");

        Assert.True(await service.HasSessionPasswordAsync("凭据节点"));
        Assert.Equal("SESSION_PASSWORD_PLACEHOLDER", await service.GetSessionPasswordAsync("凭据节点"));
        Assert.Single(Directory.GetFiles(directory, "*.bin"));

        await service.DeleteSessionPasswordAsync("凭据节点");

        Assert.False(await service.HasSessionPasswordAsync("凭据节点"));
        Assert.Null(await service.GetSessionPasswordAsync("凭据节点"));
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task SaveSessionPasswordAsync_ShouldNotWritePlaintextPassword()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var directory = CreateTempDirectory();
        var service = new DpapiNodeCredentialSecretService(directory);

        await service.SaveSessionPasswordAsync("密文节点", "SESSION_PASSWORD_PLACEHOLDER");

        var file = Assert.Single(Directory.GetFiles(directory, "*.bin"));
        var storedText = Convert.ToBase64String(await File.ReadAllBytesAsync(file));
        Assert.DoesNotContain("SESSION_PASSWORD_PLACEHOLDER", storedText, StringComparison.Ordinal);
        Directory.Delete(directory, recursive: true);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "frpnexus-credential-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
