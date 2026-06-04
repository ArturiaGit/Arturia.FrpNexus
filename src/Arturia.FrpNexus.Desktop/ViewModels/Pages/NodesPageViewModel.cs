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

public sealed partial class NodesPageViewModel : PageViewModel
{
    private readonly INodeManagementService _nodeManagementService;
    private readonly ISshConnectionService _sshConnectionService;
    private bool _isDeleteConfirmationPending;
    private string? _editingOriginalName;

    [ObservableProperty]
    private NodeProfile? _selectedNode;

    [ObservableProperty]
    private string _nodeCountText = "共 0 个节点";

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private string _editorTitle = "节点详情";

    [ObservableProperty]
    private string _formName = string.Empty;

    [ObservableProperty]
    private string _formHost = string.Empty;

    [ObservableProperty]
    private string _formSshPort = "22";

    [ObservableProperty]
    private string _formUserName = string.Empty;

    [ObservableProperty]
    private string _formAuthentication = "密钥 (LOCAL_KEY)";

    [ObservableProperty]
    private string _formOperatingSystem = "Ubuntu 22.04 LTS";

    [ObservableProperty]
    private string _formFrpVersion = "v0.61.1";

    [ObservableProperty]
    private string _formConfigPath = "/etc/frp/frpc.toml";

    [ObservableProperty]
    private string _formErrorText = string.Empty;

    [ObservableProperty]
    private string _deleteButtonText = "删除";

    [ObservableProperty]
    private string _selectedSshAuthenticationMode = "SessionPassword";

    [ObservableProperty]
    private string _sshPrivateKeyPath = string.Empty;

    [ObservableProperty]
    private string _sshSessionPassword = string.Empty;

    [ObservableProperty]
    private string _sshPrivateKeyPassphrase = string.Empty;

    [ObservableProperty]
    private string _connectionTestStatusText = "尚未测试 SSH 连接。";

    [ObservableProperty]
    private bool _isTestingConnection;

    public NodesPageViewModel(INodeManagementService nodeManagementService, ISshConnectionService sshConnectionService)
        : base("节点管理", "管理远程 Linux 节点并为 SSH/SFTP 工作流预留入口")
    {
        _nodeManagementService = nodeManagementService;
        _sshConnectionService = sshConnectionService;
        Nodes = [];

        _ = LoadNodesAsync();
    }

