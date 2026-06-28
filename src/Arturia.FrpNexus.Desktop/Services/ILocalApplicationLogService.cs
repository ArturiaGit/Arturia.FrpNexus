using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Desktop.Services;

public interface ILocalApplicationLogService
{
    string CurrentLogDirectory { get; }

    Task<IReadOnlyList<LogEntry>> ReadRecentLogsAsync(
        int lineCount = 200,
        CancellationToken cancellationToken = default);
}
