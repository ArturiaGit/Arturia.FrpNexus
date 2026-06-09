namespace Arturia.FrpNexus.Core.ExcaliburTunnel;

public sealed record TunnelProfile(
    string Id,
    string Name,
    TunnelProtocol Protocol,
    string LocalHost,
    int LocalPort,
    int RemotePort,
    string ServerAddress,
    int ServerPort,
    bool Enabled);
