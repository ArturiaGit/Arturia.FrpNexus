using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class LogsPageViewModelTests
{
    [Fact]
    public void NodeFilter_ShouldFilterVisibleLogsAndAllNodesShouldRestoreRows()
    {
        var viewModel = new LogsPageViewModel(new FakeNodeManagementService(), new FakeRemoteLogService());

        viewModel.SelectedNodeFilter = "DB-Node-SH";

        Assert.Equal(3, viewModel.VisibleLogs.Count);
        Assert.All(viewModel.VisibleLogs, log => Assert.Equal("DB-Node-SH", log.NodeName));
        Assert.Equal("[Connected: DB-Node-SH]", viewModel.TerminalConnectionText);

        viewModel.SelectedNodeFilter = "全部节点";

        Assert.Equal(viewModel.Logs.Count, viewModel.VisibleLogs.Count);
        Assert.Equal("[Connected: 全部节点]", viewModel.TerminalConnectionText);
    }

    [Fact]
    public void SearchText_ShouldMatchMessageNodeProcessLevelAndTimestamp()
    {
        var viewModel = new LogsPageViewModel(new FakeNodeManagementService(), new FakeRemoteLogService());

        viewModel.SearchText = "db_backup_sync";

        Assert.Equal(3, viewModel.VisibleLogs.Count);
        Assert.All(viewModel.VisibleLogs, log => Assert.Contains("db_backup_sync", log.Message, StringComparison.OrdinalIgnoreCase));

        viewModel.SearchText = "Web-Server-HK";

        Assert.Equal(4, viewModel.VisibleLogs.Count);
        Assert.All(viewModel.VisibleLogs, log => Assert.Equal("Web-Server-HK", log.NodeName));

        viewModel.SearchText = "14:36:00.001";

        Assert.Single(viewModel.VisibleLogs);
    }

    [Fact]
    public void ProcessAndLevelFilters_ShouldCombineWithSearch()
    {
        var viewModel = new LogsPageViewModel(new FakeNodeManagementService(), new FakeRemoteLogService());

        viewModel.SelectedProcessFilter = "frpc";
        viewModel.SelectedLevelFilter = "WARN";

        Assert.Equal(2, viewModel.VisibleLogs.Count);
        Assert.All(viewModel.VisibleLogs, log =>
        {
            Assert.Equal("frpc", log.ProcessName);
            Assert.Equal("WARN", log.Level);
        });

        viewModel.SearchText = "10 seconds";

        Assert.Single(viewModel.VisibleLogs);
        Assert.Contains("10 seconds", viewModel.VisibleLogs.Single().Message, StringComparison.OrdinalIgnoreCase);
    }

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
        Assert.Single(viewModel.VisibleLogs);
        Assert.Equal("已读取 1 行远程日志。", viewModel.StatusText);
        Assert.Equal(string.Empty, viewModel.SshSessionPassword);
        Assert.False(viewModel.IsRemoteCredentialsVisible);
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

    [Fact]
    public async Task ReadRemoteLogsCommand_ShouldReportFailureAndClearSecret()
    {
        var viewModel = new LogsPageViewModel(new FakeNodeManagementService(), new FailingRemoteLogService());
        viewModel.SelectedNodeName = "Web-Server-HK";
        viewModel.SshSessionPassword = "SESSION_PASSWORD_PLACEHOLDER";

        await viewModel.ReadRemoteLogsCommand.ExecuteAsync(null);

        Assert.Equal("远程日志读取失败，请检查输入、网络或本地数据状态后重试。", viewModel.StatusText);
        Assert.Equal(string.Empty, viewModel.SshSessionPassword);
        Assert.False(viewModel.IsReadingRemoteLogs);
    }

    [Fact]
    public async Task ReadRemoteLogsCommand_ShouldReportNodeLookupFailure()
    {
        var viewModel = new LogsPageViewModel(new FailingNodeManagementService(), new FakeRemoteLogService());
        viewModel.SelectedNodeName = "Web-Server-HK";
        viewModel.SshSessionPassword = "SESSION_PASSWORD_PLACEHOLDER";

        await viewModel.ReadRemoteLogsCommand.ExecuteAsync(null);

        Assert.Equal("节点资料读取失败，请检查输入、网络或本地数据状态后重试。", viewModel.StatusText);
        Assert.Equal("SESSION_PASSWORD_PLACEHOLDER", viewModel.SshSessionPassword);
    }

    [Fact]
    public async Task ReadRemoteLogsCommand_ShouldUseSelectedNodeFilterForRemoteRequest()
    {
        var remoteLogService = new FakeRemoteLogService();
        var viewModel = new LogsPageViewModel(new FakeNodeManagementService(), remoteLogService);
        viewModel.SelectedNodeFilter = "DB-Node-SH";
        viewModel.SshSessionPassword = "SESSION_PASSWORD_PLACEHOLDER";

        await viewModel.ReadRemoteLogsCommand.ExecuteAsync(null);

        Assert.NotNull(remoteLogService.LastRequest);
        Assert.Equal("DB-Node-SH", remoteLogService.LastRequest.Node.Name);
    }

    [Fact]
    public void ToggleRemoteCredentialsCommand_ShouldShowAndHideCredentialPanel()
    {
        var viewModel = new LogsPageViewModel(new FakeNodeManagementService(), new FakeRemoteLogService());

        viewModel.ToggleRemoteCredentialsCommand.Execute(null);

        Assert.True(viewModel.IsRemoteCredentialsVisible);

        viewModel.ToggleRemoteCredentialsCommand.Execute(null);

        Assert.False(viewModel.IsRemoteCredentialsVisible);
    }

    private sealed class FakeNodeManagementService : INodeManagementService
    {
        private readonly NodeProfile[] _nodes =
        [
            new(
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
            "/etc/frp/frpc.toml"),
            new(
            "DB-Node-SH",
            "198.51.100.20",
            22,
            "deploy",
            "会话密码",
            "Debian 12",
            FrpNexusStatus.Online,
            FrpNexusStatus.Running,
            "v0.61.1",
            "-",
            "/etc/frp/frpc.toml")
        ];

        public Task<IReadOnlyList<NodeProfile>> ListNodesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<NodeProfile>>(_nodes);
        }

        public Task<NodeProfile?> GetNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_nodes.FirstOrDefault(node => string.Equals(nodeName, node.Name, StringComparison.OrdinalIgnoreCase)));
        }

        public Task SaveNodeAsync(NodeProfile node, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateLastConnectionAsync(string nodeName, DateTimeOffset connectedAt, CancellationToken cancellationToken = default)
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

    private sealed class FailingNodeManagementService : INodeManagementService
    {
        public Task<IReadOnlyList<NodeProfile>> ListNodesAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("节点数据库不可用");
        }

        public Task<NodeProfile?> GetNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("节点数据库不可用");
        }

        public Task SaveNodeAsync(NodeProfile node, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("节点数据库不可用");
        }

        public Task DeleteNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("节点数据库不可用");
        }

        public Task UpdateLastConnectionAsync(string nodeName, DateTimeOffset connectedAt, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("节点数据库不可用");
        }

        public Task UpdateConnectionTestResultAsync(string nodeName, FrpNexusStatus status, DateTimeOffset testedAt, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("节点数据库不可用");
        }
    }

    private sealed class FailingRemoteLogService : IRemoteLogService
    {
        public Task<IReadOnlyList<LogEntry>> ReadRecentLogsAsync(RemoteLogReadRequest request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("远程日志读取失败");
        }

        public async IAsyncEnumerable<LogEntry> StreamLogsAsync(RemoteLogReadRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw new InvalidOperationException("远程日志读取失败");
            #pragma warning disable CS0162
            yield break;
            #pragma warning restore CS0162
        }
    }
}
