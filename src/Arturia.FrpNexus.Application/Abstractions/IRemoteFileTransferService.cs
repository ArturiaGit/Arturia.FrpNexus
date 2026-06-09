using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Application.Abstractions;

public interface IRemoteFileTransferService
{
    Task<RemoteFilePresenceResult> CheckRemoteFilesAsync(
        RemoteFilePresenceRequest request,
        CancellationToken cancellationToken = default);

    Task<RemoteFileTransferResult> UploadFrpBinaryAsync(
        RemoteFileUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<RemoteFileTransferResult> UploadConfigurationAsync(
        RemoteConfigurationUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<RemoteFileDeleteResult> DeleteRemoteFilesAsync(
        RemoteFileDeleteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record RemoteFileUploadRequest(
    NodeProfile Node,
    SshCredentialReference Credential,
    string LocalPath,
    string RemotePath);

public sealed record RemoteConfigurationUploadRequest(
    NodeProfile Node,
    SshCredentialReference Credential,
    string TomlContent,
    string RemotePath);

public sealed record RemoteFileDeleteRequest(
    NodeProfile Node,
    SshCredentialReference Credential,
    IReadOnlyList<string> RemotePaths);

public sealed record RemoteFilePresenceRequest(
    NodeProfile Node,
    SshCredentialReference Credential,
    IReadOnlyList<string> RemotePaths);

public sealed record RemoteFilePresenceEntry(
    string RemotePath,
    bool Exists);

public sealed record RemoteFileTransferResult(
    string NodeName,
    string RemotePath,
    FrpNexusStatus Status,
    DateTimeOffset CompletedAt,
    string Message);

public sealed record RemoteFileDeleteResult(
    string NodeName,
    IReadOnlyList<string> DeletedPaths,
    IReadOnlyList<string> MissingPaths,
    FrpNexusStatus Status,
    DateTimeOffset CompletedAt,
    string Message);

public sealed record RemoteFilePresenceResult(
    string NodeName,
    IReadOnlyList<RemoteFilePresenceEntry> Files,
    FrpNexusStatus Status,
    DateTimeOffset CompletedAt,
    string Message);
