using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class RuntimePageViewModelTests
{
    [Fact]
    public async Task LoadRuntimeProcessesAsync_ShouldPopulateProcessesFromService()
    {
        var service = new FakeRuntimeRecordService(
        [
            new("frpc-test", "本地测试节点", "frpc", FrpNexusStatus.Running, "2048", "1h", "127.0.0.1:8080")
        ]);
        var viewModel = CreateViewModel(service, new FakeDeploymentRecordService([]));

        await viewModel.LoadRuntimeProcessesAsync();

        Assert.Single(viewModel.Processes);
        Assert.Equal("frpc-test", viewModel.SelectedProcess?.Name);
        Assert.Equal("共 1 条本地运行记录", viewModel.ProcessCountText);
    }

    [Fact]
    public async Task LoadRuntimeProcessesAsync_ShouldKeepEmptyStateWhenDatabaseIsEmpty()
    {
        var service = new FakeRuntimeRecordService([]);
        var viewModel = CreateViewModel(service, new FakeDeploymentRecordService([]));

        await viewModel.LoadRuntimeProcessesAsync();

        Assert.Empty(viewModel.Processes);
        Assert.Null(viewModel.SelectedProcess);
        Assert.Equal("共 0 条本地运行记录", viewModel.ProcessCountText);
        Assert.Empty(service.SavedProcesses);
    }

    [Fact]
    public async Task LoadDeploymentRecordsAsync_ShouldPopulateStepsFromService()
    {
        var deploymentService = new FakeDeploymentRecordService(
        [
            new("生成 TOML", "本地测试节点", "已生成本地 TOML 配置", FrpNexusStatus.Ready, DateTimeOffset.UtcNow)
        ]);
        var viewModel = CreateViewModel(new FakeRuntimeRecordService([]), deploymentService);

        await viewModel.LoadDeploymentRecordsAsync();

        var step = Assert.Single(viewModel.DeploymentSteps);
        Assert.Equal("生成 TOML", step.Title);
        Assert.Equal(FrpNexusStatus.Ready, step.Status);
    }

    [Fact]
    public async Task LoadDeploymentRecordsAsync_ShouldSeedSafeSampleStepsWhenDatabaseIsEmpty()
    {
        var deploymentService = new FakeDeploymentRecordService([]);
        var viewModel = CreateViewModel(new FakeRuntimeRecordService([]), deploymentService);

        await viewModel.LoadDeploymentRecordsAsync();

        Assert.Equal(4, viewModel.DeploymentSteps.Count);
        Assert.Contains(viewModel.DeploymentSteps, step => step.Title == "测试 SSH 连接");
        Assert.DoesNotContain(deploymentService.SavedRecords, record => record.Description.Contains("密码", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(deploymentService.SavedRecords, record => record.Description.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(deploymentService.SavedRecords, record => record.Description.Contains("私钥内容", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RefreshRemoteProcessesCommand_ShouldLoadRemoteProcessesAndClearSecret()
    {
        var remoteRuntimeService = new FakeRemoteRuntimeService();
        var viewModel = CreateViewModel(
            new FakeRuntimeRecordService([new("frpc-web", "Web-Server-HK", "frpc", FrpNexusStatus.Stopped, "-", "-", "-")]),
            new FakeDeploymentRecordService([]),
            remoteRuntimeService);
        await viewModel.LoadRuntimeProcessesAsync();
        viewModel.SshSessionPassword = "SESSION_PASSWORD_PLACEHOLDER";

        await viewModel.RefreshRemoteProcessesCommand.ExecuteAsync(null);

        Assert.Equal("已刷新远程进程状态。", viewModel.StatusText);
        Assert.Equal(string.Empty, viewModel.SshSessionPassword);
        Assert.Contains(viewModel.Processes, process => process.Status == FrpNexusStatus.Running);
        Assert.NotNull(remoteRuntimeService.LastQueryRequest);
    }

    [Fact]
    public async Task StartSelectedProcessCommand_ShouldRunRemoteCommandAndClearSecret()
    {
        var remoteRuntimeService = new FakeRemoteRuntimeService();
        var viewModel = CreateViewModel(
            new FakeRuntimeRecordService([new("frpc-web", "Web-Server-HK", "frpc", FrpNexusStatus.Stopped, "-", "-", "-")]),
            new FakeDeploymentRecordService([]),
            remoteRuntimeService);
        await viewModel.LoadRuntimeProcessesAsync();
        viewModel.SelectedProcess = viewModel.Processes.Single();
        viewModel.SshSessionPassword = "SESSION_PASSWORD_PLACEHOLDER";
        viewModel.RemoteCommandText = "nohup /opt/frp/frpc -c /etc/frp/frpc.toml &";

        await viewModel.StartSelectedProcessCommand.ExecuteAsync(null);

        Assert.Equal("远程启动命令执行完成。", viewModel.StatusText);
        Assert.Equal(string.Empty, viewModel.SshSessionPassword);
        Assert.NotNull(remoteRuntimeService.LastCommandRequest);
        Assert.Equal("frpc-web", remoteRuntimeService.LastCommandRequest.ProcessName);
        Assert.DoesNotContain("SESSION_PASSWORD_PLACEHOLDER", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadRuntimeProcessesAsync_ShouldReportRecoverableFailure()
    {
        var viewModel = CreateViewModel(new FailingRuntimeRecordService(), new FakeDeploymentRecordService([]));

        await viewModel.LoadRuntimeProcessesAsync();

        Assert.Equal("运行记录加载失败", viewModel.ProcessCountText);
        Assert.Equal("运行记录加载失败，请检查输入、网络或本地数据状态后重试。", viewModel.StatusText);
        Assert.Empty(viewModel.Processes);
    }

    [Fact]
    public async Task RefreshRemoteProcessesCommand_ShouldReportFailureAndClearSecret()
    {
        var viewModel = CreateViewModel(
            new FakeRuntimeRecordService([new("frpc-web", "Web-Server-HK", "frpc", FrpNexusStatus.Stopped, "-", "-", "-")]),
            new FakeDeploymentRecordService([]),
            new FailingRemoteRuntimeService());
        await viewModel.LoadRuntimeProcessesAsync();
        viewModel.SshSessionPassword = "SESSION_PASSWORD_PLACEHOLDER";

        await viewModel.RefreshRemoteProcessesCommand.ExecuteAsync(null);

        Assert.Equal("远程进程刷新失败，请检查输入、网络或本地数据状态后重试。", viewModel.StatusText);
        Assert.Equal(string.Empty, viewModel.SshSessionPassword);
        Assert.False(viewModel.IsRemoteCommandRunning);
    }

    private static RuntimePageViewModel CreateViewModel(
        IRuntimeRecordService runtimeRecordService,
        IDeploymentRecordService deploymentRecordService,
        IRemoteRuntimeService? remoteRuntimeService = null)
    {
        return new RuntimePageViewModel(
            runtimeRecordService,
            deploymentRecordService,
            new FakeNodeManagementService(),
            remoteRuntimeService ?? new FakeRemoteRuntimeService());
    }

    private sealed class FakeRuntimeRecordService(IReadOnlyList<RuntimeProcess> processes) : IRuntimeRecordService
    {
        private readonly List<RuntimeProcess> _processes = [.. processes];

        public IReadOnlyList<RuntimeProcess> SavedProcesses => _processes;

        public Task<IReadOnlyList<RuntimeProcess>> ListRuntimeProcessesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RuntimeProcess>>(_processes);
        }

        public Task<RuntimeProcess?> GetRuntimeProcessAsync(string processName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_processes.FirstOrDefault(process => process.Name == processName));
        }

        public Task SaveRuntimeProcessAsync(RuntimeProcess process, CancellationToken cancellationToken = default)
        {
            _processes.RemoveAll(item => item.Name == process.Name);
            _processes.Add(process);
            return Task.CompletedTask;
        }

        public Task DeleteRuntimeProcessAsync(string processName, CancellationToken cancellationToken = default)
        {
            _processes.RemoveAll(process => process.Name == processName);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingRuntimeRecordService : IRuntimeRecordService
    {
        public Task<IReadOnlyList<RuntimeProcess>> ListRuntimeProcessesAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("运行记录数据库不可用");
        }

        public Task<RuntimeProcess?> GetRuntimeProcessAsync(string processName, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("运行记录数据库不可用");
        }

        public Task SaveRuntimeProcessAsync(RuntimeProcess process, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("运行记录数据库不可用");
        }

        public Task DeleteRuntimeProcessAsync(string processName, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("运行记录数据库不可用");
        }
    }

    private sealed class FakeDeploymentRecordService(IReadOnlyList<DeploymentRecord> records) : IDeploymentRecordService
    {
        private readonly List<DeploymentRecord> _records = [.. records];

        public IReadOnlyList<DeploymentRecord> SavedRecords => _records;

        public Task<IReadOnlyList<DeploymentRecord>> ListDeploymentRecordsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DeploymentRecord>>(_records);
        }

        public Task<DeploymentRecord?> GetDeploymentRecordAsync(string stepName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_records.FirstOrDefault(record => record.StepName == stepName));
        }

        public Task SaveDeploymentRecordAsync(DeploymentRecord record, CancellationToken cancellationToken = default)
        {
            _records.RemoveAll(item => item.StepName == record.StepName);
            _records.Add(record);
            return Task.CompletedTask;
        }

        public Task DeleteDeploymentRecordAsync(string stepName, CancellationToken cancellationToken = default)
        {
            _records.RemoveAll(record => record.StepName == stepName);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNodeManagementService : INodeManagementService
    {
        private readonly List<NodeProfile> _nodes =
        [
            new("Web-Server-HK", "203.0.113.10", 22, "deploy", "会话密码", "Ubuntu 22.04 LTS", FrpNexusStatus.Online, FrpNexusStatus.Running, "v0.61.1", "-", "/etc/frp/frpc.toml"),
            new("本地测试节点", "203.0.113.11", 22, "deploy", "会话密码", "Ubuntu 22.04 LTS", FrpNexusStatus.Online, FrpNexusStatus.Running, "v0.61.1", "-", "/etc/frp/frpc.toml")
        ];

        public Task<IReadOnlyList<NodeProfile>> ListNodesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<NodeProfile>>(_nodes);
        }

        public Task<NodeProfile?> GetNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_nodes.FirstOrDefault(node => node.Name == nodeName));
        }

        public Task SaveNodeAsync(NodeProfile node, CancellationToken cancellationToken = default)
        {
            _nodes.RemoveAll(item => item.Name == node.Name);
            _nodes.Add(node);
            return Task.CompletedTask;
        }

        public Task DeleteNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            _nodes.RemoveAll(node => node.Name == nodeName);
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

    private sealed class FakeRemoteRuntimeService : IRemoteRuntimeService
    {
        public RemoteRuntimeQueryRequest? LastQueryRequest { get; private set; }

        public RemoteRuntimeCommandRequest? LastCommandRequest { get; private set; }

        public Task<IReadOnlyList<RuntimeProcess>> GetProcessesAsync(RemoteRuntimeQueryRequest request, CancellationToken cancellationToken = default)
        {
            LastQueryRequest = request;
            IReadOnlyList<RuntimeProcess> processes =
            [
                new("frpc-web", request.Node.Name, "frpc", FrpNexusStatus.Running, "2048", "1h", "127.0.0.1:8080")
            ];

            return Task.FromResult(processes);
        }

        public Task<RemoteRuntimeCommandResult> StartAsync(RemoteRuntimeCommandRequest request, CancellationToken cancellationToken = default)
        {
            LastCommandRequest = request;
            return CreateResult(request, FrpNexusStatus.Running, "远程启动命令执行完成。");
        }

        public Task<RemoteRuntimeCommandResult> StopAsync(RemoteRuntimeCommandRequest request, CancellationToken cancellationToken = default)
        {
            LastCommandRequest = request;
            return CreateResult(request, FrpNexusStatus.Stopped, "远程停止命令执行完成。");
        }

        public Task<RemoteRuntimeCommandResult> RestartAsync(RemoteRuntimeCommandRequest request, CancellationToken cancellationToken = default)
        {
            LastCommandRequest = request;
            return CreateResult(request, FrpNexusStatus.Running, "远程重启命令执行完成。");
        }

        private static Task<RemoteRuntimeCommandResult> CreateResult(RemoteRuntimeCommandRequest request, FrpNexusStatus status, string message)
        {
            return Task.FromResult(new RemoteRuntimeCommandResult(
                request.Node.Name,
                request.ProcessName,
                status,
                DateTimeOffset.UtcNow,
                message));
        }
    }

    private sealed class FailingRemoteRuntimeService : IRemoteRuntimeService
    {
        public Task<IReadOnlyList<RuntimeProcess>> GetProcessesAsync(RemoteRuntimeQueryRequest request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("远程命令失败");
        }

        public Task<RemoteRuntimeCommandResult> StartAsync(RemoteRuntimeCommandRequest request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("远程命令失败");
        }

        public Task<RemoteRuntimeCommandResult> StopAsync(RemoteRuntimeCommandRequest request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("远程命令失败");
        }

        public Task<RemoteRuntimeCommandResult> RestartAsync(RemoteRuntimeCommandRequest request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("远程命令失败");
        }
    }
}
