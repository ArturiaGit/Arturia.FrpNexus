using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Desktop.Services;

public interface IFrpLifecycleStateService
{
    IReadOnlyList<RemoteFrpsLifecycleSnapshot> ListRemoteFrpsSnapshots();

    void UpdateRemoteFrpsState(string nodeName, bool isSshOnline, FrpNexusStatus frpsStatus);

    void RemoveRemoteFrpsState(string nodeName);
}

public sealed record RemoteFrpsLifecycleSnapshot(
    string NodeName,
    bool IsSshOnline,
    FrpNexusStatus FrpsStatus);
