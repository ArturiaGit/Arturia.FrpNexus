using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class LogsPageViewModelTests
{
    [Fact]
    public async Task ReadRemoteLogsCommand_ShouldLoadLogsAndClearSecret()
    {
        var remoteLogService = new FakeRemoteLogService();
        var viewModel = new LogsPageViewModel(new FakeNodeManagementService(), remoteLogService);
        viewModel.SshSessionPassword = "SESSION_PASSWORD_PLACEHOLDER";
        viewModel.SelectedNodeName = "Web-Server-HK";
        viewModel.ProcessName = "frpc";
        viewModel.RemoteLogPath = "/tmp/frpnexus-frpc.log";

        await viewModel.ReadRemoteLogsCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Logs);
        Assert.Equal("已读取 1 行远程日志。", viewModel.StatusText);
        Assert.Equal(string.Empty, viewModel.SshSessionPassword);
        Assert.NotNull(remoteLogService.LastRequest);
        Assert.DoesNotContain("SESSION_PASSWORD_PLACEHOLDER", viewModel.Logs.Single().Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadRemoteLogsCommand_ShouldRejectMissingSessionPassword()
    {
        var remoteLogService = new FakeRemoteLogService();
        var viewModel = new LogsPageViewModel(new FakeNodeManagementService(), remoteLogService);
        viewModel.SelectedNodeName = "Web-Server-HK";
        viewModel.ProcessName = "frpc";
        viewModel.RemoteLogPath = "/tmp/frpnexus-frpc.log";

        await viewModel.ReadRemoteLogsCommand.ExecuteAsync(null);

        Assert.Equal("请输入本次会话使用的 SSH 密码，密码不会保存到 SQLite。", viewModel.StatusText);
        Assert.Null(remoteLogService.LastRequest);
    }

    private sealed class FakeNodeManagementService : INodeManagementService
    {
        private readonly NodeProfile _node = new(
            "Web-Server-HK",
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

        public Task<IReadOnlyList<NodeProfile>> ListNodesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<NodeProfile>>([_node]);
        }

        public Task<NodeProfile?> GetNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(string.Equals(nodeName, _node.Name, StringComparison.OrdinalIgnoreCase) ? _node : null);
        }

        public Task SaveNodeAsync(NodeProfile node, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateConnectionTestResultAsync(string nodeName, FrpNexusStatus status, DateTimeOffset testedAt, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRemoteLogService : IRemoteLogService
    {
        public RemoteLogReadRequest? LastRequest { get; private set; }

        public Task<IReadOnlyList<LogEntry>> ReadRecentLogsAsync(RemoteLogReadRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            IReadOnlyList<LogEntry> logs =
            [
                new("2026-06-04 12:00:00.000", "INFO", request.Node.Name, request.ProcessName, "remote log line", FrpNexusStatus.Ready)
            ];

            return Task.FromResult(logs);
        }

        public async IAsyncEnumerable<LogEntry> StreamLogsAsync(RemoteLogReadRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var logs = await ReadRecentLogsAsync(request, cancellationToken);
            foreach (var log in logs)
            {
                yield return log;
            }
        }
    }
}
