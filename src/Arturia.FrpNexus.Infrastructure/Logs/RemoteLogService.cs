using System.Runtime.CompilerServices;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Runtime;
using Serilog;

namespace Arturia.FrpNexus.Infrastructure.Logs;

public sealed class RemoteLogService(
    IRemoteCommandAdapter remoteCommandAdapter,
    ILogger logger) : IRemoteLogService
{
    public async Task<IReadOnlyList<LogEntry>> ReadRecentLogsAsync(
        RemoteLogReadRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var command = $"tail -n {Math.Clamp(request.LineCount, 1, 1000)} {QuoteShellPath(request.LogPath)}";
        var result = await remoteCommandAdapter.ExecuteAsync(
            request.Node,
            request.Credential,
            command,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            logger.Warning(
                "Remote log read failed for node {NodeName}, process {ProcessName}, path {LogPath}",
                request.Node.Name,
                request.ProcessName,
                request.LogPath);

            return
            [
                new(
                    DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    "ERROR",
                    request.Node.Name,
                    request.ProcessName,
                    $"远程日志读取失败：{SanitizeMessage(result.Error)}",
                    FrpNexusStatus.Error)
            ];
        }

        return ParseLogs(request.Node.Name, request.ProcessName, result.Output);
    }

    public async IAsyncEnumerable<LogEntry> StreamLogsAsync(
        RemoteLogReadRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var command = $"tail -n {Math.Clamp(request.LineCount, 1, 1000)} -f {QuoteShellPath(request.LogPath)}";
        var result = await remoteCommandAdapter.ExecuteAsync(
            request.Node,
            request.Credential,
            command,
            cancellationToken);

        foreach (var log in ParseLogs(request.Node.Name, request.ProcessName, result.Output))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return log;
        }
    }

    private static IReadOnlyList<LogEntry> ParseLogs(string nodeName, string processName, string output)
    {
        return output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => ParseLog(nodeName, processName, line))
            .ToArray();
    }

    private static LogEntry ParseLog(string nodeName, string processName, string line)
    {
        var level = DetectLevel(line);
        return new LogEntry(
            DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            level,
            nodeName,
            processName,
            line,
            LevelToStatus(level));
    }

    private static string DetectLevel(string line)
    {
        if (line.Contains("error", StringComparison.OrdinalIgnoreCase)
            || line.Contains("failed", StringComparison.OrdinalIgnoreCase))
        {
            return "ERROR";
        }

        if (line.Contains("warn", StringComparison.OrdinalIgnoreCase)
            || line.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "WARN";
        }

        return "INFO";
    }

    private static FrpNexusStatus LevelToStatus(string level)
    {
        return level switch
        {
            "ERROR" => FrpNexusStatus.Error,
            "WARN" => FrpNexusStatus.Warning,
            _ => FrpNexusStatus.Ready
        };
    }

    private static void ValidateRequest(RemoteLogReadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProcessName))
        {
            throw new InvalidOperationException("请选择远程日志所属进程。");
        }

        if (string.IsNullOrWhiteSpace(request.LogPath)
            || !request.LogPath.StartsWith("/", StringComparison.Ordinal)
            || request.LogPath.Contains("\0", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("远程日志路径必须是 Linux 绝对路径。");
        }
    }

    private static string QuoteShellPath(string path)
    {
        return $"'{path.Replace("'", "'\\''", StringComparison.Ordinal)}'";
    }

    private static string SanitizeMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message)
            ? "未返回详细错误。"
            : message.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();
    }
}
