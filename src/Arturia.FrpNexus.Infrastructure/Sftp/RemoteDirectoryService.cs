using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Renci.SshNet.Common;
using Serilog;

namespace Arturia.FrpNexus.Infrastructure.Sftp;

public sealed class RemoteDirectoryService(
    ISftpClientAdapter sftpClientAdapter,
    ILogger logger) : IRemoteDirectoryService
{
    public async Task<RemoteDirectoryListResult> ListDirectoriesAsync(
        RemoteDirectoryListRequest request,
        CancellationToken cancellationToken = default)
    {
        var remotePath = NormalizeDirectoryPath(request.RemotePath);
        if (!IsValidRemotePath(remotePath))
        {
            return new RemoteDirectoryListResult(
                remotePath,
                FrpNexusStatus.Error,
                "远程目录必须是 Linux 绝对路径。",
                []);
        }

        try
        {
            var directories = await sftpClientAdapter.ListDirectoriesAsync(
                request.Node,
                request.Credential,
                remotePath,
                cancellationToken);

            return new RemoteDirectoryListResult(
                remotePath,
                FrpNexusStatus.Ready,
                "远程目录读取成功。",
                directories);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.Warning(
                exception,
                "SFTP remote directory list failed for node {NodeName} at {RemotePath} using {AuthenticationMode}",
                request.Node.Name,
                remotePath,
                request.Credential.AuthenticationMode);

            return new RemoteDirectoryListResult(
                remotePath,
                FrpNexusStatus.Error,
                ToUserMessage("远程目录读取失败", exception),
                []);
        }
    }

    public async Task<RemoteDirectoryOperationResult> CreateDirectoryAsync(
        RemoteDirectoryCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteDirectoryOperationAsync(
            request.Node,
            request.Credential,
            request.RemotePath,
            "远程目录创建成功。",
            "远程目录创建失败",
            sftpClientAdapter.CreateDirectoryAsync,
            cancellationToken);
    }

    public async Task<RemoteDirectoryOperationResult> EnsureDirectoryAsync(
        RemoteDirectoryEnsureRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteDirectoryOperationAsync(
            request.Node,
            request.Credential,
            request.RemotePath,
            "远程目录已准备好。",
            "远程目录准备失败",
            sftpClientAdapter.EnsureDirectoryAsync,
            cancellationToken);
    }

    private async Task<RemoteDirectoryOperationResult> ExecuteDirectoryOperationAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string remotePath,
        string successMessage,
        string failureTitle,
        Func<NodeProfile, SshCredentialReference, string, CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizeDirectoryPath(remotePath);
        if (!IsValidRemotePath(normalizedPath))
        {
            return new RemoteDirectoryOperationResult(
                normalizedPath,
                FrpNexusStatus.Error,
                "远程目录必须是 Linux 绝对路径。");
        }

        try
        {
            await operation(node, credential, normalizedPath, cancellationToken);

            return new RemoteDirectoryOperationResult(
                normalizedPath,
                FrpNexusStatus.Ready,
                successMessage);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.Warning(
                exception,
                "SFTP remote directory operation failed for node {NodeName} at {RemotePath} using {AuthenticationMode}",
                node.Name,
                normalizedPath,
                credential.AuthenticationMode);

            return new RemoteDirectoryOperationResult(
                normalizedPath,
                FrpNexusStatus.Error,
                ToUserMessage(failureTitle, exception));
        }
    }

    private static string NormalizeDirectoryPath(string remotePath)
    {
        if (string.IsNullOrWhiteSpace(remotePath))
        {
            return string.Empty;
        }

        var normalized = remotePath.Trim().Replace('\\', '/');
        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        return normalized.Length > 1 ? normalized.TrimEnd('/') : normalized;
    }

    private static bool IsValidRemotePath(string remotePath)
    {
        return !string.IsNullOrWhiteSpace(remotePath)
            && remotePath.StartsWith("/", StringComparison.Ordinal)
            && !remotePath.Contains('\0', StringComparison.Ordinal);
    }

    private static string ToUserMessage(string title, Exception exception)
    {
        return exception switch
        {
            SftpPermissionDeniedException => $"{title}：权限不足，请选择用户可写目录，例如 /home/<user>/frp。",
            SftpPathNotFoundException => $"{title}：目录不存在，或上级目录不可访问。",
            SshConnectionException => $"{title}：SFTP 连接失败，请检查节点连接状态。",
            _ => $"{title}：{SanitizeMessage(exception.Message)}"
        };
    }

    private static string SanitizeMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message)
            ? "未返回详细错误。"
            : message.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();
    }
}
