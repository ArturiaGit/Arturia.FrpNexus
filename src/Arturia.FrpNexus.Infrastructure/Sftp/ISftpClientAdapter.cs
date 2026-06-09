using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Infrastructure.Sftp;

public interface ISftpClientAdapter
{
    Task<IReadOnlyList<RemoteDirectoryEntry>> ListDirectoriesAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string remotePath,
        CancellationToken cancellationToken = default);

    Task CreateDirectoryAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string remotePath,
        CancellationToken cancellationToken = default);

    Task EnsureDirectoryAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string remotePath,
        CancellationToken cancellationToken = default);

    Task UploadFileAsync(
        NodeProfile node,
        SshCredentialReference credential,
        Stream content,
        string remotePath,
        CancellationToken cancellationToken = default);

    Task<bool> FileExistsAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string remotePath,
        CancellationToken cancellationToken = default);

    Task DeleteFileAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string remotePath,
        CancellationToken cancellationToken = default);

    Task RenameFileAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default);
}
