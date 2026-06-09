using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Application.Abstractions;

public interface IRemoteDirectoryService
{
    Task<RemoteDirectoryListResult> ListDirectoriesAsync(
        RemoteDirectoryListRequest request,
        CancellationToken cancellationToken = default);

    Task<RemoteDirectoryOperationResult> CreateDirectoryAsync(
        RemoteDirectoryCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<RemoteDirectoryOperationResult> EnsureDirectoryAsync(
        RemoteDirectoryEnsureRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record RemoteDirectoryListRequest(
    NodeProfile Node,
    SshCredentialReference Credential,
    string RemotePath);

public sealed record RemoteDirectoryCreateRequest(
    NodeProfile Node,
    SshCredentialReference Credential,
    string RemotePath);

public sealed record RemoteDirectoryEnsureRequest(
    NodeProfile Node,
    SshCredentialReference Credential,
    string RemotePath);

public sealed record RemoteDirectoryEntry(
    string Name,
    string FullPath);

public sealed record RemoteDirectoryListResult(
    string RemotePath,
    FrpNexusStatus Status,
    string Message,
    IReadOnlyList<RemoteDirectoryEntry> Directories);

public sealed record RemoteDirectoryOperationResult(
    string RemotePath,
    FrpNexusStatus Status,
    string Message);
