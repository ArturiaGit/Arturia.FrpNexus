using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Application.Abstractions;

public interface IRuntimeRecordService
{
    Task<IReadOnlyList<RuntimeProcess>> ListRuntimeProcessesAsync(CancellationToken cancellationToken = default);

    Task<RuntimeProcess?> GetRuntimeProcessAsync(string processName, CancellationToken cancellationToken = default);

    Task SaveRuntimeProcessAsync(RuntimeProcess process, CancellationToken cancellationToken = default);

    Task DeleteRuntimeProcessAsync(string processName, CancellationToken cancellationToken = default);
}
