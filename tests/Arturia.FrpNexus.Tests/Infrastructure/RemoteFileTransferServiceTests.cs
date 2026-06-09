using System.Text;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Sftp;
using Renci.SshNet.Common;
using Serilog.Core;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class RemoteFileTransferServiceTests
{
    [Fact]
    public async Task CheckRemoteFilesAsync_ShouldReturnReadyWhenAllFilesExist()
    {
        var adapter = new FakeSftpClientAdapter();
        adapter.ExistingFiles.Add("/opt/frp/frps");
        adapter.ExistingFiles.Add("/opt/frp/frps.toml");
        var service = new RemoteFileTransferService(
            adapter,
            new FakeRemoteDirectoryService(),
            new FakeDeploymentRecordService(),
            Logger.None);

        var result = await service.CheckRemoteFilesAsync(new RemoteFilePresenceRequest(
            CreateNode(),
            CreateCredential(),
            ["/opt/frp/frps", "/opt/frp/frps.toml"]));

        Assert.Equal(FrpNexusStatus.Ready, result.Status);
        Assert.All(result.Files, entry => Assert.True(entry.Exists));
        Assert.Equal("远程 frps 和 frps.toml 已就绪。", result.Message);
        Assert.Equal(
            [
                "exists:/opt/frp/frps",
                "exists:/opt/frp/frps.toml"
            ],
            adapter.Operations);
    }

    [Fact]
    public async Task CheckRemoteFilesAsync_ShouldReturnWarningWhenFileIsMissing()
    {
        var adapter = new FakeSftpClientAdapter();
        adapter.ExistingFiles.Add("/opt/frp/frps");
        var service = new RemoteFileTransferService(
            adapter,
            new FakeRemoteDirectoryService(),
            new FakeDeploymentRecordService(),
            Logger.None);

        var result = await service.CheckRemoteFilesAsync(new RemoteFilePresenceRequest(
            CreateNode(),
            CreateCredential(),
            ["/opt/frp/frps", "/opt/frp/frps.toml"]));

        Assert.Equal(FrpNexusStatus.Warning, result.Status);
        Assert.Contains(result.Files, entry => entry.RemotePath == "/opt/frp/frps" && entry.Exists);
        Assert.Contains(result.Files, entry => entry.RemotePath == "/opt/frp/frps.toml" && !entry.Exists);
        Assert.Equal("远程部署文件不完整，需要补齐缺失文件。", result.Message);
    }

    [Fact]
    public async Task CheckRemoteFilesAsync_ShouldReturnErrorForSftpFailureAndHideSecret()
    {
        var adapter = new FakeSftpClientAdapter { FileExistsException = new SshConnectionException("bad password SESSION_PASSWORD_PLACEHOLDER") };
        var service = new RemoteFileTransferService(
            adapter,
            new FakeRemoteDirectoryService(),
            new FakeDeploymentRecordService(),
            Logger.None);

        var result = await service.CheckRemoteFilesAsync(new RemoteFilePresenceRequest(
            CreateNode(),
            CreateCredential(),
            ["/opt/frp/frps", "/opt/frp/frps.toml"]));

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Contains("SFTP 检查失败", result.Message);
        Assert.DoesNotContain("SESSION_PASSWORD_PLACEHOLDER", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative/frps")]
    [InlineData("C:\\frp\\frps")]
    public async Task CheckRemoteFilesAsync_ShouldRejectInvalidRemotePath(string remotePath)
    {
        var adapter = new FakeSftpClientAdapter();
        var service = new RemoteFileTransferService(
            adapter,
            new FakeRemoteDirectoryService(),
            new FakeDeploymentRecordService(),
            Logger.None);

        var result = await service.CheckRemoteFilesAsync(new RemoteFilePresenceRequest(
            CreateNode(),
            CreateCredential(),
            [remotePath]));

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Empty(adapter.Operations);
    }

    [Fact]
    public async Task UploadFrpBinaryAsync_ShouldUploadLocalFileAndRecordDeployment()
    {
        var localPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(localPath, "frpc-binary-placeholder");
        var deploymentService = new FakeDeploymentRecordService();
        var adapter = new FakeSftpClientAdapter();
        var directoryService = new FakeRemoteDirectoryService();
        var service = new RemoteFileTransferService(adapter, directoryService, deploymentService, Logger.None);

        var result = await service.UploadFrpBinaryAsync(new RemoteFileUploadRequest(
            CreateNode(),
            CreateCredential(),
            localPath,
            "/opt/frpnexus/frpc"));

        Assert.Equal(FrpNexusStatus.Ready, result.Status);
        Assert.Equal("FRP 核心上传成功。", result.Message);
        Assert.Equal("/opt/frpnexus/frpc", adapter.LastRemotePath);
        Assert.Equal("frpc-binary-placeholder", adapter.LastContent);
        Assert.Equal("/opt/frpnexus", directoryService.LastEnsuredPath);
        Assert.Equal("上传 FRP 核心", deploymentService.LastRecord?.StepName);
        Assert.Equal(FrpNexusStatus.Ready, deploymentService.LastRecord?.Status);
        Assert.DoesNotContain("SESSION_PASSWORD_PLACEHOLDER", deploymentService.LastRecord?.Description ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadConfigurationAsync_ShouldUploadTomlText()
    {
        var deploymentService = new FakeDeploymentRecordService();
        var adapter = new FakeSftpClientAdapter { ExistingFiles = { "/etc/frp/frpc.toml" } };
        var service = new RemoteFileTransferService(adapter, new FakeRemoteDirectoryService(), deploymentService, Logger.None);

        var result = await service.UploadConfigurationAsync(new RemoteConfigurationUploadRequest(
            CreateNode(),
            CreateCredential(),
            "[[proxies]]\nname = \"web\"",
            "/etc/frp/frpc.toml"));

        Assert.Equal(FrpNexusStatus.Ready, result.Status);
        Assert.Equal("TOML 配置上传成功。", result.Message);
        Assert.Contains("[[proxies]]", adapter.LastContent);
        Assert.Equal("/etc/frp/.frps.toml.frpnexus.tmp", adapter.LastRemotePath);
        Assert.Equal(
            [
                "upload:/etc/frp/.frps.toml.frpnexus.tmp",
                "exists:/etc/frp/frpc.toml",
                "delete:/etc/frp/frpc.toml",
                "rename:/etc/frp/.frps.toml.frpnexus.tmp->/etc/frp/frpc.toml"
            ],
            adapter.Operations);
        Assert.Equal("上传 TOML 配置", deploymentService.LastRecord?.StepName);
    }

    [Fact]
    public async Task UploadConfigurationAsync_ShouldNotDeleteTargetWhenTempUploadFails()
    {
        var adapter = new FakeSftpClientAdapter { UploadException = new InvalidOperationException("临时文件写入失败") };
        adapter.ExistingFiles.Add("/opt/frp/frps.toml");
        var service = new RemoteFileTransferService(
            adapter,
            new FakeRemoteDirectoryService(),
            new FakeDeploymentRecordService(),
            Logger.None);

        var result = await service.UploadConfigurationAsync(new RemoteConfigurationUploadRequest(
            CreateNode(),
            CreateCredential(),
            "bindPort = 7000",
            "/opt/frp/frps.toml"));

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Contains("临时文件写入失败", result.Message);
        Assert.Contains("/opt/frp/.frps.toml.frpnexus.tmp", result.Message);
        Assert.Equal(["upload:/opt/frp/.frps.toml.frpnexus.tmp"], adapter.Operations);
        Assert.Contains("/opt/frp/frps.toml", adapter.ExistingFiles);
    }

    [Fact]
    public async Task UploadConfigurationAsync_ShouldDeleteExistingTargetThenRenameTemp()
    {
        var adapter = new FakeSftpClientAdapter();
        adapter.ExistingFiles.Add("/opt/frp/frps.toml");
        var service = new RemoteFileTransferService(
            adapter,
            new FakeRemoteDirectoryService(),
            new FakeDeploymentRecordService(),
            Logger.None);

        var result = await service.UploadConfigurationAsync(new RemoteConfigurationUploadRequest(
            CreateNode(),
            CreateCredential(),
            "bindPort = 7000",
            "/opt/frp/frps.toml"));

        Assert.Equal(FrpNexusStatus.Ready, result.Status);
        Assert.Equal(
            [
                "upload:/opt/frp/.frps.toml.frpnexus.tmp",
                "exists:/opt/frp/frps.toml",
                "delete:/opt/frp/frps.toml",
                "rename:/opt/frp/.frps.toml.frpnexus.tmp->/opt/frp/frps.toml"
            ],
            adapter.Operations);
        Assert.Contains("/opt/frp/frps.toml", adapter.ExistingFiles);
        Assert.DoesNotContain("/opt/frp/.frps.toml.frpnexus.tmp", adapter.ExistingFiles);
    }

    [Fact]
    public async Task UploadConfigurationAsync_ShouldRenameTempWhenTargetDoesNotExist()
    {
        var adapter = new FakeSftpClientAdapter();
        var service = new RemoteFileTransferService(
            adapter,
            new FakeRemoteDirectoryService(),
            new FakeDeploymentRecordService(),
            Logger.None);

        var result = await service.UploadConfigurationAsync(new RemoteConfigurationUploadRequest(
            CreateNode(),
            CreateCredential(),
            "bindPort = 7000",
            "/opt/frp/frps.toml"));

        Assert.Equal(FrpNexusStatus.Ready, result.Status);
        Assert.Equal(
            [
                "upload:/opt/frp/.frps.toml.frpnexus.tmp",
                "exists:/opt/frp/frps.toml",
                "rename:/opt/frp/.frps.toml.frpnexus.tmp->/opt/frp/frps.toml"
            ],
            adapter.Operations);
    }

    [Fact]
    public async Task UploadConfigurationAsync_ShouldReturnRecoverableMessageWhenRenameFails()
    {
        var adapter = new FakeSftpClientAdapter { RenameException = new InvalidOperationException("重命名失败") };
        var service = new RemoteFileTransferService(
            adapter,
            new FakeRemoteDirectoryService(),
            new FakeDeploymentRecordService(),
            Logger.None);

        var result = await service.UploadConfigurationAsync(new RemoteConfigurationUploadRequest(
            CreateNode(),
            CreateCredential(),
            "bindPort = 7000",
            "/opt/frp/frps.toml"));

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Contains("重命名失败", result.Message);
        Assert.Contains("/opt/frp/.frps.toml.frpnexus.tmp", result.Message);
        Assert.DoesNotContain("Renci.SshNet", result.Message, StringComparison.Ordinal);
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
            new FakeRemoteDirectoryService(),
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
            new FakeRemoteDirectoryService(),
            deploymentService,
            Logger.None);

        var result = await service.UploadConfigurationAsync(new RemoteConfigurationUploadRequest(
            CreateNode(),
            CreateCredential(),
            "[[proxies]]",
            "/etc/frp/frpc.toml"));

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Contains("SFTP 上传失败：权限不足", result.Message);
        Assert.Contains("/etc/frp/.frps.toml.frpnexus.tmp", result.Message);
        Assert.DoesNotContain("SESSION_PASSWORD_PLACEHOLDER", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SESSION_PASSWORD_PLACEHOLDER", deploymentService.LastRecord?.Description ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadConfigurationAsync_ShouldMapGenericSftpFailureOnSystemPathToPermissionGuidance()
    {
        var deploymentService = new FakeDeploymentRecordService();
        var service = new RemoteFileTransferService(
            new FakeSftpClientAdapter(new SftpException(Renci.SshNet.Sftp.StatusCode.Failure)),
            new FakeRemoteDirectoryService(),
            deploymentService,
            Logger.None);

        var result = await service.UploadConfigurationAsync(new RemoteConfigurationUploadRequest(
            CreateNode(),
            CreateCredential(),
            "bindPort = 7000",
            "/etc/frp/frps.toml"));

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Contains("目标文件无法写入", result.Message);
        Assert.Contains("root SSH", result.Message);
        Assert.Contains("/home/<user>/frp/frps.toml", result.Message);
        Assert.DoesNotContain("Renci.SshNet", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Failure", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SESSION_PASSWORD_PLACEHOLDER", deploymentService.LastRecord?.Description ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("目标文件无法写入", deploymentService.LastRecord?.Description ?? string.Empty);
    }

    [Fact]
    public async Task UploadConfigurationAsync_ShouldMapGenericSftpFailureOnUserPathToWritableDirectoryGuidance()
    {
        var service = new RemoteFileTransferService(
            new FakeSftpClientAdapter(new SftpException(Renci.SshNet.Sftp.StatusCode.Failure)),
            new FakeRemoteDirectoryService(),
            new FakeDeploymentRecordService(),
            Logger.None);

        var result = await service.UploadConfigurationAsync(new RemoteConfigurationUploadRequest(
            CreateNode(),
            CreateCredential(),
            "bindPort = 7000",
            "/home/deploy/frp/frps.toml"));

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Contains("目标文件无法写入", result.Message);
        Assert.Contains("当前 SSH 用户有写入权限", result.Message);
        Assert.DoesNotContain("root SSH", result.Message);
        Assert.DoesNotContain("Failure", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadFrpBinaryAsync_ShouldReturnDirectoryFailureBeforeUpload()
    {
        var localPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(localPath, "frpc-binary-placeholder");
        var deploymentService = new FakeDeploymentRecordService();
        var adapter = new FakeSftpClientAdapter();
        var service = new RemoteFileTransferService(
            adapter,
            new FakeRemoteDirectoryService(FrpNexusStatus.Error, "远程目录准备失败：权限不足，请选择用户可写目录，例如 /home/<user>/frp。"),
            deploymentService,
            Logger.None);

        var result = await service.UploadFrpBinaryAsync(new RemoteFileUploadRequest(
            CreateNode(),
            CreateCredential(),
            localPath,
            "/opt/frp/frpc"));

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Contains("权限不足", result.Message, StringComparison.Ordinal);
        Assert.Null(adapter.LastRemotePath);
        Assert.Equal(FrpNexusStatus.Error, deploymentService.LastRecord?.Status);
    }

    [Fact]
    public async Task DeleteRemoteFilesAsync_ShouldDeleteBothExistingFiles()
    {
        var adapter = new FakeSftpClientAdapter();
        adapter.ExistingFiles.Add("/opt/frp/frps");
        adapter.ExistingFiles.Add("/opt/frp/frps.toml");
        var deploymentService = new FakeDeploymentRecordService();
        var service = new RemoteFileTransferService(adapter, new FakeRemoteDirectoryService(), deploymentService, Logger.None);

        var result = await service.DeleteRemoteFilesAsync(new RemoteFileDeleteRequest(
            CreateNode(),
            CreateCredential(),
            ["/opt/frp/frps", "/opt/frp/frps.toml"]));

        Assert.Equal(FrpNexusStatus.Ready, result.Status);
        Assert.Equal(["/opt/frp/frps", "/opt/frp/frps.toml"], result.DeletedPaths);
        Assert.Empty(result.MissingPaths);
        Assert.DoesNotContain("/opt/frp/frps", adapter.ExistingFiles);
        Assert.DoesNotContain("/opt/frp/frps.toml", adapter.ExistingFiles);
        Assert.Equal(
            [
                "exists:/opt/frp/frps",
                "delete:/opt/frp/frps",
                "exists:/opt/frp/frps.toml",
                "delete:/opt/frp/frps.toml"
            ],
            adapter.Operations);
        Assert.Equal("清理远程 FRP 文件", deploymentService.LastRecord?.StepName);
        Assert.Equal(FrpNexusStatus.Ready, deploymentService.LastRecord?.Status);
    }

    [Fact]
    public async Task DeleteRemoteFilesAsync_ShouldSkipMissingFilesAsSuccess()
    {
        var adapter = new FakeSftpClientAdapter();
        adapter.ExistingFiles.Add("/opt/frp/frps");
        var service = new RemoteFileTransferService(
            adapter,
            new FakeRemoteDirectoryService(),
            new FakeDeploymentRecordService(),
            Logger.None);

        var result = await service.DeleteRemoteFilesAsync(new RemoteFileDeleteRequest(
            CreateNode(),
            CreateCredential(),
            ["/opt/frp/frps", "/opt/frp/frps.toml"]));

        Assert.Equal(FrpNexusStatus.Ready, result.Status);
        Assert.Equal(["/opt/frp/frps"], result.DeletedPaths);
        Assert.Equal(["/opt/frp/frps.toml"], result.MissingPaths);
        Assert.Contains("部分文件原本不存在", result.Message);
        Assert.Equal(
            [
                "exists:/opt/frp/frps",
                "delete:/opt/frp/frps",
                "exists:/opt/frp/frps.toml"
            ],
            adapter.Operations);
    }

    [Fact]
    public async Task DeleteRemoteFilesAsync_ShouldRejectEmptyRemotePath()
    {
        var adapter = new FakeSftpClientAdapter();
        var service = new RemoteFileTransferService(
            adapter,
            new FakeRemoteDirectoryService(),
            new FakeDeploymentRecordService(),
            Logger.None);

        var result = await service.DeleteRemoteFilesAsync(new RemoteFileDeleteRequest(
            CreateNode(),
            CreateCredential(),
            [""]));

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Equal("远程文件路径不能为空。", result.Message);
        Assert.Empty(adapter.Operations);
    }

    [Theory]
    [InlineData("relative/frps")]
    [InlineData("C:\\frp\\frps")]
    public async Task DeleteRemoteFilesAsync_ShouldRejectInvalidRemotePath(string remotePath)
    {
        var adapter = new FakeSftpClientAdapter();
        var service = new RemoteFileTransferService(
            adapter,
            new FakeRemoteDirectoryService(),
            new FakeDeploymentRecordService(),
            Logger.None);

        var result = await service.DeleteRemoteFilesAsync(new RemoteFileDeleteRequest(
            CreateNode(),
            CreateCredential(),
            [remotePath]));

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Equal("远程路径必须是 Linux 绝对路径。", result.Message);
        Assert.Empty(adapter.Operations);
    }

    [Fact]
    public async Task DeleteRemoteFilesAsync_ShouldReturnChineseFailureAndHideSecret()
    {
        var deploymentService = new FakeDeploymentRecordService();
        var adapter = new FakeSftpClientAdapter { DeleteException = new SftpException(Renci.SshNet.Sftp.StatusCode.Failure) };
        adapter.ExistingFiles.Add("/etc/frp/frps");
        var service = new RemoteFileTransferService(adapter, new FakeRemoteDirectoryService(), deploymentService, Logger.None);

        var result = await service.DeleteRemoteFilesAsync(new RemoteFileDeleteRequest(
            CreateNode(),
            CreateCredential(),
            ["/etc/frp/frps", "/etc/frp/frps.toml"]));

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Contains("SFTP 删除失败", result.Message);
        Assert.Contains("目标文件无法删除", result.Message);
        Assert.Contains("root SSH", result.Message);
        Assert.DoesNotContain("Renci.SshNet", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Failure", result.Message, StringComparison.Ordinal);
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

        public HashSet<string> ExistingFiles { get; } = new(StringComparer.Ordinal);

        public List<string> Operations { get; } = [];

        public Exception? UploadException { get; init; } = exception;

        public Exception? DeleteException { get; init; }

        public Exception? RenameException { get; init; }

        public Exception? FileExistsException { get; init; }

        public Task<IReadOnlyList<RemoteDirectoryEntry>> ListDirectoriesAsync(
            NodeProfile node,
            SshCredentialReference credential,
            string remotePath,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RemoteDirectoryEntry>>([]);
        }

        public Task CreateDirectoryAsync(
            NodeProfile node,
            SshCredentialReference credential,
            string remotePath,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task EnsureDirectoryAsync(
            NodeProfile node,
            SshCredentialReference credential,
            string remotePath,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public async Task UploadFileAsync(
            NodeProfile node,
            SshCredentialReference credential,
            Stream content,
            string remotePath,
            CancellationToken cancellationToken = default)
        {
            Operations.Add($"upload:{remotePath}");
            if (UploadException is not null)
            {
                throw UploadException;
            }

            using var reader = new StreamReader(content, Encoding.UTF8);
            LastContent = await reader.ReadToEndAsync(cancellationToken);
            LastRemotePath = remotePath;
            ExistingFiles.Add(remotePath);
        }

        public Task<bool> FileExistsAsync(
            NodeProfile node,
            SshCredentialReference credential,
            string remotePath,
            CancellationToken cancellationToken = default)
        {
            Operations.Add($"exists:{remotePath}");
            if (FileExistsException is not null)
            {
                throw FileExistsException;
            }

            return Task.FromResult(ExistingFiles.Contains(remotePath));
        }

        public Task DeleteFileAsync(
            NodeProfile node,
            SshCredentialReference credential,
            string remotePath,
            CancellationToken cancellationToken = default)
        {
            Operations.Add($"delete:{remotePath}");
            if (DeleteException is not null)
            {
                throw DeleteException;
            }

            ExistingFiles.Remove(remotePath);
            return Task.CompletedTask;
        }

        public Task RenameFileAsync(
            NodeProfile node,
            SshCredentialReference credential,
            string sourcePath,
            string destinationPath,
            CancellationToken cancellationToken = default)
        {
            Operations.Add($"rename:{sourcePath}->{destinationPath}");
            if (RenameException is not null)
            {
                throw RenameException;
            }

            ExistingFiles.Remove(sourcePath);
            ExistingFiles.Add(destinationPath);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRemoteDirectoryService(
        FrpNexusStatus status = FrpNexusStatus.Ready,
        string message = "远程目录已准备好。") : IRemoteDirectoryService
    {
        public string? LastEnsuredPath { get; private set; }

        public Task<RemoteDirectoryListResult> ListDirectoriesAsync(
            RemoteDirectoryListRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RemoteDirectoryListResult(
                request.RemotePath,
                FrpNexusStatus.Ready,
                "远程目录读取成功。",
                []));
        }

        public Task<RemoteDirectoryOperationResult> CreateDirectoryAsync(
            RemoteDirectoryCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RemoteDirectoryOperationResult(
                request.RemotePath,
                FrpNexusStatus.Ready,
                "远程目录创建成功。"));
        }

        public Task<RemoteDirectoryOperationResult> EnsureDirectoryAsync(
            RemoteDirectoryEnsureRequest request,
            CancellationToken cancellationToken = default)
        {
            LastEnsuredPath = request.RemotePath;
            return Task.FromResult(new RemoteDirectoryOperationResult(
                request.RemotePath,
                status,
                message));
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
