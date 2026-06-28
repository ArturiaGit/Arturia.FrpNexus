using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Desktop.ViewModels.Nodes;

public interface INodeRemoteFrpsWorkflow
{
    Task<NodeRemoteFrpsRefreshResult> RefreshAsync(
        NodeProfile node,
        SshCredentialReference credential,
        CancellationToken cancellationToken = default);

    Task<RemoteRuntimeCommandResult> ExecuteAsync(
        NodeProfile node,
        SshCredentialReference credential,
        NodeRemoteFrpsAction action,
        string frpsBinaryPath,
        CancellationToken cancellationToken = default);
}

public enum NodeRemoteFrpsAction
{
    Start,
    Stop,
    Restart
}

public sealed record NodeRemoteFrpsRefreshResult(
    FrpNexusStatus Status,
    string Uptime,
    bool IsAmbiguous,
    bool ShouldClearRetention);

public sealed class NodeRemoteFrpsWorkflow(IRemoteRuntimeService remoteRuntimeService) : INodeRemoteFrpsWorkflow
{
    public async Task<NodeRemoteFrpsRefreshResult> RefreshAsync(
        NodeProfile node,
        SshCredentialReference credential,
        CancellationToken cancellationToken = default)
    {
        var processes = await remoteRuntimeService.GetProcessesAsync(
            new RemoteRuntimeQueryRequest(node, credential),
            cancellationToken);
        var match = ResolveRemoteFrpsRuntimeProcess(processes, node.ConfigPath);
        if (match.IsAmbiguous)
        {
            return new NodeRemoteFrpsRefreshResult(FrpNexusStatus.Warning, node.Uptime, true, false);
        }

        var process = match.Process;
        var status = process is null ? FrpNexusStatus.Stopped : FrpNexusStatus.Running;
        var uptime = process is null || string.IsNullOrWhiteSpace(process.Uptime)
            ? "-"
            : process.Uptime;

        return new NodeRemoteFrpsRefreshResult(status, uptime, false, true);
    }

    public Task<RemoteRuntimeCommandResult> ExecuteAsync(
        NodeProfile node,
        SshCredentialReference credential,
        NodeRemoteFrpsAction action,
        string frpsBinaryPath,
        CancellationToken cancellationToken = default)
    {
        var request = new RemoteRuntimeCommandRequest(
            node,
            credential,
            $"frps-{node.Name}",
            "frps",
            BuildCommand(node, action, frpsBinaryPath));

        return action switch
        {
            NodeRemoteFrpsAction.Start => remoteRuntimeService.StartAsync(request, cancellationToken),
            NodeRemoteFrpsAction.Stop => remoteRuntimeService.StopAsync(request, cancellationToken),
            _ => remoteRuntimeService.RestartAsync(request, cancellationToken)
        };
    }

    private static string BuildCommand(NodeProfile node, NodeRemoteFrpsAction action, string frpsBinaryPath)
    {
        return action switch
        {
            NodeRemoteFrpsAction.Start => BuildStartCommand(node, frpsBinaryPath),
            NodeRemoteFrpsAction.Stop => "pkill -f '[f]rps' || true",
            _ => $"pkill -f '[f]rps' || true; sleep 1; {BuildStartCommand(node, frpsBinaryPath)}"
        };
    }

    private static string BuildStartCommand(NodeProfile node, string frpsBinaryPath)
    {
        var configPath = string.IsNullOrWhiteSpace(node.ConfigPath)
            ? "/opt/frp/frps.toml"
            : node.ConfigPath.Trim();

        return string.Join(
            " ",
            $"chmod +x {ShellQuote(frpsBinaryPath)} &&",
            $"(nohup {ShellQuote(frpsBinaryPath)} -c {ShellQuote(configPath)} >/tmp/frpnexus-frps.log 2>&1 &",
            "frps_pid=$!;",
            "sleep 1;",
            "if kill -0 \"$frps_pid\" 2>/dev/null && ps -p \"$frps_pid\" -o args= | grep -q '[f]rps'; then exit 0; fi;",
            "echo 'frps start command returned, but the process did not stay running.';",
            "tail -n 20 /tmp/frpnexus-frps.log 2>/dev/null || true;",
            "exit 1; )");
    }

    private static RemoteFrpsRuntimeProcessMatch ResolveRemoteFrpsRuntimeProcess(
        IReadOnlyList<RuntimeProcess> processes,
        string configPath)
    {
        var frpsProcesses = processes
            .Where(process => string.Equals(process.ProcessKind, "frps", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (frpsProcesses.Length == 0)
        {
            return new RemoteFrpsRuntimeProcessMatch(null, false);
        }

        var matchedByConfig = frpsProcesses
            .Where(process => CommandUsesConfigPath(process.CommandLine, configPath))
            .ToArray();
        if (matchedByConfig.Length == 1)
        {
            return new RemoteFrpsRuntimeProcessMatch(matchedByConfig[0], false);
        }

        if (frpsProcesses.Length == 1)
        {
            return new RemoteFrpsRuntimeProcessMatch(frpsProcesses[0], false);
        }

        return new RemoteFrpsRuntimeProcessMatch(null, true);
    }

    private static bool CommandUsesConfigPath(string commandLine, string expectedConfigPath)
    {
        if (string.IsNullOrWhiteSpace(commandLine) || string.IsNullOrWhiteSpace(expectedConfigPath))
        {
            return false;
        }

        var normalizedCommand = NormalizeLinuxPathForCommandMatch(commandLine);
        var normalizedExpectedPath = NormalizeLinuxPathForCommandMatch(expectedConfigPath);
        return normalizedCommand.Contains(normalizedExpectedPath, StringComparison.Ordinal);
    }

    private static string NormalizeLinuxPathForCommandMatch(string value)
    {
        return value.Trim().Trim('"', '\'').Replace('\\', '/');
    }

    private static string ShellQuote(string value)
    {
        return $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
    }

    private sealed record RemoteFrpsRuntimeProcessMatch(RuntimeProcess? Process, bool IsAmbiguous);
}
