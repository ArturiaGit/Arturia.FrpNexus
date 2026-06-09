using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Application.Abstractions;

public interface INodeManagementService
{
    Task<IReadOnlyList<NodeProfile>> ListNodesAsync(CancellationToken cancellationToken = default);

    Task<NodeProfile?> GetNodeAsync(string nodeName, CancellationToken cancellationToken = default);

    Task SaveNodeAsync(NodeProfile node, CancellationToken cancellationToken = default);

    Task DeleteNodeAsync(string nodeName, CancellationToken cancellationToken = default);

    Task UpdateLastConnectionAsync(
        string nodeName,
        DateTimeOffset connectedAt,
        CancellationToken cancellationToken = default);

    Task UpdateConnectionTestResultAsync(
        string nodeName,
        FrpNexusStatus status,
        DateTimeOffset testedAt,
        CancellationToken cancellationToken = default);
}
