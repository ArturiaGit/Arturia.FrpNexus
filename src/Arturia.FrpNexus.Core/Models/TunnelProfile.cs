namespace Arturia.FrpNexus.Core.Models;

public sealed record TunnelProfile(
    string Name,
    TunnelProtocol Protocol,
    string NodeName,
    string LocalAddress,
    int LocalPort,
    string RemoteEndpoint,
    FrpNexusStatus Status,
    string Remark);
