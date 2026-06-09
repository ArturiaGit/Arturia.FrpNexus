using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Application.Abstractions;

public interface ILocalFrpcProcessService
{
    Task<LocalFrpcProcessResult> ApplyNodeTunnelsAsync(
        LocalFrpcProcessRequest request,
        CancellationToken cancellationToken = default);

    Task<LocalFrpcProcessResult> StopNodeAsync(
        string nodeName,
        CancellationToken cancellationToken = default);

    LocalFrpcProcessSnapshot GetNodeStatus(string nodeName);
}

public sealed record LocalFrpcProcessRequest(
    NodeProfile Node,
    IReadOnlyList<TunnelProfile> EnabledTunnels);

public sealed record LocalFrpcProcessResult(
    string NodeName,
    FrpNexusStatus Status,
    DateTimeOffset CompletedAt,
    string Message);

public sealed record LocalFrpcProcessSnapshot(
    string NodeName,
    FrpNexusStatus Status,
    string Message,
    int? ManagementPort = null);
