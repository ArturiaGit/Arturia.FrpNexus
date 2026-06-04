using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Application.Abstractions;

public interface IRemoteRuntimeService
{
    Task<IReadOnlyList<RuntimeProcess>> GetProcessesAsync(
        RemoteRuntimeQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<RemoteRuntimeCommandResult> StartAsync(
        RemoteRuntimeCommandRequest request,
        CancellationToken cancellationToken = default);

    Task<RemoteRuntimeCommandResult> StopAsync(
        RemoteRuntimeCommandRequest request,
        CancellationToken cancellationToken = default);

    Task<RemoteRuntimeCommandResult> RestartAsync(
        RemoteRuntimeCommandRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record RemoteRuntimeQueryRequest(
    NodeProfile Node,
    SshCredentialReference Credential);

public sealed record RemoteRuntimeCommandRequest(
    NodeProfile Node,
    SshCredentialReference Credential,
    string ProcessName,
    string ProcessKind,
    string Command);

public sealed record RemoteRuntimeCommandResult(
    string NodeName,
    string ProcessName,
    FrpNexusStatus Status,
    DateTimeOffset CompletedAt,
    string Message);
