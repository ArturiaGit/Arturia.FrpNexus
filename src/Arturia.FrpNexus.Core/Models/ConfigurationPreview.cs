namespace Arturia.FrpNexus.Core.Models;

public sealed record ConfigurationPreview(
    string ProxyName,
    TunnelProtocol Protocol,
    string LocalAddress,
    int LocalPort,
    string RemoteEndpoint,
    string Toml);
