using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Application.Abstractions;

public interface IRemoteRuntimeService
{
    Task<IReadOnlyList<RuntimeProcess>> GetProcessesAsync(string nodeName, CancellationToken cancellationToken = default);

    Task StartAsync(string nodeName, string processName, CancellationToken cancellationToken = default);

    Task StopAsync(string nodeName, string processName, CancellationToken cancellationToken = default);

    Task RestartAsync(string nodeName, string processName, CancellationToken cancellationToken = default);
}
