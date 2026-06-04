namespace Arturia.FrpNexus.Core.Models;

public sealed record ConfigurationVersion(
    string Name,
    TunnelProtocol Protocol,
    string LocalAddress,
    int LocalPort,
    string RemoteEndpoint,
    string Toml,
    DateTimeOffset UpdatedAt);
