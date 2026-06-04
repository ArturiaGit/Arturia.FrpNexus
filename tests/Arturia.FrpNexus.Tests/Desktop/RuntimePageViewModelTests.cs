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
        var viewModel = new RuntimePageViewModel(service, new FakeDeploymentRecordService([]));

        await viewModel.LoadRuntimeProcessesAsync();

        Assert.Single(viewModel.Processes);
        Assert.Equal("frpc-test", viewModel.SelectedProcess?.Name);
        Assert.Equal("共 1 条本地运行记录", viewModel.ProcessCountText);
    }

    [Fact]
    public async Task LoadRuntimeProcessesAsync_ShouldSeedSafeSampleProcessesWhenDatabaseIsEmpty()
    {
        var service = new FakeRuntimeRecordService([]);
        var viewModel = new RuntimePageViewModel(service, new FakeDeploymentRecordService([]));

        await viewModel.LoadRuntimeProcessesAsync();

        Assert.Equal(4, viewModel.Processes.Count);
        Assert.Contains(viewModel.Processes, process => process.Status == FrpNexusStatus.Running);
        Assert.Contains(viewModel.Processes, process => process.Status == FrpNexusStatus.Stopped);
        Assert.Contains(viewModel.Processes, process => process.Status == FrpNexusStatus.Error);
        Assert.DoesNotContain(service.SavedProcesses, process => process.Name.Contains("password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(service.SavedProcesses, process => process.Name.Contains("token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LoadDeploymentRecordsAsync_ShouldPopulateStepsFromService()
    {
        var deploymentService = new FakeDeploymentRecordService(
        [
            new("生成 TOML", "本地测试节点", "已生成本地 TOML 配置", FrpNexusStatus.Ready, DateTimeOffset.UtcNow)
        ]);
        var viewModel = new RuntimePageViewModel(new FakeRuntimeRecordService([]), deploymentService);

        await viewModel.LoadDeploymentRecordsAsync();

        var step = Assert.Single(viewModel.DeploymentSteps);
        Assert.Equal("生成 TOML", step.Title);
        Assert.Equal(FrpNexusStatus.Ready, step.Status);
    }

    [Fact]
    public async Task LoadDeploymentRecordsAsync_ShouldSeedSafeSampleStepsWhenDatabaseIsEmpty()
    {
        var deploymentService = new FakeDeploymentRecordService([]);
        var viewModel = new RuntimePageViewModel(new FakeRuntimeRecordService([]), deploymentService);

        await viewModel.LoadDeploymentRecordsAsync();

        Assert.Equal(4, viewModel.DeploymentSteps.Count);
        Assert.Contains(viewModel.DeploymentSteps, step => step.Title == "测试 SSH 连接");
        Assert.DoesNotContain(deploymentService.SavedRecords, record => record.Description.Contains("密码", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(deploymentService.SavedRecords, record => record.Description.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(deploymentService.SavedRecords, record => record.Description.Contains("私钥内容", StringComparison.OrdinalIgnoreCase));
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
}
