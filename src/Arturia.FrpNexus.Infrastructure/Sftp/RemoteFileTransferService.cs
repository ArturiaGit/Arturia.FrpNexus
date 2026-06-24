using System.Text;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;
using Serilog;

namespace Arturia.FrpNexus.Infrastructure.Sftp;

public sealed class RemoteFileTransferService(
    ISftpClientAdapter sftpClientAdapter,
    IRemoteDirectoryService remoteDirectoryService,
    IDeploymentRecordService deploymentRecordService,
    ILogger logger) : IRemoteFileTransferService
{
    public async Task<RemoteFilePresenceResult> CheckRemoteFilesAsync(
        RemoteFilePresenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var completedAt = DateTimeOffset.UtcNow;
        var remotePaths = request.RemotePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim().Replace('\\', '/'))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (remotePaths.Length == 0)
        {
            return new RemoteFilePresenceResult(
                request.Node.Name,
                [],
                FrpNexusStatus.Error,
                completedAt,
                "远程文件路径不能为空。");
        }

        var invalidPath = remotePaths.FirstOrDefault(path => !IsValidRemotePath(path));
        if (!string.IsNullOrWhiteSpace(invalidPath))
        {
            return new RemoteFilePresenceResult(
                request.Node.Name,
                [],
                FrpNexusStatus.Error,
                completedAt,
                "远程路径必须是 Linux 绝对路径。");
        }

        List<RemoteFilePresenceEntry> entries = [];

        try
        {
            foreach (var remotePath in remotePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var exists = await sftpClientAdapter.FileExistsAsync(
                    request.Node,
                    request.Credential,
                    remotePath,
                    cancellationToken);
                entries.Add(new RemoteFilePresenceEntry(remotePath, exists));
            }

            var missingCount = entries.Count(entry => !entry.Exists);
            var message = missingCount == 0
                ? "远程 frps 和 frps.toml 已就绪。"
                : missingCount == entries.Count
                    ? "远程 frps 和 frps.toml 尚未上传。"
                    : "远程部署文件不完整，需要补齐缺失文件。";

            return new RemoteFilePresenceResult(
                request.Node.Name,
                entries,
                missingCount == 0 ? FrpNexusStatus.Ready : FrpNexusStatus.Warning,
                completedAt,
                message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.Warning(
                exception,
                "SFTP remote FRP files presence check failed for node {NodeName} using {AuthenticationMode}",
                request.Node.Name,
                request.Credential.AuthenticationMode);

            return new RemoteFilePresenceResult(
                request.Node.Name,
                entries,
                FrpNexusStatus.Error,
                completedAt,
                ToPresenceFailureMessage(exception));
        }
    }

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
        return await UploadConfigurationWithSafeReplaceAsync(
            "上传 TOML 配置",
            request.Node,
            request.Credential,
            stream,
            request.RemotePath,
            "TOML 配置上传成功。",
            cancellationToken);
    }

    public async Task<RemoteFileDeleteResult> DeleteRemoteFilesAsync(
        RemoteFileDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var completedAt = DateTimeOffset.UtcNow;
        var remotePaths = request.RemotePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim().Replace('\\', '/'))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (remotePaths.Length == 0)
        {
            return await RecordDeleteFailureAsync(
                request.Node.Name,
                [],
                [],
                "远程文件路径不能为空。",
                completedAt,
                cancellationToken);
        }

        var invalidPath = remotePaths.FirstOrDefault(path => !IsValidRemotePath(path));
        if (!string.IsNullOrWhiteSpace(invalidPath))
        {
            return await RecordDeleteFailureAsync(
                request.Node.Name,
                [],
                [],
                "远程路径必须是 Linux 绝对路径。",
                completedAt,
                cancellationToken);
        }

        List<string> deletedPaths = [];
        List<string> missingPaths = [];

        try
        {
            foreach (var remotePath in remotePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!await sftpClientAdapter.FileExistsAsync(request.Node, request.Credential, remotePath, cancellationToken))
                {
                    missingPaths.Add(remotePath);
                    continue;
                }

                await sftpClientAdapter.DeleteFileAsync(request.Node, request.Credential, remotePath, cancellationToken);
                deletedPaths.Add(remotePath);
            }

            var message = missingPaths.Count == 0
                ? "已清理远程 frps 核心和 frps.toml。"
                : "远程文件已清理，部分文件原本不存在。";

            await deploymentRecordService.SaveDeploymentRecordAsync(
                new DeploymentRecord("清理远程 FRP 文件", request.Node.Name, message, FrpNexusStatus.Ready, completedAt),
                cancellationToken);

            logger.Information(
                "SFTP remote FRP files delete succeeded for node {NodeName} using {AuthenticationMode}",
                request.Node.Name,
                request.Credential.AuthenticationMode);

            return new RemoteFileDeleteResult(
                request.Node.Name,
                deletedPaths,
                missingPaths,
                FrpNexusStatus.Ready,
                completedAt,
                message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failedPath = remotePaths.FirstOrDefault(path => !deletedPaths.Contains(path, StringComparer.Ordinal)
                && !missingPaths.Contains(path, StringComparer.Ordinal)) ?? remotePaths[0];
            var failureMessage = ToDeleteFailureMessage(exception, failedPath);
            await deploymentRecordService.SaveDeploymentRecordAsync(
                new DeploymentRecord("清理远程 FRP 文件", request.Node.Name, failureMessage, FrpNexusStatus.Error, completedAt),
                cancellationToken);

            logger.Warning(
                exception,
                "SFTP remote FRP files delete failed for node {NodeName} using {AuthenticationMode}",
                request.Node.Name,
                request.Credential.AuthenticationMode);

            return new RemoteFileDeleteResult(
                request.Node.Name,
                deletedPaths,
                missingPaths,
                FrpNexusStatus.Error,
                completedAt,
                failureMessage);
        }
    }

    private async Task<RemoteFileTransferResult> UploadConfigurationWithSafeReplaceAsync(
        string stepName,
        NodeProfile node,
        SshCredentialReference credential,
        Stream content,
        string remotePath,
        string successMessage,
        CancellationToken cancellationToken)
    {
        var completedAt = DateTimeOffset.UtcNow;
        var parentDirectory = GetParentDirectory(remotePath);
        var tempPath = CombineRemotePath(parentDirectory, ".frps.toml.frpnexus.tmp");

        try
        {
            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                var directoryResult = await remoteDirectoryService.EnsureDirectoryAsync(
                    new RemoteDirectoryEnsureRequest(node, credential, parentDirectory),
                    cancellationToken);

                if (directoryResult.Status == FrpNexusStatus.Error)
                {
                    await deploymentRecordService.SaveDeploymentRecordAsync(
                        new DeploymentRecord(stepName, node.Name, directoryResult.Message, FrpNexusStatus.Error, completedAt),
                        cancellationToken);

                    return new RemoteFileTransferResult(
                        node.Name,
                        remotePath,
                        FrpNexusStatus.Error,
                        completedAt,
                        directoryResult.Message);
                }
            }

            await sftpClientAdapter.UploadFileAsync(node, credential, content, tempPath, cancellationToken);

            if (await sftpClientAdapter.FileExistsAsync(node, credential, remotePath, cancellationToken))
            {
                await sftpClientAdapter.DeleteFileAsync(node, credential, remotePath, cancellationToken);
            }

            await sftpClientAdapter.RenameFileAsync(node, credential, tempPath, remotePath, cancellationToken);

            await deploymentRecordService.SaveDeploymentRecordAsync(
                new DeploymentRecord(stepName, node.Name, $"{successMessage} 目标：{remotePath}", FrpNexusStatus.Ready, completedAt),
                cancellationToken);

            logger.Information(
                "SFTP configuration upload succeeded for node {NodeName} to {RemotePath} using {AuthenticationMode}",
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
            var failureMessage = ToConfigurationReplaceFailureMessage(exception, remotePath, tempPath);
            await deploymentRecordService.SaveDeploymentRecordAsync(
                new DeploymentRecord(stepName, node.Name, failureMessage, FrpNexusStatus.Error, completedAt),
                cancellationToken);

            logger.Warning(
                exception,
                "SFTP configuration upload failed for node {NodeName} to {RemotePath} using {AuthenticationMode}",
                node.Name,
                remotePath,
                credential.AuthenticationMode);

            return new RemoteFileTransferResult(
                node.Name,
                remotePath,
                FrpNexusStatus.Error,
                completedAt,
                failureMessage);
        }
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
            var parentDirectory = GetParentDirectory(remotePath);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                var directoryResult = await remoteDirectoryService.EnsureDirectoryAsync(
                    new RemoteDirectoryEnsureRequest(node, credential, parentDirectory),
                    cancellationToken);

                if (directoryResult.Status == FrpNexusStatus.Error)
                {
                    await deploymentRecordService.SaveDeploymentRecordAsync(
                        new DeploymentRecord(stepName, node.Name, directoryResult.Message, FrpNexusStatus.Error, completedAt),
                        cancellationToken);

                    return new RemoteFileTransferResult(
                        node.Name,
                        remotePath,
                        FrpNexusStatus.Error,
                        completedAt,
                        directoryResult.Message);
                }
            }

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
            var failureMessage = ToUploadFailureMessage(exception, remotePath);
            await deploymentRecordService.SaveDeploymentRecordAsync(
                new DeploymentRecord(stepName, node.Name, failureMessage, FrpNexusStatus.Error, completedAt),
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
                failureMessage);
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

    private async Task<RemoteFileDeleteResult> RecordDeleteFailureAsync(
        string nodeName,
        IReadOnlyList<string> deletedPaths,
        IReadOnlyList<string> missingPaths,
        string message,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        await deploymentRecordService.SaveDeploymentRecordAsync(
            new DeploymentRecord("清理远程 FRP 文件", nodeName, message, FrpNexusStatus.Error, completedAt),
            cancellationToken);

        return new RemoteFileDeleteResult(
            nodeName,
            deletedPaths,
            missingPaths,
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

    private static string GetParentDirectory(string remotePath)
    {
        var normalized = remotePath.Trim().Replace('\\', '/');
        var lastSlash = normalized.LastIndexOf('/');
        if (lastSlash <= 0)
        {
            return "/";
        }

        return normalized[..lastSlash];
    }

    private static string CombineRemotePath(string directory, string fileName)
    {
        var normalizedDirectory = string.IsNullOrWhiteSpace(directory)
            ? "/"
            : directory.Trim().Replace('\\', '/').TrimEnd('/');

        return string.Equals(normalizedDirectory, "/", StringComparison.Ordinal)
            ? $"/{fileName}"
            : $"{normalizedDirectory}/{fileName}";
    }

    private static string ToConfigurationReplaceFailureMessage(Exception exception, string remotePath, string tempPath)
    {
        var baseMessage = ToUploadFailureMessage(exception, remotePath);
        if (baseMessage.Contains("目标文件无法写入", StringComparison.Ordinal)
            || baseMessage.Contains("远程目录不存在", StringComparison.Ordinal)
            || baseMessage.Contains("连接失败", StringComparison.Ordinal))
        {
            return baseMessage;
        }

        return $"{baseMessage} 已上传的临时文件可能保留在 {tempPath}，请确认远程目录后手动处理。";
    }

    private static string ToUploadFailureMessage(Exception exception, string remotePath)
    {
        return exception switch
        {
            TimeoutException => "SFTP 上传超时：远程节点响应过慢，请检查网络和服务器状态。",
            SftpPermissionDeniedException => BuildPermissionFailureMessage(remotePath),
            SftpPathNotFoundException => "SFTP 上传失败：远程目录不存在，或上级目录不可访问。",
            SshConnectionException => "SFTP 上传失败：连接失败，请检查节点连接状态。",
            SftpException sftpException when IsGenericSftpFailure(sftpException) => BuildPermissionFailureMessage(remotePath),
            _ => $"SFTP 上传失败：{SanitizeMessage(exception.Message)}"
        };
    }

    private static string ToDeleteFailureMessage(Exception exception, string remotePath)
    {
        return exception switch
        {
            TimeoutException => "SFTP 删除超时：远程节点响应过慢，请检查网络和服务器状态。",
            SftpPermissionDeniedException => BuildDeletePermissionFailureMessage(remotePath),
            SftpPathNotFoundException => "SFTP 删除失败：远程目录不存在，或上级目录不可访问。",
            SshConnectionException => "SFTP 删除失败：连接失败，请检查节点连接状态。",
            SftpException sftpException when IsGenericSftpFailure(sftpException) => BuildDeletePermissionFailureMessage(remotePath),
            _ => $"SFTP 删除失败：{SanitizeMessage(exception.Message)}"
        };
    }

    private static string ToPresenceFailureMessage(Exception exception)
    {
        return exception switch
        {
            TimeoutException => "SFTP 检查超时：远程节点响应过慢，请检查网络和服务器状态。",
            SftpPermissionDeniedException => "SFTP 检查失败：远程目录权限不足，无法确认部署文件状态。",
            SftpPathNotFoundException => "SFTP 检查失败：远程目录不存在，或上级目录不可访问。",
            SshConnectionException => "SFTP 检查失败：连接失败，请检查节点连接状态。",
            SftpException sftpException when IsGenericSftpFailure(sftpException) => "SFTP 检查失败：远程目录不可访问，请确认路径和权限。",
            _ => $"SFTP 检查失败：{SanitizeMessage(exception.Message)}"
        };
    }

    private static bool IsGenericSftpFailure(SftpException exception)
    {
        return exception.StatusCode == StatusCode.Failure
            || string.Equals(exception.Message, "Failure", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildPermissionFailureMessage(string remotePath)
    {
        if (IsSystemPath(remotePath))
        {
            return "SFTP 上传失败：目标文件无法写入，通常是系统目录或文件权限不足。请使用 root SSH 会话上传，或把节点配置路径改为当前用户可写目录，例如 /home/<user>/frp/frps.toml。";
        }

        return "SFTP 上传失败：目标文件无法写入，通常是目录或文件权限不足。请确认远程目录存在且当前 SSH 用户有写入权限，或改用 /home/<user>/frp/frps.toml 这类用户可写路径。";
    }

    private static string BuildDeletePermissionFailureMessage(string remotePath)
    {
        if (IsSystemPath(remotePath))
        {
            return "SFTP 删除失败：目标文件无法删除，通常是系统目录或文件权限不足。请使用 root SSH 会话清理，或改用当前用户可写的远程 FRP 目录。";
        }

        return "SFTP 删除失败：目标文件无法删除，通常是目录或文件权限不足。请确认当前 SSH 用户对远程 FRP 目录有写入权限。";
    }

    private static bool IsSystemPath(string remotePath)
    {
        var normalized = remotePath.Trim().Replace('\\', '/');
        return normalized.StartsWith("/etc/", StringComparison.Ordinal)
            || normalized.StartsWith("/usr/", StringComparison.Ordinal)
            || normalized.StartsWith("/var/", StringComparison.Ordinal);
    }

    private static string SanitizeMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message)
            ? "未返回详细错误。"
            : message.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();
    }
}
