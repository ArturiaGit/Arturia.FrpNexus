using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Arturia.FrpNexus.Application.Abstractions;

namespace Arturia.FrpNexus.Infrastructure.Credentials;

[SupportedOSPlatform("windows")]
public sealed class DpapiNodeCredentialSecretService : INodeCredentialSecretService
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Arturia.FrpNexus.NodeCredentialSecret.v1");
    private readonly string _secretDirectory;

    public DpapiNodeCredentialSecretService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Arturia",
            "FrpNexus",
            "credentials",
            "ssh-session-passwords"))
    {
    }

    public DpapiNodeCredentialSecretService(string secretDirectory)
    {
        _secretDirectory = string.IsNullOrWhiteSpace(secretDirectory)
            ? throw new ArgumentException("Credential secret directory cannot be empty.", nameof(secretDirectory))
            : secretDirectory;
    }

    public Task<bool> HasSessionPasswordAsync(
        string nodeName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(GetSecretPath(nodeName)));
    }

    public async Task<string?> GetSessionPasswordAsync(
        string nodeName,
        CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetSecretPath(nodeName);
        if (!File.Exists(path))
        {
            return null;
        }

        var protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var unprotectedBytes = ProtectedData.Unprotect(
            protectedBytes,
            Entropy,
            DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(unprotectedBytes);
    }

    public async Task SaveSessionPasswordAsync(
        string nodeName,
        string password,
        CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("会话密码不能为空。");
        }

        Directory.CreateDirectory(_secretDirectory);
        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(password),
            Entropy,
            DataProtectionScope.CurrentUser);

        await File.WriteAllBytesAsync(GetSecretPath(nodeName), protectedBytes, cancellationToken);
    }

    public Task DeleteSessionPasswordAsync(
        string nodeName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetSecretPath(nodeName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetSecretPath(string nodeName)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            throw new ArgumentException("Node name cannot be empty.", nameof(nodeName));
        }

        var normalized = nodeName.Trim().ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        return Path.Combine(_secretDirectory, $"{hash}.bin");
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows DPAPI credential storage requires Windows CurrentUser protection.");
        }
    }
}
