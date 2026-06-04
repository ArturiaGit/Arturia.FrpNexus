using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Serilog;

namespace Arturia.FrpNexus.Infrastructure.Runtime;

public sealed class RemoteRuntimeService(
    IRemoteCommandAdapter remoteCommandAdapter,
    IRuntimeRecordService runtimeRecordService,
    ILogger logger) : IRemoteRuntimeService
{
    public async Task<IReadOnlyList<RuntimeProcess>> GetProcessesAsync(
        RemoteRuntimeQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await remoteCommandAdapter.ExecuteAsync(
            request.Node,
            request.Credential,
            "pgrep -a 'frpc|frps' || true",
            cancellationToken);

        var processes = ParseProcesses(request.Node.Name, result.Output);
        foreach (var process in processes)
        {
            await runtimeRecordService.SaveRuntimeProcessAsync(process, cancellationToken);
        }

        logger.Information(
            "Remote runtime status read for node {NodeName}: {ProcessCount} processes",
            request.Node.Name,
            processes.Count);

        return processes;
    }

    public Task<RemoteRuntimeCommandResult> StartAsync(
        RemoteRuntimeCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        return ExecuteCommandAsync(request, FrpNexusStatus.Running, "远程启动命令执行完成。", cancellationToken);
    }

    public Task<RemoteRuntimeCommandResult> StopAsync(
        RemoteRuntimeCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        return ExecuteCommandAsync(request, FrpNexusStatus.Stopped, "远程停止命令执行完成。", cancellationToken);
    }

    public async Task<RemoteRuntimeCommandResult> RestartAsync(
        RemoteRuntimeCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopResult = await ExecuteCommandAsync(request, FrpNexusStatus.Stopped, "远程停止命令执行完成。", cancellationToken);
        if (stopResult.Status == FrpNexusStatus.Error)
        {
            return stopResult;
        }

        return await ExecuteCommandAsync(request, FrpNexusStatus.Running, "远程重启命令执行完成。", cancellationToken);
    }

    private async Task<RemoteRuntimeCommandResult> ExecuteCommandAsync(
        RemoteRuntimeCommandRequest request,
        FrpNexusStatus successStatus,
        string successMessage,
        CancellationToken cancellationToken)
    {
        var completedAt = DateTimeOffset.UtcNow;

        try
        {
            var result = await remoteCommandAdapter.ExecuteAsync(
                request.Node,
                request.Credential,
                request.Command,
                cancellationToken);

            if (result.ExitCode != 0)
            {
                return await SaveResultAsync(
                    request,
                    FrpNexusStatus.Error,
                    completedAt,
                    $"远程命令执行失败：{SanitizeMessage(result.Error)}",
                    cancellationToken);
            }

            return await SaveResultAsync(
                request,
                successStatus,
                completedAt,
                successMessage,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return await SaveResultAsync(
                request,
                FrpNexusStatus.Error,
                completedAt,
                $"远程命令执行失败：{SanitizeMessage(exception.Message)}",
                cancellationToken);
        }
    }

    private async Task<RemoteRuntimeCommandResult> SaveResultAsync(
        RemoteRuntimeCommandRequest request,
        FrpNexusStatus status,
        DateTimeOffset completedAt,
        string message,
        CancellationToken cancellationToken)
    {
        var process = new RuntimeProcess(
            request.ProcessName,
            request.Node.Name,
            request.ProcessKind,
            status,
            "-",
            status == FrpNexusStatus.Running ? "刚刚启动" : "-",
            "-");

        await runtimeRecordService.SaveRuntimeProcessAsync(process, cancellationToken);

        logger.Information(
            "Remote runtime command completed for node {NodeName}, process {ProcessName}, status {Status}",
            request.Node.Name,
            request.ProcessName,
            status);

        return new RemoteRuntimeCommandResult(
            request.Node.Name,
            request.ProcessName,
            status,
            completedAt,
            message);
    }

    private static IReadOnlyList<RuntimeProcess> ParseProcesses(string nodeName, string output)
    {
        return output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => ParseProcess(nodeName, line))
            .Where(process => process is not null)
            .Cast<RuntimeProcess>()
            .ToArray();
    }

    private static RuntimeProcess? ParseProcess(string nodeName, string line)
    {
        var firstSpace = line.IndexOf(' ', StringComparison.Ordinal);
        if (firstSpace <= 0)
        {
            return null;
        }

        var processId = line[..firstSpace].Trim();
        var command = line[(firstSpace + 1)..].Trim();
        var fileName = Path.GetFileName(command.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? command);
        var processKind = fileName.Contains("frps", StringComparison.OrdinalIgnoreCase) ? "frps" : "frpc";
        var name = $"{processKind}-{processId}";

        return new RuntimeProcess(
            name,
            nodeName,
            processKind,
            FrpNexusStatus.Running,
            processId,
            "-",
            "-");
    }

    private static string SanitizeMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message)
            ? "未返回详细错误。"
            : message.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();
    }
}
