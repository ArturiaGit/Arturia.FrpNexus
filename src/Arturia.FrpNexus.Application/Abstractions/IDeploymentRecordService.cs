using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Application.Abstractions;

public interface IDeploymentRecordService
{
    Task<IReadOnlyList<DeploymentRecord>> ListDeploymentRecordsAsync(CancellationToken cancellationToken = default);

    Task<DeploymentRecord?> GetDeploymentRecordAsync(string stepName, CancellationToken cancellationToken = default);

    Task SaveDeploymentRecordAsync(DeploymentRecord record, CancellationToken cancellationToken = default);

    Task DeleteDeploymentRecordAsync(string stepName, CancellationToken cancellationToken = default);
}
