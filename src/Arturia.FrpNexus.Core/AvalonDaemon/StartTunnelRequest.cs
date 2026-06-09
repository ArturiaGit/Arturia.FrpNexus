using Arturia.FrpNexus.Core.ExcaliburTunnel;

namespace Arturia.FrpNexus.Core.AvalonDaemon;

public sealed record StartTunnelRequest(
    TunnelProfile Profile,
    string FrpcPath,
    bool KeepGeneratedConfig = false);
