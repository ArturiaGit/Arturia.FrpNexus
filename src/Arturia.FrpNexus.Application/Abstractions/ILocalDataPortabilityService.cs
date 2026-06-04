using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Application.Abstractions;

public interface ILocalDataPortabilityService
{
    Task<LocalDataExportSnapshot> CreateExportSnapshotAsync(CancellationToken cancellationToken = default);

    Task ExportAsync(string filePath, CancellationToken cancellationToken = default);

    Task ImportAsync(string filePath, CancellationToken cancellationToken = default);
}

public sealed record LocalDataExportSnapshot(
    string FormatVersion,
    DateTimeOffset ExportedAt,
    FrpNexusSettingsSnapshot Settings,
    IReadOnlyList<NodeProfile> Nodes,
    IReadOnlyList<TunnelProfile> Tunnels,
    IReadOnlyList<ConfigurationVersion> Configurations,
    IReadOnlyList<RuntimeProcess> RuntimeProcesses,
    IReadOnlyList<DeploymentRecord> DeploymentRecords);
