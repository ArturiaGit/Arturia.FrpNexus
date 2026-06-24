using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Sftp;
using Serilog.Core;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class RemoteDirectoryServiceTests
{
    [Fact]
    public async Task ListDirectoriesAsync_ShouldMapTimeoutFailureAndHideSecret()
    {
        var service = new RemoteDirectoryService(
            new FakeSftpClientAdapter(new TimeoutException("SESSION_PASSWORD_PLACEHOLDER timed out")),
            Logger.None);

        var result = await service.ListDirectoriesAsync(new RemoteDirectoryListRequest(
            CreateNode(),
            CreateCredential(),
            "/opt/frp"));

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Equal("远程目录读取超时：远程节点响应过慢，请检查网络和服务器状态。", result.Message);
        Assert.DoesNotContain("SESSION_PASSWORD_PLACEHOLDER", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureDirectoryAsync_ShouldMapTimeoutFailureAndHideSecret()
    {
        var service = new RemoteDirectoryService(
            new FakeSftpClientAdapter(new TimeoutException("SESSION_PASSWORD_PLACEHOLDER timed out")),
            Logger.None);

        var result = await service.EnsureDirectoryAsync(new RemoteDirectoryEnsureRequest(
            CreateNode(),
            CreateCredential(),
            "/opt/frp"));

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Equal("远程目录准备超时：远程节点响应过慢，请检查网络和服务器状态。", result.Message);
        Assert.DoesNotContain("SESSION_PASSWORD_PLACEHOLDER", result.Message, StringComparison.Ordinal);
    }

    private static NodeProfile CreateNode()
    {
        return new NodeProfile(
            "测试节点",
            "203.0.113.10",
            22,
            "deploy",
            "会话密码",
            "Ubuntu 22.04 LTS",
            FrpNexusStatus.Online,
            FrpNexusStatus.Stopped,
            "v0.61.1",
            "-",
            "/etc/frp/frpc.toml");
    }

    private static SshCredentialReference CreateCredential()
    {
        return new SshCredentialReference(
            SshAuthenticationMode.SessionPassword,
            SessionPassword: "SESSION_PASSWORD_PLACEHOLDER");
    }

    private sealed class FakeSftpClientAdapter(Exception exception) : ISftpClientAdapter
    {
        public Task<IReadOnlyList<RemoteDirectoryEntry>> ListDirectoriesAsync(
            NodeProfile node,
            SshCredentialReference credential,
            string remotePath,
            CancellationToken cancellationToken = default)
        {
            throw exception;
        }

        public Task CreateDirectoryAsync(
            NodeProfile node,
            SshCredentialReference credential,
            string remotePath,
            CancellationToken cancellationToken = default)
        {
            throw exception;
        }

        public Task EnsureDirectoryAsync(
            NodeProfile node,
            SshCredentialReference credential,
            string remotePath,
            CancellationToken cancellationToken = default)
        {
            throw exception;
        }

        public Task UploadFileAsync(
            NodeProfile node,
            SshCredentialReference credential,
            Stream content,
            string remotePath,
            CancellationToken cancellationToken = default)
        {
            throw exception;
        }

        public Task<bool> FileExistsAsync(
            NodeProfile node,
            SshCredentialReference credential,
            string remotePath,
            CancellationToken cancellationToken = default)
        {
            throw exception;
        }

        public Task DeleteFileAsync(
            NodeProfile node,
            SshCredentialReference credential,
            string remotePath,
            CancellationToken cancellationToken = default)
        {
            throw exception;
        }

        public Task RenameFileAsync(
            NodeProfile node,
            SshCredentialReference credential,
            string sourcePath,
            string destinationPath,
            CancellationToken cancellationToken = default)
        {
            throw exception;
        }
    }
}
