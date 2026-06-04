using System.Text;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Sftp;
using Serilog.Core;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class RemoteFileTransferServiceTests
{
    [Fact]
    public async Task UploadFrpBinaryAsync_ShouldUploadLocalFileAndRecordDeployment()
    {
        var localPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(localPath, "frpc-binary-placeholder");
        var deploymentService = new FakeDeploymentRecordService();
        var adapter = new FakeSftpClientAdapter();
        var service = new RemoteFileTransferService(adapter, deploymentService, Logger.None);

        var result = await service.UploadFrpBinaryAsync(new RemoteFileUploadRequest(
            CreateNode(),
            CreateCredential(),
            localPath,
            "/opt/frpnexus/frpc"));

        Assert.Equal(FrpNexusStatus.Ready, result.Status);
        Assert.Equal("FRP 核心上传成功。", result.Message);
        Assert.Equal("/opt/frpnexus/frpc", adapter.LastRemotePath);
        Assert.Equal("frpc-binary-placeholder", adapter.LastContent);
        Assert.Equal("上传 FRP 核心", deploymentService.LastRecord?.StepName);
        Assert.Equal(FrpNexusStatus.Ready, deploymentService.LastRecord?.Status);
        Assert.DoesNotContain("SESSION_PASSWORD_PLACEHOLDER", deploymentService.LastRecord?.Description ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadConfigurationAsync_ShouldUploadTomlText()
    {
        var deploymentService = new FakeDeploymentRecordService();
        var adapter = new FakeSftpClientAdapter();
        var service = new RemoteFileTransferService(adapter, deploymentService, Logger.None);

        var result = await service.UploadConfigurationAsync(new RemoteConfigurationUploadRequest(
            CreateNode(),
            CreateCredential(),
            "[[proxies]]\nname = \"web\"",
            "/etc/frp/frpc.toml"));

        Assert.Equal(FrpNexusStatus.Ready, result.Status);
        Assert.Equal("TOML 配置上传成功。", result.Message);
        Assert.Contains("[[proxies]]", adapter.LastContent);
        Assert.Equal("上传 TOML 配置", deploymentService.LastRecord?.StepName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative/frpc")]
    [InlineData("C:\\frpc")]
    public async Task UploadFrpBinaryAsync_ShouldRejectInvalidRemotePath(string remotePath)
    {
        var localPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(localPath, "frpc-binary-placeholder");
        var service = new RemoteFileTransferService(
            new FakeSftpClientAdapter(),
            new FakeDeploymentRecordService(),
            Logger.None);

        var result = await service.UploadFrpBinaryAsync(new RemoteFileUploadRequest(
            CreateNode(),
            CreateCredential(),
            localPath,
            remotePath));

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Equal("远程路径必须是 Linux 绝对路径。", result.Message);
    }

    [Fact]
    public async Task UploadConfigurationAsync_ShouldReturnChineseFailureAndHideSecret()
    {
        var deploymentService = new FakeDeploymentRecordService();
        var service = new RemoteFileTransferService(
            new FakeSftpClientAdapter(new InvalidOperationException("权限不足")),
            deploymentService,
            Logger.None);

        var result = await service.UploadConfigurationAsync(new RemoteConfigurationUploadRequest(
            CreateNode(),
            CreateCredential(),
            "[[proxies]]",
            "/etc/frp/frpc.toml"));

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Equal("SFTP 上传失败：权限不足", result.Message);
        Assert.DoesNotContain("SESSION_PASSWORD_PLACEHOLDER", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SESSION_PASSWORD_PLACEHOLDER", deploymentService.LastRecord?.Description ?? string.Empty, StringComparison.Ordinal);
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

    private sealed class FakeSftpClientAdapter(Exception? exception = null) : ISftpClientAdapter
    {
        public string? LastContent { get; private set; }

        public string? LastRemotePath { get; private set; }

        public async Task UploadFileAsync(
            NodeProfile node,
            SshCredentialReference credential,
            Stream content,
            string remotePath,
            CancellationToken cancellationToken = default)
        {
            if (exception is not null)
            {
                throw exception;
            }

            using var reader = new StreamReader(content, Encoding.UTF8);
            LastContent = await reader.ReadToEndAsync(cancellationToken);
            LastRemotePath = remotePath;
        }
    }

    private sealed class FakeDeploymentRecordService : IDeploymentRecordService
    {
        public DeploymentRecord? LastRecord { get; private set; }

        public Task<IReadOnlyList<DeploymentRecord>> ListDeploymentRecordsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DeploymentRecord>>([]);
        }

        public Task<DeploymentRecord?> GetDeploymentRecordAsync(string stepName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DeploymentRecord?>(null);
        }

        public Task SaveDeploymentRecordAsync(DeploymentRecord record, CancellationToken cancellationToken = default)
        {
            LastRecord = record;
            return Task.CompletedTask;
        }

        public Task DeleteDeploymentRecordAsync(string stepName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