    [RelayCommand]
    private async Task TestSelectedNodeConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedNode is null)
        {
            ConnectionTestStatusText = "请先选择一个节点。";
            return;
        }

        if (!TryCreateCredentialReference(out var credential))
        {
            return;
        }

        IsTestingConnection = true;
        ConnectionTestStatusText = $"正在测试 {SelectedNode.Name} 的 SSH 连接...";

        try
        {
            var result = await _sshConnectionService.TestConnectionAsync(
                new SshConnectionTestRequest(SelectedNode, credential),
                cancellationToken);

            ConnectionTestStatusText = result.Message;
            await LoadNodesAsync(cancellationToken);
            SelectedNode = Nodes.FirstOrDefault(node => node.Name == result.NodeName) ?? SelectedNode;
        }
        catch (OperationCanceledException)
        {
            ConnectionTestStatusText = "SSH 连接测试已取消。";
        }
        finally
        {
            SshSessionPassword = string.Empty;
            SshPrivateKeyPassphrase = string.Empty;
            IsTestingConnection = false;
        }
    }

    public ObservableCollection<NodeProfile> Nodes { get; }

    [RelayCommand]
    public async Task LoadNodesAsync(CancellationToken cancellationToken = default)
    {
        var nodes = await _nodeManagementService.ListNodesAsync(cancellationToken);

        if (nodes.Count == 0)
        {
            nodes = CreateSeedNodes();

            foreach (var node in nodes)
            {
                await _nodeManagementService.SaveNodeAsync(node, cancellationToken);
            }
        }

        Nodes.Clear();
        foreach (var node in nodes)
        {
            Nodes.Add(node);
        }

        SelectedNode = Nodes.FirstOrDefault();
        NodeCountText = $"共 {Nodes.Count} 个节点";
    }

    [RelayCommand]
    private void StartCreateNode()
    {
        _editingOriginalName = null;
        ResetDeleteConfirmation();
        EditorTitle = "添加节点";
        FormName = string.Empty;
        FormHost = string.Empty;
        FormSshPort = "22";
        FormUserName = "deploy";
        FormAuthentication = "密钥 (LOCAL_KEY)";
        FormOperatingSystem = "Ubuntu 22.04 LTS";
        FormFrpVersion = "v0.61.1";
        FormConfigPath = "/etc/frp/frpc.toml";
        FormErrorText = string.Empty;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void StartEditSelectedNode()
    {
        if (SelectedNode is null)
        {
            FormErrorText = "请先选择一个节点。";
            return;
        }

        _editingOriginalName = SelectedNode.Name;
        ResetDeleteConfirmation();
        EditorTitle = "编辑节点";
        FormName = SelectedNode.Name;
        FormHost = SelectedNode.Host;
        FormSshPort = SelectedNode.SshPort.ToString();
        FormUserName = SelectedNode.UserName;
        FormAuthentication = SelectedNode.Authentication;
        FormOperatingSystem = SelectedNode.OperatingSystem;
        FormFrpVersion = SelectedNode.FrpVersion;
        FormConfigPath = SelectedNode.ConfigPath;
        FormErrorText = string.Empty;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private async Task SaveNodeAsync()
    {
        ResetDeleteConfirmation();

        if (!TryCreateNode(out var node))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_editingOriginalName)
            && !string.Equals(_editingOriginalName, node.Name, System.StringComparison.OrdinalIgnoreCase))
        {
            await _nodeManagementService.DeleteNodeAsync(_editingOriginalName);
        }

        await _nodeManagementService.SaveNodeAsync(node);
        await LoadNodesAsync();

        SelectedNode = Nodes.FirstOrDefault(item => item.Name == node.Name);
        IsEditorOpen = false;
        FormErrorText = string.Empty;
        _editingOriginalName = null;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        ResetDeleteConfirmation();
        IsEditorOpen = false;
        FormErrorText = string.Empty;
        _editingOriginalName = null;
    }

    [RelayCommand]
    private async Task DeleteSelectedNodeAsync()
    {
        if (SelectedNode is null)
        {
            FormErrorText = "请先选择一个节点。";
            return;
        }

        if (!_isDeleteConfirmationPending)
        {
            _isDeleteConfirmationPending = true;
            DeleteButtonText = "确认删除";
            FormErrorText = $"将删除本地节点记录 `{SelectedNode.Name}`，再次点击确认。";
            return;
        }

        await _nodeManagementService.DeleteNodeAsync(SelectedNode.Name);
        ResetDeleteConfirmation();
        await LoadNodesAsync();
        IsEditorOpen = false;
        FormErrorText = string.Empty;
    }

    partial void OnSelectedNodeChanged(NodeProfile? value)
    {
        ResetDeleteConfirmation();
    }

    private static IReadOnlyList<NodeProfile> CreateSeedNodes()
    {
        return
        [
            new("Web-Server-HK", "103.114.160.22", 22, "root", "密钥 (ID_RSA_HK)", "Linux x86_64 (Ubuntu 22.04 LTS)", FrpNexusStatus.Online, FrpNexusStatus.Running, "v0.51.3", "4d 12h 30m", "/etc/frp/frpc.toml"),
            new("DB-Node-SH", "47.101.44.112", 22, "deploy", "密钥 (ID_RSA_SH)", "Debian 12", FrpNexusStatus.Online, FrpNexusStatus.Stopped, "v0.51.3", "-", "/opt/frp/frpc.toml"),
            new("Edge-Router-BJ", "123.56.77.89", 2222, "root", "密钥 (ID_RSA_BJ)", "Ubuntu 20.04 LTS", FrpNexusStatus.Offline, FrpNexusStatus.Stopped, "-", "-", "/etc/frp/frpc.toml")
        ];
    }

    private bool TryCreateNode(out NodeProfile node)
    {
        node = new NodeProfile(string.Empty, string.Empty, 22, string.Empty, string.Empty, string.Empty, FrpNexusStatus.Offline, FrpNexusStatus.Stopped, string.Empty, "-", string.Empty);

        if (string.IsNullOrWhiteSpace(FormName))
        {
            FormErrorText = "节点名称不能为空。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(FormHost))
        {
            FormErrorText = "Host 不能为空。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(FormUserName))
        {
            FormErrorText = "用户名不能为空。";
            return false;
        }

        if (!int.TryParse(FormSshPort, out var sshPort) || sshPort is < 1 or > 65535)
        {
            FormErrorText = "SSH 端口必须是 1 到 65535 之间的数字。";
            return false;
        }

        node = new NodeProfile(
            FormName.Trim(),
            FormHost.Trim(),
            sshPort,
            FormUserName.Trim(),
            string.IsNullOrWhiteSpace(FormAuthentication) ? "密钥描述未设置" : FormAuthentication.Trim(),
            string.IsNullOrWhiteSpace(FormOperatingSystem) ? "Linux" : FormOperatingSystem.Trim(),
            FrpNexusStatus.Offline,
            FrpNexusStatus.Stopped,
            string.IsNullOrWhiteSpace(FormFrpVersion) ? "-" : FormFrpVersion.Trim(),
            "-",
            string.IsNullOrWhiteSpace(FormConfigPath) ? "/etc/frp/frpc.toml" : FormConfigPath.Trim());

        return true;
    }

    private bool TryCreateCredentialReference(out SshCredentialReference credential)
    {
        credential = new SshCredentialReference(SshAuthenticationMode.SessionPassword);

        if (!TryParseAuthenticationMode(SelectedSshAuthenticationMode, out var mode))
        {
            ConnectionTestStatusText = "请选择有效的 SSH 认证方式。";
            return false;
        }

        if (mode == SshAuthenticationMode.SessionPassword && string.IsNullOrWhiteSpace(SshSessionPassword))
        {
            ConnectionTestStatusText = "请输入本次会话使用的 SSH 密码，密码不会保存到 SQLite。";
            return false;
        }

        if (mode == SshAuthenticationMode.PrivateKey && string.IsNullOrWhiteSpace(SshPrivateKeyPath))
        {
            ConnectionTestStatusText = "请输入私钥文件路径，私钥内容和 passphrase 不会保存到 SQLite。";
            return false;
        }

        credential = new SshCredentialReference(
            mode,
            string.IsNullOrWhiteSpace(SshPrivateKeyPath) ? null : SshPrivateKeyPath.Trim(),
            string.IsNullOrWhiteSpace(SshSessionPassword) ? null : SshSessionPassword,
            string.IsNullOrWhiteSpace(SshPrivateKeyPassphrase) ? null : SshPrivateKeyPassphrase);

        return true;
    }

    private static bool TryParseAuthenticationMode(string value, out SshAuthenticationMode mode)
    {
        if (string.Equals(value, "PrivateKey", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "私钥文件", System.StringComparison.OrdinalIgnoreCase))
        {
            mode = SshAuthenticationMode.PrivateKey;
            return true;
        }

        if (string.Equals(value, "SshAgent", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "SSH Agent", System.StringComparison.OrdinalIgnoreCase))
        {
            mode = SshAuthenticationMode.SshAgent;
            return true;
        }

        if (string.Equals(value, "SessionPassword", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "会话密码", System.StringComparison.OrdinalIgnoreCase))
        {
            mode = SshAuthenticationMode.SessionPassword;
            return true;
        }

        mode = SshAuthenticationMode.SessionPassword;
        return false;
    }

    private void ResetDeleteConfirmation()
    {
        _isDeleteConfirmationPending = false;
        DeleteButtonText = "删除";
    }
}
