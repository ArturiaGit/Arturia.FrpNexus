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
    private readonly INodeManagementService _nodeManagementService;
    private readonly IRemoteRuntimeService _remoteRuntimeService;

    [ObservableProperty]
    private RuntimeProcess? _selectedProcess;

    [ObservableProperty]
    private string _processCountText = "共 0 条本地运行记录";

    [ObservableProperty]
    private string _statusText = "当前显示本地运行记录，不执行远程启动、停止或刷新。";

    [ObservableProperty]
    private string _selectedSshAuthenticationMode = "SessionPassword";

    [ObservableProperty]
    private string _sshPrivateKeyPath = string.Empty;

    [ObservableProperty]
    private string _sshSessionPassword = string.Empty;

    [ObservableProperty]
    private string _sshPrivateKeyPassphrase = string.Empty;

    [ObservableProperty]
    private string _remoteCommandText = "nohup /opt/frp/frpc -c /etc/frp/frpc.toml >/tmp/frpnexus-frpc.log 2>&1 &";

    [ObservableProperty]
    private bool _isRemoteCommandRunning;

    public RuntimePageViewModel(
        IRuntimeRecordService runtimeRecordService,
        IDeploymentRecordService deploymentRecordService,
        INodeManagementService nodeManagementService,
        IRemoteRuntimeService remoteRuntimeService)
        : base("运行", "查看 FRP 进程状态记录，并预留启动、停止、重启操作")
    {
        _runtimeRecordService = runtimeRecordService;
        _deploymentRecordService = deploymentRecordService;
        _nodeManagementService = nodeManagementService;
        _remoteRuntimeService = remoteRuntimeService;
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
    private async Task RefreshRemoteProcessesAsync(CancellationToken cancellationToken = default)
    {
        var runtimeRequest = await TryCreateRuntimeRequestAsync(cancellationToken);
        if (runtimeRequest is null)
        {
            return;
        }

        var (node, credential) = runtimeRequest.Value;
        IsRemoteCommandRunning = true;
        StatusText = $"正在读取 {node.Name} 的远程 FRP 进程...";

        try
        {
            var processes = await _remoteRuntimeService.GetProcessesAsync(
                new RemoteRuntimeQueryRequest(node, credential),
                cancellationToken);

            Processes.Clear();
            foreach (var process in processes)
            {
                Processes.Add(process);
            }

            SelectedProcess = Processes.FirstOrDefault();
            ProcessCountText = $"共 {Processes.Count} 条远程运行记录";
            StatusText = "已刷新远程进程状态。";
        }
        catch (OperationCanceledException)
        {
            StatusText = "远程刷新已取消。";
        }
        finally
        {
            ClearSessionSecrets();
            IsRemoteCommandRunning = false;
        }
    }

    [RelayCommand]
    private Task StartSelectedProcessAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteSelectedProcessCommandAsync("启动", cancellationToken);
    }

    [RelayCommand]
    private Task StopSelectedProcessAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteSelectedProcessCommandAsync("停止", cancellationToken);
    }

    [RelayCommand]
    private Task RestartSelectedProcessAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteSelectedProcessCommandAsync("重启", cancellationToken);
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

    private async Task ExecuteSelectedProcessCommandAsync(string action, CancellationToken cancellationToken)
    {
        if (SelectedProcess is null)
        {
            StatusText = "请先选择一个运行记录。";
            return;
        }

        var runtimeRequest = await TryCreateRuntimeRequestAsync(cancellationToken);
        if (runtimeRequest is null)
        {
            return;
        }

        var (node, credential) = runtimeRequest.Value;
        if (string.IsNullOrWhiteSpace(RemoteCommandText))
        {
            StatusText = "远程命令不能为空。";
            return;
        }

        IsRemoteCommandRunning = true;
        StatusText = $"正在{action} {SelectedProcess.Name}...";

        try
        {
            var request = new RemoteRuntimeCommandRequest(
                node,
                credential,
                SelectedProcess.Name,
                SelectedProcess.ProcessKind,
                RemoteCommandText.Trim());

            var result = action switch
            {
                "启动" => await _remoteRuntimeService.StartAsync(request, cancellationToken),
                "停止" => await _remoteRuntimeService.StopAsync(request, cancellationToken),
                _ => await _remoteRuntimeService.RestartAsync(request, cancellationToken)
            };

            StatusText = result.Message;
            await LoadRuntimeProcessesAsync(cancellationToken);
            SelectedProcess = Processes.FirstOrDefault(process => process.Name == result.ProcessName) ?? SelectedProcess;
            StatusText = result.Message;
        }
        catch (OperationCanceledException)
        {
            StatusText = $"远程{action}已取消。";
        }
        finally
        {
            ClearSessionSecrets();
            IsRemoteCommandRunning = false;
        }
    }

    private async Task<(NodeProfile Node, SshCredentialReference Credential)?> TryCreateRuntimeRequestAsync(CancellationToken cancellationToken)
    {
        if (SelectedProcess is null)
        {
            StatusText = "请先选择一个运行记录。";
            return null;
        }

        var matchingNode = await _nodeManagementService.GetNodeAsync(SelectedProcess.NodeName, cancellationToken);
        if (matchingNode is null)
        {
            StatusText = $"未找到节点 {SelectedProcess.NodeName}，请先在节点页保存节点资料。";
            return null;
        }

        if (!TryParseAuthenticationMode(SelectedSshAuthenticationMode, out var mode))
        {
            StatusText = "请选择有效的 SSH 认证方式。";
            return null;
        }

        if (mode == SshAuthenticationMode.SessionPassword && string.IsNullOrWhiteSpace(SshSessionPassword))
        {
            StatusText = "请输入本次会话使用的 SSH 密码，密码不会保存到 SQLite。";
            return null;
        }

        if (mode == SshAuthenticationMode.PrivateKey && string.IsNullOrWhiteSpace(SshPrivateKeyPath))
        {
            StatusText = "请输入私钥文件路径，私钥内容和 passphrase 不会保存到 SQLite。";
            return null;
        }

        var credential = new SshCredentialReference(
            mode,
            string.IsNullOrWhiteSpace(SshPrivateKeyPath) ? null : SshPrivateKeyPath.Trim(),
            string.IsNullOrWhiteSpace(SshSessionPassword) ? null : SshSessionPassword,
            string.IsNullOrWhiteSpace(SshPrivateKeyPassphrase) ? null : SshPrivateKeyPassphrase);

        return (matchingNode, credential);
    }

    private static bool TryParseAuthenticationMode(string value, out SshAuthenticationMode mode)
    {
        if (string.Equals(value, "PrivateKey", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "私钥文件", StringComparison.OrdinalIgnoreCase))
        {
            mode = SshAuthenticationMode.PrivateKey;
            return true;
        }

        if (string.Equals(value, "SshAgent", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "SSH Agent", StringComparison.OrdinalIgnoreCase))
        {
            mode = SshAuthenticationMode.SshAgent;
            return true;
        }

        if (string.Equals(value, "SessionPassword", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "会话密码", StringComparison.OrdinalIgnoreCase))
        {
            mode = SshAuthenticationMode.SessionPassword;
            return true;
        }

        mode = SshAuthenticationMode.SessionPassword;
        return false;
    }

    private void ClearSessionSecrets()
    {
        SshSessionPassword = string.Empty;
        SshPrivateKeyPassphrase = string.Empty;
    }
}
