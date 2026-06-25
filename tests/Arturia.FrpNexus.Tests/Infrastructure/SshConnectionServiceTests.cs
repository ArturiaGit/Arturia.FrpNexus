using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Ssh;
using Renci.SshNet.Common;
using Serilog.Core;
using Serilog;
using System.Net.Sockets;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class SshConnectionServiceTests
{
    [Fact]
    public async Task TestConnectionAsync_ShouldReturnSuccessAndPersistSafeMetadata()
    {
        var nodeService = new FakeNodeManagementService();
        var service = new SshConnectionService(new FakeSshClientAdapter(), nodeService, Logger.None);
        var request = CreateRequest("SESSION_PASSWORD_PLACEHOLDER");

        var result = await service.TestConnectionAsync(request);

        Assert.Equal(FrpNexusStatus.Online, result.Status);
        Assert.Equal("SSH 连接测试成功。", result.Message);
        Assert.Equal(FrpNexusStatus.Online, nodeService.LastStatus);
        Assert.Equal("测试节点", nodeService.LastNodeName);
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldReturnChineseFailureWithoutPersistingSecret()
    {
        var nodeService = new FakeNodeManagementService();
        var service = new SshConnectionService(
            new FakeSshClientAdapter(new InvalidOperationException("认证失败")),
            nodeService,
            Logger.None);
        var request = CreateRequest("SESSION_PASSWORD_PLACEHOLDER");

        var result = await service.TestConnectionAsync(request);

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Equal("SSH 连接测试失败：认证失败", result.Message);
        Assert.Equal(FrpNexusStatus.Error, nodeService.LastStatus);
        Assert.DoesNotContain("SESSION_PASSWORD_PLACEHOLDER", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldRedactSecretBearingGenericFailure()
    {
        var nodeService = new FakeNodeManagementService();
        var service = new SshConnectionService(
            new FakeSshClientAdapter(new InvalidOperationException("ssh failed with --password SECRET_PASSWORD --token=SECRET_TOKEN")),
            nodeService,
            Logger.None);

        var result = await service.TestConnectionAsync(CreateRequest("SESSION_PASSWORD_PLACEHOLDER"));

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.DoesNotContain("SECRET_PASSWORD", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_TOKEN", result.Message, StringComparison.Ordinal);
        Assert.Contains("--password [REDACTED]", result.Message, StringComparison.Ordinal);
        Assert.Contains("--token=[REDACTED]", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldMapAuthenticationFailureToRecoverableChineseMessage()
    {
        var nodeService = new FakeNodeManagementService();
        var service = new SshConnectionService(
            new FakeSshClientAdapter(new SshAuthenticationException("Permission denied (password).")),
            nodeService,
            Logger.None);
        var request = CreateRequest("SESSION_PASSWORD_PLACEHOLDER");

        var result = await service.TestConnectionAsync(request);

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Equal("SSH 认证失败，请检查用户名、密码、私钥或服务器认证策略。", result.Message);
        Assert.Equal(FrpNexusStatus.Error, nodeService.LastStatus);
        Assert.DoesNotContain("Permission denied", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SESSION_PASSWORD_PLACEHOLDER", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldMapConnectionFailureToRecoverableChineseMessage()
    {
        var nodeService = new FakeNodeManagementService();
        var service = new SshConnectionService(
            new FakeSshClientAdapter(new SshConnectionException("Connection refused")),
            nodeService,
            Logger.None);

        var result = await service.TestConnectionAsync(CreateRequest("SESSION_PASSWORD_PLACEHOLDER"));

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Equal("SSH 连接失败，请检查主机、端口、防火墙或网络。", result.Message);
        Assert.Equal(FrpNexusStatus.Error, nodeService.LastStatus);
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldMapSocketFailureToRecoverableChineseMessage()
    {
        var nodeService = new FakeNodeManagementService();
        var service = new SshConnectionService(
            new FakeSshClientAdapter(new SocketException()),
            nodeService,
            Logger.None);

        var result = await service.TestConnectionAsync(CreateRequest("SESSION_PASSWORD_PLACEHOLDER"));

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Equal("SSH 连接失败，请检查主机、端口、防火墙或网络。", result.Message);
        Assert.Equal(FrpNexusStatus.Error, nodeService.LastStatus);
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldMapTimeoutToRecoverableChineseMessage()
    {
        var nodeService = new FakeNodeManagementService();
        var service = new SshConnectionService(
            new FakeSshClientAdapter(new TimeoutException("Timed out")),
            nodeService,
            Logger.None);

        var result = await service.TestConnectionAsync(CreateRequest("SESSION_PASSWORD_PLACEHOLDER"));

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Equal("SSH 连接超时，请检查网络和服务器状态。", result.Message);
        Assert.Equal(FrpNexusStatus.Error, nodeService.LastStatus);
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldMapUnsupportedAuthenticationToRecoverableChineseMessage()
    {
        var nodeService = new FakeNodeManagementService();
        var service = new SshConnectionService(
            new FakeSshClientAdapter(new NotSupportedException("SSH Agent authentication is not supported.")),
            nodeService,
            Logger.None);

        var result = await service.TestConnectionAsync(CreateRequest("SESSION_PASSWORD_PLACEHOLDER"));

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Equal("SSH Agent 认证暂未接入。", result.Message);
        Assert.Equal(FrpNexusStatus.Error, nodeService.LastStatus);
    }

    [Fact]
    public void SshCredentialReference_ShouldNotExposePersistedSecretModel()
    {
        var properties = typeof(NodeProfile)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(properties, property => property.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Contains("PrivateKeyContent", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Contains("Passphrase", StringComparison.OrdinalIgnoreCase));
    }

    private static SshConnectionTestRequest CreateRequest(string password)
    {
        var node = new NodeProfile(
            "测试节点",
            "203.0.113.10",
            22,
            "deploy",
            "会话密码",
            "Ubuntu 22.04 LTS",
            FrpNexusStatus.Offline,
            FrpNexusStatus.Stopped,
            "v0.61.1",
            "-",
            "/etc/frp/frpc.toml");

        return new SshConnectionTestRequest(
            node,
            new SshCredentialReference(SshAuthenticationMode.SessionPassword, SessionPassword: password));
    }

    private sealed class FakeSshClientAdapter(Exception? exception = null) : ISshClientAdapter
    {
        public Task ConnectAsync(NodeProfile node, SshCredentialReference credential, CancellationToken cancellationToken = default)
        {
            if (exception is not null)
            {
                throw exception;
            }

            return Task.CompletedTask;
        }

        public Task<ISshClientSession> OpenSessionAsync(
            NodeProfile node,
            SshCredentialReference credential,
            CancellationToken cancellationToken = default)
        {
            if (exception is not null)
            {
                throw exception;
            }

            return Task.FromResult<ISshClientSession>(new FakeSshClientSession());
        }
    }

    private sealed class FakeSshClientSession : ISshClientSession
    {
        public bool IsConnected { get; private set; } = true;

        public void Disconnect()
        {
            IsConnected = false;
        }

        public void Dispose()
        {
            Disconnect();
        }
    }

    private sealed class FakeNodeManagementService : INodeManagementService
    {
        public string? LastNodeName { get; private set; }

        public FrpNexusStatus? LastStatus { get; private set; }

        public Task<IReadOnlyList<NodeProfile>> ListNodesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<NodeProfile>>([]);
        }

        public Task<NodeProfile?> GetNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<NodeProfile?>(null);
        }

        public Task SaveNodeAsync(NodeProfile node, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateLastConnectionAsync(
            string nodeName,
            DateTimeOffset connectedAt,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateConnectionTestResultAsync(
            string nodeName,
            FrpNexusStatus status,
            DateTimeOffset testedAt,
            CancellationToken cancellationToken = default)
        {
            LastNodeName = nodeName;
            LastStatus = status;
            return Task.CompletedTask;
        }
    }
}
