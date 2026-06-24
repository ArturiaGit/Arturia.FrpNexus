using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.ViewModels.Nodes;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class NodeDeploymentPresenceStateTests
{
    [Fact]
    public void CreateCacheKey_ShouldNormalizeNodeAndConfigPath()
    {
        var state = new NodeDeploymentPresenceState();
        var node = CreateNode("node-a", "opt/frp//frps.toml");

        var cacheKey = state.CreateCacheKey(node, "/opt/frp/frps.toml");

        Assert.Equal("node-a|/opt/frp/frps.toml", cacheKey);
    }

    [Fact]
    public void CacheAndTryRestore_ShouldRoundTripStatusSnapshot()
    {
        var state = new NodeDeploymentPresenceState();
        var snapshot = new DeploymentPresenceStatusSnapshot("ready", "files ready", "success");

        state.Cache("node-a|/opt/frp/frps.toml", snapshot);

        Assert.True(state.HasChecked("node-a|/opt/frp/frps.toml"));
        Assert.True(state.TryRestore("node-a|/opt/frp/frps.toml", out var restored));
        Assert.Equal(snapshot, restored);
    }

    [Fact]
    public void ClearNode_ShouldRemoveOnlyMatchingNodeEntries()
    {
        var state = new NodeDeploymentPresenceState();
        state.Cache("node-a|/opt/frp/frps.toml", new DeploymentPresenceStatusSnapshot("a", "a", "success"));
        state.Cache("node-b|/opt/frp/frps.toml", new DeploymentPresenceStatusSnapshot("b", "b", "warning"));

        state.ClearNode("node-a");

        Assert.False(state.HasChecked("node-a|/opt/frp/frps.toml"));
        Assert.True(state.HasChecked("node-b|/opt/frp/frps.toml"));
    }

    [Fact]
    public void TryRestoreNewer_ShouldOnlyRestoreWhenCacheVersionAdvanced()
    {
        var state = new NodeDeploymentPresenceState();
        var key = "node-a|/opt/frp/frps.toml";
        var previousVersion = state.GetVersion(key);

        Assert.False(state.TryRestoreNewer(key, previousVersion, out _));

        var snapshot = new DeploymentPresenceStatusSnapshot("ready", "files ready", "success");
        state.Cache(key, snapshot);

        Assert.True(state.TryRestoreNewer(key, previousVersion, out var restored));
        Assert.Equal(snapshot, restored);
    }

    [Fact]
    public void CreateCacheKey_ShouldUseDefaultConfigPathWhenNodeConfigPathIsBlank()
    {
        var state = new NodeDeploymentPresenceState();
        var node = CreateNode("node-a", string.Empty);

        var cacheKey = state.CreateCacheKey(node, "/opt/frp/frps.toml");

        Assert.Equal("node-a|/opt/frp/frps.toml", cacheKey);
    }

    private static NodeProfile CreateNode(string name, string configPath)
    {
        return new NodeProfile(
            name,
            "203.0.113.10",
            22,
            "deploy",
            "SessionPassword",
            "Ubuntu",
            FrpNexusStatus.Online,
            FrpNexusStatus.Stopped,
            "v0.61.1",
            "-",
            configPath);
    }
}
