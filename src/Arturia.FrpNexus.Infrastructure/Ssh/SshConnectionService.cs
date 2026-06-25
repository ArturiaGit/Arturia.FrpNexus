using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Logging;
using Arturia.FrpNexus.Core.Models;
using Renci.SshNet.Common;
using Serilog;
using System.Net.Sockets;

namespace Arturia.FrpNexus.Infrastructure.Ssh;

public sealed class SshConnectionService(
    ISshClientAdapter sshClientAdapter,
    INodeManagementService nodeManagementService,
    ILogger logger) : ISshConnectionService
{
    public async Task<SshConnectionTestResult> TestConnectionAsync(
        SshConnectionTestRequest request,
        CancellationToken cancellationToken = default)
    {
        var testedAt = DateTimeOffset.UtcNow;

        try
        {
            await sshClientAdapter.ConnectAsync(request.Node, request.Credential, cancellationToken);
            await nodeManagementService.UpdateConnectionTestResultAsync(
                request.Node.Name,
                FrpNexusStatus.Online,
                testedAt,
                cancellationToken);

            logger.Information(
                "SSH connection test succeeded for node {NodeName} at {Host}:{Port} using {AuthenticationMode}",
                request.Node.Name,
                request.Node.Host,
                request.Node.SshPort,
                request.Credential.AuthenticationMode);

            return new SshConnectionTestResult(
                request.Node.Name,
                FrpNexusStatus.Online,
                testedAt,
                "SSH 连接测试成功。");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            await nodeManagementService.UpdateConnectionTestResultAsync(
                request.Node.Name,
                FrpNexusStatus.Error,
                testedAt,
                cancellationToken);

            logger.Warning(
                exception,
                "SSH connection test failed for node {NodeName} at {Host}:{Port} using {AuthenticationMode}",
                request.Node.Name,
                request.Node.Host,
                request.Node.SshPort,
                request.Credential.AuthenticationMode);

            return new SshConnectionTestResult(
                request.Node.Name,
                FrpNexusStatus.Error,
                testedAt,
                GetFailureMessage(exception));
        }
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
            _ => $"SSH 连接测试失败：{SanitizeMessage(exception.Message)}",
        };
    }

    private static string SanitizeMessage(string message)
    {
        var sanitized = string.IsNullOrWhiteSpace(message)
            ? "未返回详细错误。"
            : LogTextSanitizer
                .RedactSecrets(message.Replace(Environment.NewLine, " ", StringComparison.Ordinal))
                .Trim();

        return sanitized.Length > 160
            ? sanitized[..160]
            : sanitized;
    }
}
