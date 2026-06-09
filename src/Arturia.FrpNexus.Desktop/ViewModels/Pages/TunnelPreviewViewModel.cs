using Arturia.FrpNexus.Core.ExcaliburTunnel;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed class TunnelPreviewViewModel
{
    public TunnelPreviewViewModel(
        string id,
        string name,
        string protocol,
        string localEndpoint,
        string remoteEndpoint,
        bool enabled,
        TunnelProfile profile)
    {
        Id = id;
        Name = name;
        Protocol = protocol;
        LocalEndpoint = localEndpoint;
        RemoteEndpoint = remoteEndpoint;
        Enabled = enabled;
        Profile = profile;
    }

    public string Id { get; }

    public string Name { get; }

    public string Protocol { get; }

    public string LocalEndpoint { get; }

    public string RemoteEndpoint { get; }

    public bool Enabled { get; }

    public string EnabledText => Enabled ? "启用" : "停用";

    public TunnelProfile Profile { get; }
}
