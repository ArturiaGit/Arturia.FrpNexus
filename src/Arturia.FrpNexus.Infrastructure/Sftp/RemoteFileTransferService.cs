using System.Text;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Serilog;

namespace Arturia.FrpNexus.Infrastructure.Sftp;

public sealed class RemoteFileTransferService(
    ISftpClientAdapter sftpClientAdapter,
    IDeploymentRecordService deploymentRecordService,
    ILogger logger) : IRemoteFileTransferService
{
    public async Task<RemoteFileTransferResult> UploadFrpBinaryAsync(
        RemoteFileUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.LocalPath))
        {
            return await RecordFailureAsync(
                "上传 FRP 核心",
                request.Node.Name,
                request.RemotePath,
                "本地 FRP 文件不存在。",
                cancellationToken);
        }

        if (!IsValidRemotePath(request.RemotePath))
        {
            return await RecordFailureAsync(
                "上传 FRP 核心",
                request.Node.Name,
                request.RemotePath,
                "远程路径必须是 Linux 绝对路径。",
                cancellationToken);
        }

        await using var stream = File.OpenRead(request.LocalPath);
        return await UploadAsync(
            "上传 FRP 核心",
            request.Node,
            request.Credential,
            stream,
            request.RemotePath,
            "FRP 核心上传成功。",
            cancellationToken);
    }

    public async Task<RemoteFileTransferResult> UploadConfigurationAsync(
        RemoteConfigurationUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TomlContent))
        {
            return await RecordFailureAsync(
                "上传 TOML 配置",
                request.Node.Name,
                request.RemotePath,
                "TOML 配置内容不能为空。",
                cancellationToken);
        }

        if (!IsValidRemotePath(request.RemotePath))
        {
            return await RecordFailureAsync(
                "上传 TOML 配置",
                request.Node.Name,
                request.RemotePath,
                "远程路径必须是 Linux 绝对路径。",
                cancellationToken);
        }

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(request.TomlContent));
        return await UploadAsync(
            "上传 TOML 配置",
            request.Node,
            request.Credential,
            stream,
            request.RemotePath,
            "TOML 配置上传成功。",
            cancellationToken);
    }

    private async Task<RemoteFileTransferResult> UploadAsync(
        string stepName,
        NodeProfile node,
        SshCredentialReference credential,
        Stream content,
        string remotePath,
        string successMessage,
        CancellationToken cancellationToken)
    {
        var completedAt = DateTimeOffset.UtcNow;

        try
        {
            await sftpClientAdapter.UploadFileAsync(node, credential, content, remotePath, cancellationToken);

            await deploymentRecordService.SaveDeploymentRecordAsync(
                new DeploymentRecord(stepName, node.Name, $"{successMessage} 目标：{remotePath}", FrpNexusStatus.Ready, completedAt),
                cancellationToken);

            logger.Information(
                "SFTP upload succeeded for node {NodeName} to {RemotePath} using {AuthenticationMode}",
                node.Name,
                remotePath,
                credential.AuthenticationMode);

            return new RemoteFileTransferResult(
                node.Name,
                remotePath,
                FrpNexusStatus.Ready,
                completedAt,
                successMessage);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            await deploymentRecordService.SaveDeploymentRecordAsync(
                new DeploymentRecord(stepName, node.Name, $"SFTP 上传失败：{SanitizeMessage(exception.Message)}", FrpNexusStatus.Error, completedAt),
                cancellationToken);

            logger.Warning(
                exception,
                "SFTP upload failed for node {NodeName} to {RemotePath} using {AuthenticationMode}",
                node.Name,
                remotePath,
                credential.AuthenticationMode);

            return new RemoteFileTransferResult(
                node.Name,
                remotePath,
                FrpNexusStatus.Error,
                completedAt,
                $"SFTP 上传失败：{SanitizeMessage(exception.Message)}");
        }
    }

    private async Task<RemoteFileTransferResult> RecordFailureAsync(
        string stepName,
        string nodeName,
        string remotePath,
        string message,
        CancellationToken cancellationToken)
    {
        var completedAt = DateTimeOffset.UtcNow;
        await deploymentRecordService.SaveDeploymentRecordAsync(
            new DeploymentRecord(stepName, nodeName, message, FrpNexusStatus.Error, completedAt),
            cancellationToken);

        return new RemoteFileTransferResult(
            nodeName,
            remotePath,
            FrpNexusStatus.Error,
            completedAt,
            message);
    }

    private static bool IsValidRemotePath(string remotePath)
    {
        return !string.IsNullOrWhiteSpace(remotePath)
            && remotePath.StartsWith("/", StringComparison.Ordinal)
            && !remotePath.Contains("\0", StringComparison.Ordinal);
    }

    private static string SanitizeMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message)
            ? "未返回详细错误。"
            : message.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();
    }
}
