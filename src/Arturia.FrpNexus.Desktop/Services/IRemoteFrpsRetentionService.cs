namespace Arturia.FrpNexus.Desktop.Services;

public interface IRemoteFrpsRetentionService
{
    Task<IReadOnlyList<RemoteFrpsRetentionRecord>> ListAsync(CancellationToken cancellationToken = default);

    Task<RemoteFrpsRetentionRecord?> GetAsync(string nodeName, CancellationToken cancellationToken = default);

    Task SaveAsync(RemoteFrpsRetentionRecord record, CancellationToken cancellationToken = default);

    Task ClearAsync(string nodeName, CancellationToken cancellationToken = default);
}

public sealed record RemoteFrpsRetentionRecord(
    string NodeName,
    DateTimeOffset RetainedAt,
    string Status,
    string ConfigPath);
