using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Application.Abstractions;

public interface IRemoteFileTransferService
{
    Task<RemoteFileTransferResult> UploadFrpBinaryAsync(
        RemoteFileUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<RemoteFileTransferResult> UploadConfigurationAsync(
        RemoteConfigurationUploadRequest request,
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

public sealed record RemoteFileTransferResult(
    string NodeName,
    string RemotePath,
    FrpNexusStatus Status,
    DateTimeOffset CompletedAt,
    string Message);
