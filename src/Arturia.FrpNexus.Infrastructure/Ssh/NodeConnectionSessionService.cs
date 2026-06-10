using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Renci.SshNet.Common;
using Serilog;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Arturia.FrpNexus.Infrastructure.Ssh;

public sealed class NodeConnectionSessionService(
    ISshClientAdapter sshClientAdapter,
    ILogger logger) : INodeConnectionSessionService, IDisposable
{
    private readonly ConcurrentDictionary<string, ActiveNodeSession> _sessions =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, NodeConnectionSessionSnapshot> _lastSnapshots =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<NodeConnectionSessionResult> ConnectAsync(
        NodeProfile node,
        SshCredentialReference credential,
        CancellationToken cancellationToken = default)
    {
        await DisconnectAsync(node.Name, cancellationToken);
        var connectedAt = DateTimeOffset.UtcNow;

        try
        {
            var session = await sshClientAdapter.OpenSessionAsync(node, credential, cancellationToken);
            var activeSession = new ActiveNodeSession(session, credential, connectedAt);
            _sessions[node.Name] = activeSession;
            _lastSnapshots[node.Name] = new NodeConnectionSessionSnapshot(
                node.Name,
                NodeConnectionSessionState.Online,
                connectedAt,
                "SSH 节点连接在线。");

            logger.Information(
                "SSH node session connected for node {NodeName} at {Host}:{Port} using {AuthenticationMode}",
                node.Name,
                node.Host,
                node.SshPort,
                credential.AuthenticationMode);

            return new NodeConnectionSessionResult(
                node.Name,
                NodeConnectionSessionState.Online,
                connectedAt,
                "SSH 节点连接成功。");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.Warning(
                exception,
                "SSH node session failed for node {NodeName} at {Host}:{Port} using {AuthenticationMode}",
                node.Name,
                node.Host,
                node.SshPort,
                credential.AuthenticationMode);

            var result = new NodeConnectionSessionResult(
                node.Name,
                NodeConnectionSessionState.Error,
                null,
                GetFailureMessage(exception));
            _lastSnapshots[node.Name] = new NodeConnectionSessionSnapshot(
                node.Name,
                result.State,
                result.ConnectedAt,
                result.Message);

            return result;
        }
    }

    public Task<NodeConnectionSessionResult> DisconnectAsync(
        string nodeName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_sessions.TryRemove(nodeName, out var session))
        {
            session.Dispose();
        }

        var result = new NodeConnectionSessionResult(
            nodeName,
            NodeConnectionSessionState.Disconnected,
            null,
            "SSH 节点连接已断开。");
        _lastSnapshots[nodeName] = new NodeConnectionSessionSnapshot(
            result.NodeName,
            result.State,
            result.ConnectedAt,
            result.Message);

        return Task.FromResult(result);
    }

    public NodeConnectionSessionSnapshot GetSessionStatus(string nodeName)
    {
        if (!_sessions.TryGetValue(nodeName, out var session))
        {
            if (_lastSnapshots.TryGetValue(nodeName, out var snapshot))
            {
                return snapshot;
            }

            return new NodeConnectionSessionSnapshot(
                nodeName,
                NodeConnectionSessionState.Offline,
                null,
                "尚未连接。");
        }

        if (session.Session.IsConnected)
        {
            return new NodeConnectionSessionSnapshot(
                nodeName,
                NodeConnectionSessionState.Online,
                session.ConnectedAt,
                "SSH 节点连接在线。");
        }

        _sessions.TryRemove(nodeName, out _);
        session.Dispose();

        var disconnected = new NodeConnectionSessionSnapshot(
            nodeName,
            NodeConnectionSessionState.Disconnected,
            session.ConnectedAt,
            "SSH 连接已断开。");
        _lastSnapshots[nodeName] = disconnected;

        return disconnected;
    }

    public IReadOnlyList<NodeConnectionSessionSnapshot> ListActiveSessions()
    {
        var snapshots = new List<NodeConnectionSessionSnapshot>();
        foreach (var item in _sessions.ToArray())
        {
            var snapshot = GetSessionStatus(item.Key);
            if (snapshot.State == NodeConnectionSessionState.Online)
            {
                snapshots.Add(snapshot);
            }
        }

        return snapshots;
    }

    public SshCredentialReference? GetConnectedCredential(string nodeName)
    {
        var status = GetSessionStatus(nodeName);
        if (status.State != NodeConnectionSessionState.Online)
        {
            return null;
        }

        return _sessions.TryGetValue(nodeName, out var session)
            ? session.Credential
            : null;
    }

    public void Dispose()
    {
        foreach (var item in _sessions.ToArray())
        {
            if (_sessions.TryRemove(item.Key, out var session))
            {
                session.Dispose();
            }
        }

        _lastSnapshots.Clear();
    }

    private static string GetFailureMessage(Exception exception)
    {
        return exception switch
        {
            SshAuthenticationException => "SSH 认证失败，请检查用户名、密码、私钥或服务器认证策略。",
            SshConnectionException => "SSH 连接失败，请检查主机、端口、防火墙或网络。",
            SocketException => "SSH 连接失败，请检查主机、端口、防火墙或网络。",
            TimeoutException => "SSH 连接超时，请检查网络和服务器状态。",
            NotSupportedException => "SSH Agent 认证暂未接入。",
            _ => $"SSH 节点连接失败：{SanitizeMessage(exception.Message)}",
        };
    }

    private static string SanitizeMessage(string message)
    {
        var sanitized = string.IsNullOrWhiteSpace(message)
            ? "未返回详细错误。"
            : message.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();

        return sanitized.Length > 160
            ? sanitized[..160]
            : sanitized;
    }

    private sealed record ActiveNodeSession(
        ISshClientSession Session,
        SshCredentialReference Credential,
        DateTimeOffset ConnectedAt) : IDisposable
    {
        public void Dispose()
        {
            Session.Dispose();
        }
    }
}
