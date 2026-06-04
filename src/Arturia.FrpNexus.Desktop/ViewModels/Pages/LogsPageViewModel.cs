using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed partial class LogsPageViewModel : PageViewModel
{
    private readonly INodeManagementService _nodeManagementService;
    private readonly IRemoteLogService _remoteLogService;

    [ObservableProperty]
    private string _selectedNodeName = "Web-Server-HK";

    [ObservableProperty]
    private string _processName = "frpc";

    [ObservableProperty]
    private string _remoteLogPath = "/tmp/frpnexus-frpc.log";

    [ObservableProperty]
    private string _selectedSshAuthenticationMode = "SessionPassword";

    [ObservableProperty]
    private string _sshPrivateKeyPath = string.Empty;

    [ObservableProperty]
    private string _sshSessionPassword = string.Empty;

    [ObservableProperty]
    private string _sshPrivateKeyPassphrase = string.Empty;

    [ObservableProperty]
    private string _statusText = "当前显示静态日志样例。";

    [ObservableProperty]
    private bool _isReadingRemoteLogs;

    public LogsPageViewModel(INodeManagementService nodeManagementService, IRemoteLogService remoteLogService)
        : base("日志", "筛选、搜索并查看远程 FRP 日志输出")
    {
        _nodeManagementService = nodeManagementService;
        _remoteLogService = remoteLogService;
        Logs =
        [
            new("2026-06-04 14:32:01.102", "INFO", "Web-Server-HK", "frpc", "FrpNexus client daemon started successfully. Version: V2.4.0", FrpNexusStatus.Ready),
            new("2026-06-04 14:32:01.150", "INFO", "Web-Server-HK", "frpc", "Reading configuration from /etc/frp/frpc.toml", FrpNexusStatus.Ready),
            new("2026-06-04 14:32:02.045", "INFO", "Web-Server-HK", "frpc", "Connection to control server established.", FrpNexusStatus.Online),
            new("2026-06-04 14:35:12.880", "WARN", "DB-Node-SH", "frpc", "Proxy [db_backup_sync] connection timeout. Retrying in 5 seconds...", FrpNexusStatus.Warning),
            new("2026-06-04 14:35:28.105", "ERROR", "DB-Node-SH", "frpc", "Failed to establish proxy [db_backup_sync]. Reason: remote server closed connection unexpectedly. EOF.", FrpNexusStatus.Error),
            new("2026-06-04 14:36:00.001", "INFO", "Web-Server-HK", "frpc", "Heartbeat sent to control server. Latency: 42ms.", FrpNexusStatus.Ready)
        ];
    }

    public ObservableCollection<LogEntry> Logs { get; }

    [RelayCommand]
    private async Task ReadRemoteLogsAsync(CancellationToken cancellationToken = default)
    {
        var request = await TryCreateRequestAsync(cancellationToken);
        if (request is null)
        {
            return;
        }

        IsReadingRemoteLogs = true;
        StatusText = $"正在读取 {request.Node.Name} 的远程日志...";

        try
        {
            var logs = await _remoteLogService.ReadRecentLogsAsync(request, cancellationToken);
            Logs.Clear();
            foreach (var log in logs)
            {
                Logs.Add(log);
            }

            StatusText = $"已读取 {Logs.Count} 行远程日志。";
        }
        catch (OperationCanceledException)
        {
            StatusText = "远程日志读取已取消。";
        }
        finally
        {
            ClearSessionSecrets();
            IsReadingRemoteLogs = false;
        }
    }

    [RelayCommand]
    private void ClearLogs()
    {
        Logs.Clear();
        StatusText = "日志面板已清空。";
    }

    private async Task<RemoteLogReadRequest?> TryCreateRequestAsync(CancellationToken cancellationToken)
    {
        var node = await _nodeManagementService.GetNodeAsync(SelectedNodeName, cancellationToken);
        if (node is null)
        {
            StatusText = $"未找到节点 {SelectedNodeName}，请先在节点页保存节点资料。";
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

        return new RemoteLogReadRequest(
            node,
            credential,
            string.IsNullOrWhiteSpace(ProcessName) ? "frpc" : ProcessName.Trim(),
            string.IsNullOrWhiteSpace(RemoteLogPath) ? "/tmp/frpnexus-frpc.log" : RemoteLogPath.Trim());
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
