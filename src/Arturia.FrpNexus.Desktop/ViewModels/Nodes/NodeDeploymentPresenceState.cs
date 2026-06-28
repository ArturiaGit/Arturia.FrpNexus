using System;
using System.Collections.Generic;
using System.Linq;
using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Desktop.ViewModels.Nodes;

public sealed class NodeDeploymentPresenceState
{
    private readonly HashSet<string> _checkedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DeploymentPresenceStatusSnapshot> _statusCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _cacheVersions = new(StringComparer.OrdinalIgnoreCase);

    public string CreateCacheKey(NodeProfile node, string defaultConfigPath)
    {
        var normalizedConfigPath = NodeConnectionWorkflowHelpers.NormalizeRemotePath(
            string.IsNullOrWhiteSpace(node.ConfigPath) ? defaultConfigPath : node.ConfigPath);
        return $"{node.Name}|{normalizedConfigPath}";
    }

    public bool HasChecked(string cacheKey)
    {
        return _checkedKeys.Contains(cacheKey);
    }

    public int GetVersion(string cacheKey)
    {
        return _cacheVersions.TryGetValue(cacheKey, out var version) ? version : 0;
    }

    public void Cache(string cacheKey, DeploymentPresenceStatusSnapshot snapshot)
    {
        _checkedKeys.Add(cacheKey);
        _statusCache[cacheKey] = snapshot;
        _cacheVersions[cacheKey] = GetVersion(cacheKey) + 1;
    }

    public bool TryRestore(string cacheKey, out DeploymentPresenceStatusSnapshot snapshot)
    {
        return _statusCache.TryGetValue(cacheKey, out snapshot!);
    }

    public bool TryRestoreNewer(
        string cacheKey,
        int previousVersion,
        out DeploymentPresenceStatusSnapshot snapshot)
    {
        if (GetVersion(cacheKey) <= previousVersion)
        {
            snapshot = default!;
            return false;
        }

        return TryRestore(cacheKey, out snapshot);
    }

    public void ClearNode(string nodeName)
    {
        var prefix = $"{nodeName}|";
        _checkedKeys.RemoveWhere(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        foreach (var key in _statusCache.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToArray())
        {
            _statusCache.Remove(key);
        }

        foreach (var key in _cacheVersions.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToArray())
        {
            _cacheVersions.Remove(key);
        }
    }
}

public sealed record DeploymentPresenceStatusSnapshot(string Title, string Text, string Severity);
