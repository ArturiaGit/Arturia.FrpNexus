using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Application.Abstractions;

public interface IRemoteLogService
{
    Task<IReadOnlyList<LogEntry>> ReadRecentLogsAsync(
        RemoteLogReadRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<LogEntry> StreamLogsAsync(
        RemoteLogReadRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record RemoteLogReadRequest(
    NodeProfile Node,
    SshCredentialReference Credential,
    string ProcessName,
    string LogPath,
    int LineCount = 200);
