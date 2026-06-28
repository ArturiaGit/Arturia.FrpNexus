using System.Text.Json;

namespace Arturia.FrpNexus.Desktop.Services;

public sealed class RemoteFrpsRetentionService : IRemoteFrpsRetentionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _statePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public RemoteFrpsRetentionService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Arturia",
            "FrpNexus",
            "state",
            "remote-frps-retention.json"))
    {
    }

    public RemoteFrpsRetentionService(string statePath)
    {
        _statePath = statePath;
    }

    public async Task<IReadOnlyList<RemoteFrpsRetentionRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadRecordsAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RemoteFrpsRetentionRecord?> GetAsync(string nodeName, CancellationToken cancellationToken = default)
    {
        var records = await ListAsync(cancellationToken);
        return records.FirstOrDefault(record =>
            string.Equals(record.NodeName, nodeName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task SaveAsync(RemoteFrpsRetentionRecord record, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(record.NodeName))
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var records = (await ReadRecordsAsync(cancellationToken)).ToList();
            records.RemoveAll(candidate =>
                string.Equals(candidate.NodeName, record.NodeName, StringComparison.OrdinalIgnoreCase));
            records.Add(record);
            await WriteRecordsAsync(records, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(string nodeName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var records = (await ReadRecordsAsync(cancellationToken)).ToList();
            records.RemoveAll(candidate =>
                string.Equals(candidate.NodeName, nodeName, StringComparison.OrdinalIgnoreCase));
            await WriteRecordsAsync(records, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<RemoteFrpsRetentionRecord>> ReadRecordsAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_statePath))
        {
            return [];
        }

        await using var stream = new FileStream(_statePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var records = await JsonSerializer.DeserializeAsync<List<RemoteFrpsRetentionRecord>>(
            stream,
            JsonOptions,
            cancellationToken);

        return records ?? [];
    }

    private async Task WriteRecordsAsync(IReadOnlyList<RemoteFrpsRetentionRecord> records, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_statePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = new FileStream(_statePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, records, JsonOptions, cancellationToken);
    }
}
