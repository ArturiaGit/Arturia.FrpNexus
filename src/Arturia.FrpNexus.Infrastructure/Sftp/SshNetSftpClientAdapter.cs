using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Ssh;
using Renci.SshNet;

namespace Arturia.FrpNexus.Infrastructure.Sftp;

public sealed class SshNetSftpClientAdapter : ISftpClientAdapter
{
    public Task<IReadOnlyList<RemoteDirectoryEntry>> ListDirectoriesAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<RemoteDirectoryEntry>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var client = new SftpClient(SshConnectionInfoFactory.Create(node, credential, "SFTP"));
            client.Connect();

            cancellationToken.ThrowIfCancellationRequested();
            var directories = client
                .ListDirectory(remotePath)
                .Where(item => item.IsDirectory && item.Name is not "." and not "..")
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(item => new RemoteDirectoryEntry(item.Name, CombineRemotePath(remotePath, item.Name)))
                .ToArray();

            client.Disconnect();
            return directories;
        }, cancellationToken);
    }

    public Task CreateDirectoryAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var client = new SftpClient(SshConnectionInfoFactory.Create(node, credential, "SFTP"));
            client.Connect();

            cancellationToken.ThrowIfCancellationRequested();
            if (!client.Exists(remotePath))
            {
                client.CreateDirectory(remotePath);
            }

            client.Disconnect();
        }, cancellationToken);
    }

    public Task EnsureDirectoryAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var client = new SftpClient(SshConnectionInfoFactory.Create(node, credential, "SFTP"));
            client.Connect();

            foreach (var segmentPath in EnumerateDirectorySegments(remotePath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!client.Exists(segmentPath))
                {
                    client.CreateDirectory(segmentPath);
                }
            }

            client.Disconnect();
        }, cancellationToken);
    }

    public Task UploadFileAsync(
        NodeProfile node,
        SshCredentialReference credential,
        Stream content,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var client = new SftpClient(SshConnectionInfoFactory.Create(node, credential, "SFTP"));
            client.Connect();

            cancellationToken.ThrowIfCancellationRequested();
            client.UploadFile(content, remotePath, canOverride: true);
            client.Disconnect();
        }, cancellationToken);
    }

    public Task<bool> FileExistsAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var client = new SftpClient(SshConnectionInfoFactory.Create(node, credential, "SFTP"));
            client.Connect();

            cancellationToken.ThrowIfCancellationRequested();
            var exists = client.Exists(remotePath);
            client.Disconnect();
            return exists;
        }, cancellationToken);
    }

    public Task DeleteFileAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var client = new SftpClient(SshConnectionInfoFactory.Create(node, credential, "SFTP"));
            client.Connect();

            cancellationToken.ThrowIfCancellationRequested();
            client.DeleteFile(remotePath);
            client.Disconnect();
        }, cancellationToken);
    }

    public Task RenameFileAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var client = new SftpClient(SshConnectionInfoFactory.Create(node, credential, "SFTP"));
            client.Connect();

            cancellationToken.ThrowIfCancellationRequested();
            client.RenameFile(sourcePath, destinationPath);
            client.Disconnect();
        }, cancellationToken);
    }

    private static IEnumerable<string> EnumerateDirectorySegments(string remotePath)
    {
        var trimmed = remotePath.Trim('/');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            yield break;
        }

        var current = string.Empty;
        foreach (var segment in trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = $"{current}/{segment}";
            yield return current;
        }
    }

    private static string CombineRemotePath(string parent, string child)
    {
        return string.Equals(parent, "/", StringComparison.Ordinal)
            ? $"/{child}"
            : $"{parent.TrimEnd('/')}/{child}";
    }
}
