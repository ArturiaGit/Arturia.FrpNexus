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
        return ExecuteWithResultAsync(
            node,
            credential,
            "SFTP 目录读取",
            SshNetOperationPolicy.SftpOperationTimeout,
            client => (IReadOnlyList<RemoteDirectoryEntry>)client
                .ListDirectory(remotePath)
                .Where(item => item.IsDirectory && item.Name is not "." and not "..")
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(item => new RemoteDirectoryEntry(item.Name, CombineRemotePath(remotePath, item.Name)))
                .ToArray(),
            cancellationToken);
    }

    public Task CreateDirectoryAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            node,
            credential,
            "SFTP 目录创建",
            SshNetOperationPolicy.SftpOperationTimeout,
            client =>
            {
                if (!client.Exists(remotePath))
                {
                    client.CreateDirectory(remotePath);
                }
            },
            cancellationToken);
    }

    public Task EnsureDirectoryAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            node,
            credential,
            "SFTP 目录准备",
            SshNetOperationPolicy.SftpOperationTimeout,
            client =>
            {
                foreach (var segmentPath in EnumerateDirectorySegments(remotePath))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!client.Exists(segmentPath))
                    {
                        client.CreateDirectory(segmentPath);
                    }
                }
            },
            cancellationToken);
    }

    public Task UploadFileAsync(
        NodeProfile node,
        SshCredentialReference credential,
        Stream content,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            node,
            credential,
            "SFTP 文件上传",
            SshNetOperationPolicy.SftpUploadTimeout,
            client => client.UploadFile(content, remotePath, canOverride: true),
            cancellationToken);
    }

    public Task<bool> FileExistsAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithResultAsync(
            node,
            credential,
            "SFTP 文件检查",
            SshNetOperationPolicy.SftpOperationTimeout,
            client => client.Exists(remotePath),
            cancellationToken);
    }

    public Task DeleteFileAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            node,
            credential,
            "SFTP 文件删除",
            SshNetOperationPolicy.SftpOperationTimeout,
            client => client.DeleteFile(remotePath),
            cancellationToken);
    }

    public Task RenameFileAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            node,
            credential,
            "SFTP 文件重命名",
            SshNetOperationPolicy.SftpOperationTimeout,
            client => client.RenameFile(sourcePath, destinationPath),
            cancellationToken);
    }

    private static Task ExecuteAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string operationName,
        TimeSpan timeout,
        Action<SftpClient> operation,
        CancellationToken cancellationToken)
    {
        return SshNetOperationPolicy.RunAsync(
            operationName,
            timeout,
            () =>
            {
                using var client = CreateConnectedClient(node, credential, timeout, cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    operation(client);
                }
                finally
                {
                    Disconnect(client);
                }
            },
            cancellationToken);
    }

    private static Task<T> ExecuteWithResultAsync<T>(
        NodeProfile node,
        SshCredentialReference credential,
        string operationName,
        TimeSpan timeout,
        Func<SftpClient, T> operation,
        CancellationToken cancellationToken)
    {
        return SshNetOperationPolicy.RunAsync(
            operationName,
            timeout,
            () =>
            {
                using var client = CreateConnectedClient(node, credential, timeout, cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return operation(client);
                }
                finally
                {
                    Disconnect(client);
                }
            },
            cancellationToken);
    }

    private static SftpClient CreateConnectedClient(
        NodeProfile node,
        SshCredentialReference credential,
        TimeSpan operationTimeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = new SftpClient(SshConnectionInfoFactory.Create(node, credential, "SFTP"));
        client.OperationTimeout = operationTimeout;
        try
        {
            client.Connect();
            cancellationToken.ThrowIfCancellationRequested();
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static void Disconnect(SftpClient client)
    {
        if (client.IsConnected)
        {
            client.Disconnect();
        }
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
