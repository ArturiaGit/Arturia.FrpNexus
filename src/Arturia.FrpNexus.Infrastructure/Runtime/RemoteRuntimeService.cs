using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Logging;
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
            "ps -eo pid=,etime=,args= | awk '/[f]rpc|[f]rps/ { pid=$1; etime=$2; $1=\"\"; $2=\"\"; sub(/^  */, \"\", $0); print pid \"|\" etime \"|\" $0 }' || true",
            cancellationToken);

        var processes = ParseProcesses(request.Node.Name, result.Output);
        await runtimeRecordService.ReplaceRuntimeProcessesForNodeAsync(
            request.Node.Name,
            processes,
            cancellationToken);

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
                    BuildCommandFailureMessage(result),
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
                BuildCommandFailureMessage(exception),
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
        var parts = line.Split('|', 3, StringSplitOptions.TrimEntries);
        if (parts.Length == 3)
        {
            return ParseDelimitedProcess(nodeName, parts[0], parts[1], parts[2]);
        }

        return ParseLegacyProcess(nodeName, line);
    }

    private static RuntimeProcess? ParseDelimitedProcess(string nodeName, string processId, string uptime, string command)
    {
        if (string.IsNullOrWhiteSpace(processId)
            || string.IsNullOrWhiteSpace(command)
            || !IsFrpProcessCommand(command))
        {
            return null;
        }

        var processKind = ResolveProcessKind(command);
        var name = $"{processKind}-{processId}";

        return new RuntimeProcess(
            name,
            nodeName,
            processKind,
            FrpNexusStatus.Running,
            processId,
            string.IsNullOrWhiteSpace(uptime) ? "-" : uptime,
            "-",
            LogTextSanitizer.RedactSecrets(command));
    }

    private static RuntimeProcess? ParseLegacyProcess(string nodeName, string line)
    {
        var psEfProcess = ParsePsEfProcess(nodeName, line);
        if (psEfProcess is not null)
        {
            return psEfProcess;
        }

        var firstSpace = line.IndexOf(' ', StringComparison.Ordinal);
        if (firstSpace <= 0)
        {
            return null;
        }

        var processId = line[..firstSpace].Trim();
        var command = line[(firstSpace + 1)..].Trim();
        if (!IsFrpProcessCommand(command))
        {
            return null;
        }

        var processKind = ResolveProcessKind(command);
        var name = $"{processKind}-{processId}";

        return new RuntimeProcess(
            name,
            nodeName,
            processKind,
            FrpNexusStatus.Running,
            processId,
            "-",
            "-",
            LogTextSanitizer.RedactSecrets(command));
    }

    private static RuntimeProcess? ParsePsEfProcess(string nodeName, string line)
    {
        var columns = line.Split(' ', 8, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (columns.Length < 8 || !int.TryParse(columns[1], out _))
        {
            return null;
        }

        var processId = columns[1];
        var startOrTime = columns[4];
        var command = columns[7];
        if (!IsFrpProcessCommand(command))
        {
            return null;
        }

        var processKind = ResolveProcessKind(command);
        var name = $"{processKind}-{processId}";
        return new RuntimeProcess(
            name,
            nodeName,
            processKind,
            FrpNexusStatus.Running,
            processId,
            LooksLikeElapsedTime(startOrTime) ? startOrTime : "-",
            "-",
            LogTextSanitizer.RedactSecrets(command));
    }

    private static string ResolveProcessKind(string command)
    {
        var fileName = Path.GetFileName(command.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? command);
        return fileName.Contains("frps", StringComparison.OrdinalIgnoreCase)
            || command.Contains("frps", StringComparison.OrdinalIgnoreCase)
                ? "frps"
                : "frpc";
    }

    private static bool IsFrpProcessCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command)
            || command.Contains("grep", StringComparison.OrdinalIgnoreCase)
            || command.Contains("awk", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return command.Contains("frps", StringComparison.OrdinalIgnoreCase)
            || command.Contains("frpc", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeElapsedTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        var daySeparator = normalized.IndexOf('-', StringComparison.Ordinal);
        if (daySeparator >= 0)
        {
            if (!int.TryParse(normalized[..daySeparator], out _))
            {
                return false;
            }

            normalized = normalized[(daySeparator + 1)..];
        }

        var parts = normalized.Split(':');
        return parts is { Length: 2 or 3 }
            && parts.All(part => int.TryParse(part, out _));
    }

    private static string SanitizeMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message)
            ? "未返回详细错误。"
            : LogTextSanitizer
                .RedactSecrets(message.Replace(Environment.NewLine, " ", StringComparison.Ordinal))
                .Trim();
    }

    private static string BuildCommandFailureMessage(RemoteCommandResult result)
    {
        var detail = BuildReadableDiagnostic(result);
        return BuildCommandFailureMessage(detail);
    }

    private static string BuildCommandFailureMessage(string detail)
    {
        if (IsExecutableFormatFailure(detail))
        {
            return "frps 核心无法在当前 VPS 上执行，请确认上传的是匹配该 VPS 架构的 Linux frps，例如 linux_amd64 或 linux_arm64。";
        }

        return $"远程命令执行失败：{SanitizeMessage(detail)}";
    }

    private static string BuildCommandFailureMessage(Exception exception)
    {
        return exception is TimeoutException
            ? "远程命令执行超时：远程节点响应过慢，请检查网络和服务器状态。"
            : BuildCommandFailureMessage(exception.Message);
    }

    private static string BuildReadableDiagnostic(RemoteCommandResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Output))
        {
            return result.Error;
        }

        if (string.IsNullOrWhiteSpace(result.Error))
        {
            return result.Output;
        }

        return $"{result.Error}{Environment.NewLine}{result.Output}";
    }

    private static bool IsExecutableFormatFailure(string message)
    {
        return message.Contains("cannot execute binary file", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Exec format error", StringComparison.OrdinalIgnoreCase);
    }
}
