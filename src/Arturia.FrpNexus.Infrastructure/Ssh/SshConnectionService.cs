using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Serilog;

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
                $"SSH 连接测试失败：{SanitizeMessage(exception.Message)}");
        }
    }

    private static string SanitizeMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message)
            ? "未返回详细错误。"
            : message.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();
    }
}
