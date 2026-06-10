using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Application.Abstractions;

public interface INodeConnectionSessionService
{
    Task<NodeConnectionSessionResult> ConnectAsync(
        NodeProfile node,
        SshCredentialReference credential,
        CancellationToken cancellationToken = default);

    Task<NodeConnectionSessionResult> DisconnectAsync(
        string nodeName,
        CancellationToken cancellationToken = default);

    NodeConnectionSessionSnapshot GetSessionStatus(string nodeName);

    IReadOnlyList<NodeConnectionSessionSnapshot> ListActiveSessions();

    SshCredentialReference? GetConnectedCredential(string nodeName);
}

public sealed record NodeConnectionSessionResult(
    string NodeName,
    NodeConnectionSessionState State,
    DateTimeOffset? ConnectedAt,
    string Message);

public sealed record NodeConnectionSessionSnapshot(
    string NodeName,
    NodeConnectionSessionState State,
    DateTimeOffset? ConnectedAt,
    string Message);

public enum NodeConnectionSessionState
{
    Offline,
    Connecting,
    Online,
    Error,
    Disconnected
}
