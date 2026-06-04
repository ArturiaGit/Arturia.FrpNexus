using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Application.Abstractions;

public interface IRemoteLogService
{
    Task<IReadOnlyList<LogEntry>> ReadRecentLogsAsync(string nodeName, string processName, CancellationToken cancellationToken = default);
}
