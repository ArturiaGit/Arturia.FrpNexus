using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Desktop.Services;

public sealed class FrpLifecycleStateService : IFrpLifecycleStateService
{
    private readonly Dictionary<string, RemoteFrpsLifecycleSnapshot> _remoteFrpsSnapshots =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object _gate = new();

    public IReadOnlyList<RemoteFrpsLifecycleSnapshot> ListRemoteFrpsSnapshots()
    {
        lock (_gate)
        {
            return _remoteFrpsSnapshots.Values.ToArray();
        }
    }

    public void UpdateRemoteFrpsState(
        string nodeName,
        bool isSshOnline,
        FrpNexusStatus frpsStatus,
        string configPath = "")
    {
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            return;
        }

        lock (_gate)
        {
            _remoteFrpsSnapshots[nodeName] = new RemoteFrpsLifecycleSnapshot(
                nodeName,
                isSshOnline,
                frpsStatus,
                configPath);
        }
    }

    public void RemoveRemoteFrpsState(string nodeName)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            return;
        }

        lock (_gate)
        {
            _remoteFrpsSnapshots.Remove(nodeName);
        }
    }
}
