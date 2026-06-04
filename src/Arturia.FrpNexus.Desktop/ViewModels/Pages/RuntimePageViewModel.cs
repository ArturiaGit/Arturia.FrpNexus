using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed partial class RuntimePageViewModel : PageViewModel
{
    private readonly IRuntimeRecordService _runtimeRecordService;
    private readonly IDeploymentRecordService _deploymentRecordService;

    [ObservableProperty]
    private RuntimeProcess? _selectedProcess;

    [ObservableProperty]
    private string _processCountText = "共 0 条本地运行记录";

    [ObservableProperty]
    private string _statusText = "当前显示本地运行记录，不执行远程启动、停止或刷新。";

    public RuntimePageViewModel(
        IRuntimeRecordService runtimeRecordService,
        IDeploymentRecordService deploymentRecordService)
        : base("运行", "查看 FRP 进程状态记录，并预留启动、停止、重启操作")
    {
        _runtimeRecordService = runtimeRecordService;
        _deploymentRecordService = deploymentRecordService;
        Processes = [];
        DeploymentSteps = [];

        _ = LoadRuntimeProcessesAsync();
        _ = LoadDeploymentRecordsAsync();
    }

    public ObservableCollection<RuntimeProcess> Processes { get; }

    public ObservableCollection<RuntimeStepViewModel> DeploymentSteps { get; }

    [RelayCommand]
    public async Task LoadRuntimeProcessesAsync(CancellationToken cancellationToken = default)
    {
        var processes = await _runtimeRecordService.ListRuntimeProcessesAsync(cancellationToken);

        if (processes.Count == 0)
        {
            processes = CreateSeedProcesses();

            foreach (var process in processes)
            {
                await _runtimeRecordService.SaveRuntimeProcessAsync(process, cancellationToken);
            }
        }

        Processes.Clear();
        foreach (var process in processes)
        {
            Processes.Add(process);
        }

        SelectedProcess = Processes.FirstOrDefault();
        ProcessCountText = $"共 {Processes.Count} 条本地运行记录";
        StatusText = "已加载本地运行记录。远程控制仍为 Phase 3 能力。";
    }

    [RelayCommand]
    public async Task LoadDeploymentRecordsAsync(CancellationToken cancellationToken = default)
    {
        var records = await _deploymentRecordService.ListDeploymentRecordsAsync(cancellationToken);

        if (records.Count == 0)
        {
            records = CreateSeedDeploymentRecords();

            foreach (var record in records)
            {
                await _deploymentRecordService.SaveDeploymentRecordAsync(record, cancellationToken);
            }
        }

        DeploymentSteps.Clear();
        foreach (var record in records)
        {
            DeploymentSteps.Add(new RuntimeStepViewModel(record.StepName, record.Description, record.Status));
        }
    }

    private static IReadOnlyList<RuntimeProcess> CreateSeedProcesses()
    {
        return
        [
            new("frps-main", "Web-Server-HK", "frps", FrpNexusStatus.Running, "14022", "4d 12h 30m", "0.0.0.0:7000"),
            new("frpc-web", "Web-Server-HK", "frpc", FrpNexusStatus.Running, "14090", "4d 10h 12m", "127.0.0.1:8080"),
            new("frpc-db", "DB-Node-SH", "frpc", FrpNexusStatus.Stopped, "-", "-", "127.0.0.1:3306"),
            new("frpc-edge", "Edge-Router-BJ", "frpc", FrpNexusStatus.Error, "-", "连接失败", "127.0.0.1:7777")
        ];
    }

    private static IReadOnlyList<DeploymentRecord> CreateSeedDeploymentRecords()
    {
        var updatedAt = DateTimeOffset.UtcNow;

        return
        [
            new("测试 SSH 连接", "Web-Server-HK", "确认远程 Linux 节点凭据可用", FrpNexusStatus.Ready, updatedAt),
            new("下载 FRP Release", "Web-Server-HK", "选择适合目标系统的 frpc / frps", FrpNexusStatus.Pending, updatedAt.AddSeconds(-1)),
            new("通过 SFTP 上传核心", "Web-Server-HK", "上传二进制文件与 TOML 配置", FrpNexusStatus.Pending, updatedAt.AddSeconds(-2)),
            new("启动远程进程", "Web-Server-HK", "执行启动命令并读取状态", FrpNexusStatus.Pending, updatedAt.AddSeconds(-3))
        ];
    }
}
