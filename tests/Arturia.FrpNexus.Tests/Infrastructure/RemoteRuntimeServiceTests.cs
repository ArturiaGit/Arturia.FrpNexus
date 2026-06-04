using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Runtime;
using Serilog.Core;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class RemoteRuntimeServiceTests
{
    [Fact]
    public async Task GetProcessesAsync_ShouldParseRemoteProcessesAndPersistSnapshots()
    {
        var runtimeRecordService = new FakeRuntimeRecordService();
        var service = new RemoteRuntimeService(
            new FakeRemoteCommandAdapter(new RemoteCommandResult(0, "2048 /opt/frp/frpc -c /etc/frp/frpc.toml\n4096 /opt/frp/frps -c /etc/frp/frps.toml", string.Empty)),
            runtimeRecordService,
            Logger.None);

        var processes = await service.GetProcessesAsync(CreateQueryRequest());

        Assert.Equal(2, processes.Count);
        Assert.Contains(processes, process => process.ProcessKind == "frpc" && process.ProcessId == "2048");
        Assert.Contains(processes, process => process.ProcessKind == "frps" && process.ProcessId == "4096");
        Assert.Equal(2, runtimeRecordService.SavedProcesses.Count);
    }

    [Fact]
    public async Task StartAsync_ShouldExecuteCommandAndPersistRunningSnapshot()
    {
        var runtimeRecordService = new FakeRuntimeRecordService();
        var adapter = new FakeRemoteCommandAdapter(new RemoteCommandResult(0, "started", string.Empty));
        var service = new RemoteRuntimeService(adapter, runtimeRecordService, Logger.None);

        var result = await service.StartAsync(CreateCommandRequest("nohup /opt/frp/frpc -c /etc/frp/frpc.toml &"));

        Assert.Equal(FrpNexusStatus.Running, result.Status);
        Assert.Equal("远程启动命令执行完成。", result.Message);
        Assert.Equal("nohup /opt/frp/frpc -c /etc/frp/frpc.toml &", adapter.LastCommand);
        Assert.Equal(FrpNexusStatus.Running, runtimeRecordService.SavedProcesses.Single().Status);
    }

    [Fact]
    public async Task StopAsync_ShouldReturnChineseFailureAndHideSecret()
    {
        var runtimeRecordService = new FakeRuntimeRecordService();
        var service = new RemoteRuntimeService(
            new FakeRemoteCommandAdapter(new RemoteCommandResult(1, string.Empty, "permission denied")),
            runtimeRecordService,
            Logger.None);

        var result = await service.StopAsync(CreateCommandRequest("pkill -f frpc"));

        Assert.Equal(FrpNexusStatus.Error, result.Status);
        Assert.Equal("远程命令执行失败：permission denied", result.Message);
        Assert.DoesNotContain("SESSION_PASSWORD_PLACEHOLDER", result.Message, StringComparison.Ordinal);
        Assert.Equal(FrpNexusStatus.Error, runtimeRecordService.SavedProcesses.Single().Status);
    }

    private static RemoteRuntimeQueryRequest CreateQueryRequest()
    {
        return new RemoteRuntimeQueryRequest(CreateNode(), CreateCredential());
    }

    private static RemoteRuntimeCommandRequest CreateCommandRequest(string command)
    {
        return new RemoteRuntimeCommandRequest(
            CreateNode(),
            CreateCredential(),
            "frpc-web",
            "frpc",
            command);
    }

    private static NodeProfile CreateNode()
    {
        return new NodeProfile(
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
    }

    private static SshCredentialReference CreateCredential()
    {
        return new SshCredentialReference(
            SshAuthenticationMode.SessionPassword,
            SessionPassword: "SESSION_PASSWORD_PLACEHOLDER");
    }

    private sealed class FakeRemoteCommandAdapter(RemoteCommandResult result) : IRemoteCommandAdapter
    {
        public string? LastCommand { get; private set; }

        public Task<RemoteCommandResult> ExecuteAsync(NodeProfile node, SshCredentialReference credential, string command, CancellationToken cancellationToken = default)
        {
            LastCommand = command;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeRuntimeRecordService : IRuntimeRecordService
    {
        public List<RuntimeProcess> SavedProcesses { get; } = [];

        public Task<IReadOnlyList<RuntimeProcess>> ListRuntimeProcessesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RuntimeProcess>>(SavedProcesses);
        }

        public Task<RuntimeProcess?> GetRuntimeProcessAsync(string processName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SavedProcesses.FirstOrDefault(process => process.Name == processName));
        }

        public Task SaveRuntimeProcessAsync(RuntimeProcess process, CancellationToken cancellationToken = default)
        {
            SavedProcesses.RemoveAll(item => item.Name == process.Name);
            SavedProcesses.Add(process);
            return Task.CompletedTask;
        }

        public Task DeleteRuntimeProcessAsync(string processName, CancellationToken cancellationToken = default)
        {
            SavedProcesses.RemoveAll(process => process.Name == processName);
            return Task.CompletedTask;
        }
    }
}
