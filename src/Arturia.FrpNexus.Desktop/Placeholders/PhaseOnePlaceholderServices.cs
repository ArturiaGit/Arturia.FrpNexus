using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Desktop.Placeholders;

public sealed class PhaseOneNodeManagementService : INodeManagementService
{
    private static readonly IReadOnlyList<NodeProfile> Nodes =
    [
        new(
            "东京-生产节点",
            "203.0.113.10",
            22,
            "deploy",
            "SSH Key",
            "Ubuntu 22.04 LTS",
            FrpNexusStatus.Online,
            FrpNexusStatus.Running,
            "v0.61.1",
            "15 天 04:12",
            "/opt/frpnexus/frpc.toml"),
        new(
            "家用 NAS",
            "192.168.31.20",
            2222,
            "admin",
            "SSH Key",
            "Debian 12",
            FrpNexusStatus.Offline,
            FrpNexusStatus.Stopped,
            "未安装",
            "-",
            "/opt/frpnexus/frpc.toml")
    ];

    public Task<IReadOnlyList<NodeProfile>> ListNodesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Nodes);
    }

    public Task<NodeProfile?> GetNodeAsync(string nodeName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var node = Nodes.FirstOrDefault(item => string.Equals(item.Name, nodeName, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(node);
    }

    public Task SaveNodeAsync(NodeProfile node, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task DeleteNodeAsync(string nodeName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task UpdateLastConnectionAsync(
        string nodeName,
        DateTimeOffset connectedAt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task UpdateConnectionTestResultAsync(
        string nodeName,
        FrpNexusStatus status,
        DateTimeOffset testedAt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

public sealed class PhaseOneSshConnectionService : ISshConnectionService
{
    public Task<SshConnectionTestResult> TestConnectionAsync(SshConnectionTestRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SshConnectionTestResult(
            request.Node.Name,
            FrpNexusStatus.Pending,
            DateTimeOffset.UtcNow,
            "SSH 连接测试占位服务尚未接入。"));
    }
}

public sealed class PhaseOneRemoteFileTransferService : IRemoteFileTransferService
{
    public Task<RemoteFilePresenceResult> CheckRemoteFilesAsync(RemoteFilePresenceRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new RemoteFilePresenceResult(
            request.Node.Name,
            request.RemotePaths.Select(path => new RemoteFilePresenceEntry(path, false)).ToArray(),
            FrpNexusStatus.Pending,
            DateTimeOffset.UtcNow,
            "SFTP 远程文件检查占位服务尚未接入。"));
    }

    public Task<RemoteFileTransferResult> UploadFrpBinaryAsync(RemoteFileUploadRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new RemoteFileTransferResult(
            request.Node.Name,
            request.RemotePath,
            FrpNexusStatus.Pending,
            DateTimeOffset.UtcNow,
            "SFTP 文件上传占位服务尚未接入。"));
    }

    public Task<RemoteFileTransferResult> UploadConfigurationAsync(RemoteConfigurationUploadRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new RemoteFileTransferResult(
            request.Node.Name,
            request.RemotePath,
            FrpNexusStatus.Pending,
            DateTimeOffset.UtcNow,
            "SFTP 配置上传占位服务尚未接入。"));
    }

    public Task<RemoteFileDeleteResult> DeleteRemoteFilesAsync(RemoteFileDeleteRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new RemoteFileDeleteResult(
            request.Node.Name,
            [],
            request.RemotePaths,
            FrpNexusStatus.Pending,
            DateTimeOffset.UtcNow,
            "SFTP 远程文件清理占位服务尚未接入。"));
    }
}

public sealed class PhaseOneFrpReleaseService : IFrpReleaseService
{
    public Task<IReadOnlyList<FrpReleaseVersion>> ListAvailableVersionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<FrpReleaseVersion> versions =
        [
            new("v0.61.1", DateTimeOffset.UtcNow),
            new("v0.60.0", DateTimeOffset.UtcNow),
            new("v0.59.0", DateTimeOffset.UtcNow)
        ];
        return Task.FromResult(versions);
    }

    public Task<FrpReleasePreparationResult> PrepareReleaseAsync(FrpReleasePreparationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new FrpReleasePreparationResult(
            request.Version,
            request.TargetRuntime,
            request.BinaryName,
            string.Empty,
            DateTimeOffset.UtcNow,
            "FRP Release 准备占位服务尚未接入。"));
    }
}

