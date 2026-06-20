using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Logs;
using Arturia.FrpNexus.Infrastructure.Runtime;
using Serilog.Core;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class RemoteLogServiceTests
{
    [Fact]
    public async Task ReadRecentLogsAsync_ShouldParseLevels()
    {
        var service = new RemoteLogService(
            new FakeRemoteCommandAdapter(new RemoteCommandResult(0, "connected\nWARN timeout\nERROR failed", string.Empty)),
            Logger.None);

        var logs = await service.ReadRecentLogsAsync(CreateRequest());

        Assert.Equal(3, logs.Count);
        Assert.Contains(logs, log => log.Level == "INFO" && log.Status == FrpNexusStatus.Ready);
        Assert.Contains(logs, log => log.Level == "WARN" && log.Status == FrpNexusStatus.Warning);
        Assert.Contains(logs, log => log.Level == "ERROR" && log.Status == FrpNexusStatus.Error);
    }

    [Fact]
    public async Task ReadRecentLogsAsync_ShouldStripAnsiControlSequences()
    {
        var service = new RemoteLogService(
            new FakeRemoteCommandAdapter(new RemoteCommandResult(
                0,
                "\u001b[1;34m2026-06-15 21:49:33.209 [I] [frps/root.go:115] frps uses config file: /opt/frp/frps.toml\u001b[0m",
                string.Empty)),
            Logger.None);

        var logs = await service.ReadRecentLogsAsync(CreateRequest());

        var log = Assert.Single(logs);
        Assert.DoesNotContain("\u001b", log.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("[0m", log.Message, StringComparison.Ordinal);
        Assert.Contains("frps uses config file: /opt/frp/frps.toml", log.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadRecentLogsAsync_ShouldUseFrpLogTimestampInsteadOfReadTime()
    {
        var service = new RemoteLogService(
            new FakeRemoteCommandAdapter(new RemoteCommandResult(
                0,
                "2026-06-15 21:49:33.209 [I] [frps/root.go:115] frps uses config file: /opt/frp/frps.toml",
                string.Empty)),
            Logger.None);

        var logs = await service.ReadRecentLogsAsync(CreateRequest());

        var log = Assert.Single(logs);
        Assert.Equal("2026-06-15 21:49:33.209", log.Timestamp);
    }

    [Fact]
    public async Task ReadRecentLogsAsync_ShouldReturnChineseFailureAndHideSecret()
    {
        var service = new RemoteLogService(
            new FakeRemoteCommandAdapter(new RemoteCommandResult(1, string.Empty, "permission denied")),
            Logger.None);

        var logs = await service.ReadRecentLogsAsync(CreateRequest());

        var log = Assert.Single(logs);
        Assert.Equal("ERROR", log.Level);
        Assert.Equal("远程日志读取失败：permission denied", log.Message);
        Assert.DoesNotContain("SESSION_PASSWORD_PLACEHOLDER", log.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StreamLogsAsync_ShouldExposeAsyncEnumerableHook()
    {
        var service = new RemoteLogService(
            new FakeRemoteCommandAdapter(new RemoteCommandResult(0, "line one\nline two", string.Empty)),
            Logger.None);

        var logs = new List<LogEntry>();
        await foreach (var log in service.StreamLogsAsync(CreateRequest()))
        {
            logs.Add(log);
        }

        Assert.Equal(2, logs.Count);
    }

    [Fact]
    public async Task ReadRecentLogsAsync_ShouldRejectInvalidRemotePath()
    {
        var service = new RemoteLogService(
            new FakeRemoteCommandAdapter(new RemoteCommandResult(0, "line", string.Empty)),
            Logger.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReadRecentLogsAsync(CreateRequest("relative/frpc.log")));

        Assert.Equal("远程日志路径必须是 Linux 绝对路径。", exception.Message);
    }

    private static RemoteLogReadRequest CreateRequest(string logPath = "/tmp/frpnexus-frpc.log")
    {
        var node = new NodeProfile(
            "测试节点",
            "203.0.113.10",
            22,
            "deploy",
            "会话密码",
            "Ubuntu 22.04 LTS",
            FrpNexusStatus.Online,
            FrpNexusStatus.Running,
            "v0.61.1",
            "-",
            "/etc/frp/frpc.toml");

        var credential = new SshCredentialReference(
            SshAuthenticationMode.SessionPassword,
            SessionPassword: "SESSION_PASSWORD_PLACEHOLDER");

        return new RemoteLogReadRequest(node, credential, "frpc", logPath);
    }

    private sealed class FakeRemoteCommandAdapter(RemoteCommandResult result) : IRemoteCommandAdapter
    {
        public Task<RemoteCommandResult> ExecuteAsync(NodeProfile node, SshCredentialReference credential, string command, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(result);
        }
    }
}