public sealed class PhaseOneTomlConfigurationService : ITomlConfigurationService
{
    public string GenerateProxyToml(ConfigurationPreview preview)
    {
        return preview.Toml;
    }

    public string GenerateClientToml(NodeProfile node, IReadOnlyList<TunnelProfile> tunnels)
    {
        return string.Join(
            Environment.NewLine,
            tunnels.Select(tunnel => GenerateProxyToml(new ConfigurationPreview(
                tunnel.Name,
                tunnel.Protocol,
                tunnel.LocalAddress,
                tunnel.LocalPort,
                tunnel.RemoteEndpoint,
                string.Empty))));
    }

    public string GenerateServerToml(int bindPort)
    {
        if (bindPort is < 1 or > 65535)
        {
            throw new InvalidOperationException("frps 监听端口必须是 1 到 65535 之间的数字。");
        }

        return $"bindPort = {bindPort}";
    }

    public Task ValidateAsync(string tomlContent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

public sealed class PhaseOneRemoteRuntimeService : IRemoteRuntimeService
{
    public Task<IReadOnlyList<RuntimeProcess>> GetProcessesAsync(RemoteRuntimeQueryRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<RuntimeProcess> processes =
        [
            new("frpc-web", request.Node.Name, "frpc", FrpNexusStatus.Running, "1842", "02:14:33", "0.0.0.0:7000"),
            new("frpc-nas", request.Node.Name, "frpc", FrpNexusStatus.Stopped, "-", "-", "-")
        ];
        return Task.FromResult(processes);
    }

    public Task<RemoteRuntimeCommandResult> StartAsync(RemoteRuntimeCommandRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return CreatePlaceholderResult(request, "远程启动占位服务尚未接入。");
    }

    public Task<RemoteRuntimeCommandResult> StopAsync(RemoteRuntimeCommandRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return CreatePlaceholderResult(request, "远程停止占位服务尚未接入。");
    }

    public Task<RemoteRuntimeCommandResult> RestartAsync(RemoteRuntimeCommandRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return CreatePlaceholderResult(request, "远程重启占位服务尚未接入。");
    }

    private static Task<RemoteRuntimeCommandResult> CreatePlaceholderResult(RemoteRuntimeCommandRequest request, string message)
    {
        return Task.FromResult(new RemoteRuntimeCommandResult(
            request.Node.Name,
            request.ProcessName,
            FrpNexusStatus.Pending,
            DateTimeOffset.UtcNow,
            message));
    }
}

public sealed class PhaseOneRemoteLogService : IRemoteLogService
{
    public Task<IReadOnlyList<LogEntry>> ReadRecentLogsAsync(RemoteLogReadRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<LogEntry> logs =
        [
            new("2026-06-04 12:00:00", "INFO", request.Node.Name, request.ProcessName, "Phase 1 placeholder log entry.", FrpNexusStatus.Running),
            new("2026-06-04 12:01:10", "WARN", request.Node.Name, request.ProcessName, "Placeholder warning for UI state verification.", FrpNexusStatus.Warning)
        ];
        return Task.FromResult(logs);
    }

    public async IAsyncEnumerable<LogEntry> StreamLogsAsync(RemoteLogReadRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var logs = await ReadRecentLogsAsync(request, cancellationToken);
        foreach (var log in logs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return log;
        }
    }
}

public sealed class PhaseOneSettingsService : ISettingsService
{
    public Task<FrpNexusSettingsSnapshot> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = new FrpNexusSettingsSnapshot(
            "GitHub Releases",
            "%LocalAppData%\\Arturia\\FrpNexus\\core",
            "%LocalAppData%\\Arturia\\FrpNexus\\configs",
            "%LocalAppData%\\Arturia\\FrpNexus\\logs",
            "%LocalAppData%\\Arturia\\FrpNexus\\data\\frpnexus.db");

        return Task.FromResult(settings);
    }

    public Task SaveSettingsAsync(FrpNexusSettingsSnapshot settings, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
