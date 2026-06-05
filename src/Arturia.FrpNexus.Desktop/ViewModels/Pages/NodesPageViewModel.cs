using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed partial class NodesPageViewModel : PageViewModel
{
    public const string DefaultSshPort = "22";
    public const string DefaultUserName = "deploy";
    public const string DefaultAuthentication = "密钥 (LOCAL_KEY)";
    public const string DefaultOperatingSystem = "Ubuntu 22.04 LTS";
    public const string DefaultFrpVersion = "v0.61.1";
    public const string DefaultConfigPath = "/etc/frp/frpc.toml";
    public const string OtherOperatingSystemOption = "其他";

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
    private bool _isEditingExistingNode;

    [ObservableProperty]
    private string _editorTitle = "节点详情";

    [ObservableProperty]
    private string _formName = string.Empty;

    [ObservableProperty]
    private string _formHost = string.Empty;

    [ObservableProperty]
    private string _formSshPort = string.Empty;

    [ObservableProperty]
    private string _formUserName = string.Empty;

    [ObservableProperty]
    private string _formAuthentication = string.Empty;

    [ObservableProperty]
    private string _formOperatingSystem = DefaultOperatingSystem;

    [ObservableProperty]
    private string _selectedOperatingSystem = DefaultOperatingSystem;

    [ObservableProperty]
    private string _customOperatingSystem = string.Empty;

    [ObservableProperty]
    private bool _isCustomOperatingSystemSelected;

    [ObservableProperty]
    private string _formFrpVersion = string.Empty;

    [ObservableProperty]
    private string _formConfigPath = string.Empty;

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
        NodeRows = [];

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
        catch (Exception ex)
        {
            ConnectionTestStatusText = ViewModelErrorText.ForUser("SSH 连接测试", ex);
        }
        finally
        {
            SshSessionPassword = string.Empty;
            SshPrivateKeyPassphrase = string.Empty;
            IsTestingConnection = false;
        }
    }

    public ObservableCollection<NodeProfile> Nodes { get; }

    public ObservableCollection<NodeListItemViewModel> NodeRows { get; }

    public string DefaultSshPortText => DefaultSshPort;

    public string DefaultUserNameText => DefaultUserName;

    public string DefaultAuthenticationText => DefaultAuthentication;

    public string DefaultFrpVersionText => DefaultFrpVersion;

    public string DefaultConfigPathText => DefaultConfigPath;

    public IReadOnlyList<string> OperatingSystemOptions { get; } =
    [
        DefaultOperatingSystem,
        "Debian 12",
        "CentOS Stream 9",
        "AlmaLinux 9",
        "Rocky Linux 9",
        OtherOperatingSystemOption
    ];

    [RelayCommand]
    public async Task LoadNodesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<NodeProfile> nodes;
        try
        {
            nodes = await _nodeManagementService.ListNodesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            ConnectionTestStatusText = "节点列表加载已取消。";
            return;
        }
        catch (Exception ex)
        {
            NodeCountText = "节点加载失败";
            ConnectionTestStatusText = ViewModelErrorText.ForUser("节点列表加载", ex);
            return;
        }

        if (nodes.Count == 0)
        {
            nodes = CreateSeedNodes();

            foreach (var node in nodes)
            {
                try
                {
                    await _nodeManagementService.SaveNodeAsync(node, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    ConnectionTestStatusText = "节点样例写入已取消。";
                    return;
                }
                catch (Exception ex)
                {
                    NodeCountText = "节点加载失败";
                    ConnectionTestStatusText = ViewModelErrorText.ForUser("节点样例写入", ex);
                    return;
                }
            }
        }

        Nodes.Clear();
        NodeRows.Clear();
        foreach (var node in nodes)
        {
            Nodes.Add(node);
            NodeRows.Add(new NodeListItemViewModel(node));
        }

        SelectedNode = Nodes.FirstOrDefault();
        SyncSelectedRows();
        NodeCountText = $"共 {Nodes.Count} 个节点";
    }

    [RelayCommand]
    private void SelectNode(NodeListItemViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        SelectedNode = row.Node;
        IsEditorOpen = false;
    }

    [RelayCommand]
    private void StartCreateNode()
    {
        _editingOriginalName = null;
        ResetDeleteConfirmation();
        IsEditingExistingNode = false;
        EditorTitle = "添加节点";
        FormName = string.Empty;
        FormHost = string.Empty;
        FormSshPort = string.Empty;
        FormUserName = string.Empty;
        FormAuthentication = string.Empty;
        FormOperatingSystem = DefaultOperatingSystem;
        SelectedOperatingSystem = DefaultOperatingSystem;
        CustomOperatingSystem = string.Empty;
        FormFrpVersion = string.Empty;
        FormConfigPath = string.Empty;
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
        IsEditingExistingNode = true;
        EditorTitle = "编辑节点";
        FormName = SelectedNode.Name;
        FormHost = SelectedNode.Host;
        FormSshPort = SelectedNode.SshPort.ToString();
        FormUserName = SelectedNode.UserName;
        FormAuthentication = SelectedNode.Authentication;
        FormOperatingSystem = SelectedNode.OperatingSystem;
        SetOperatingSystemSelection(SelectedNode.OperatingSystem);
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

        try
        {
            if (!string.IsNullOrWhiteSpace(_editingOriginalName)
                && !string.Equals(_editingOriginalName, node.Name, System.StringComparison.OrdinalIgnoreCase))
            {
                await _nodeManagementService.DeleteNodeAsync(_editingOriginalName);
            }

            await _nodeManagementService.SaveNodeAsync(node);
            await LoadNodesAsync();

            SelectedNode = Nodes.FirstOrDefault(item => item.Name == node.Name);
            IsEditorOpen = false;
            IsEditingExistingNode = false;
            FormErrorText = string.Empty;
            _editingOriginalName = null;
        }
        catch (Exception ex)
        {
            FormErrorText = ViewModelErrorText.ForUser("节点保存", ex);
            IsEditorOpen = true;
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        ResetDeleteConfirmation();
        IsEditorOpen = false;
        IsEditingExistingNode = false;
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

        try
        {
            await _nodeManagementService.DeleteNodeAsync(SelectedNode.Name);
            ResetDeleteConfirmation();
            await LoadNodesAsync();
            IsEditorOpen = false;
            IsEditingExistingNode = false;
            FormErrorText = string.Empty;
        }
        catch (Exception ex)
        {
            FormErrorText = ViewModelErrorText.ForUser("节点删除", ex);
            ResetDeleteConfirmation();
        }
    }

    partial void OnSelectedNodeChanged(NodeProfile? value)
    {
        ResetDeleteConfirmation();
        SyncSelectedRows();
    }

    partial void OnSelectedOperatingSystemChanged(string value)
    {
        IsCustomOperatingSystemSelected = string.Equals(value, OtherOperatingSystemOption, StringComparison.Ordinal);
        if (!IsCustomOperatingSystemSelected)
        {
            CustomOperatingSystem = string.Empty;
            FormOperatingSystem = value;
        }
    }

    private void SyncSelectedRows()
    {
        foreach (var row in NodeRows)
        {
            row.IsSelected = SelectedNode is not null
                && string.Equals(row.Node.Name, SelectedNode.Name, StringComparison.OrdinalIgnoreCase);
        }
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

        var userName = ValueOrDefault(FormUserName, DefaultUserName);
        var sshPortText = ValueOrDefault(FormSshPort, DefaultSshPort);
        if (!int.TryParse(sshPortText, out var sshPort) || sshPort is < 1 or > 65535)
        {
            FormErrorText = "SSH 端口必须是 1 到 65535 之间的数字。";
            return false;
        }

        node = new NodeProfile(
            FormName.Trim(),
            FormHost.Trim(),
            sshPort,
            userName,
            ValueOrDefault(FormAuthentication, DefaultAuthentication),
            ResolveOperatingSystem(),
            FrpNexusStatus.Offline,
            FrpNexusStatus.Stopped,
            ValueOrDefault(FormFrpVersion, DefaultFrpVersion),
            "-",
            ValueOrDefault(FormConfigPath, DefaultConfigPath));

        return true;
    }

    private void SetOperatingSystemSelection(string operatingSystem)
    {
        if (OperatingSystemOptions.Contains(operatingSystem))
        {
            SelectedOperatingSystem = operatingSystem;
            CustomOperatingSystem = string.Empty;
            FormOperatingSystem = operatingSystem;
            return;
        }

        SelectedOperatingSystem = OtherOperatingSystemOption;
        CustomOperatingSystem = operatingSystem;
        FormOperatingSystem = operatingSystem;
    }

    private string ResolveOperatingSystem()
    {
        if (string.Equals(SelectedOperatingSystem, OtherOperatingSystemOption, StringComparison.Ordinal))
        {
            return ValueOrDefault(CustomOperatingSystem, "Linux");
        }

        return ValueOrDefault(SelectedOperatingSystem, DefaultOperatingSystem);
    }

    private static string ValueOrDefault(string value, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
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

public sealed partial class NodeListItemViewModel : ObservableObject
{
    public NodeListItemViewModel(NodeProfile node)
    {
        Node = node;
    }

    public NodeProfile Node { get; }

    [ObservableProperty]
    private bool _isSelected;

    public string Name => Node.Name;

    public string Host => Node.Host;

    public string Version => string.IsNullOrWhiteSpace(Node.FrpVersion) ? "-" : Node.FrpVersion;

    public string ConnectionStatusText => Node.ConnectionStatus == FrpNexusStatus.Online ? "在线" : "离线";

    public bool IsConnectionSuccess => Node.ConnectionStatus == FrpNexusStatus.Online;

    public bool IsConnectionError => !IsConnectionSuccess;

    public string FrpServiceText
    {
        get
        {
            if (Version == "-")
            {
                return "未安装";
            }

            return Node.FrpStatus switch
            {
                FrpNexusStatus.Running => "运行中",
                FrpNexusStatus.Stopped => "已停止",
                FrpNexusStatus.Warning => "警告",
                FrpNexusStatus.Error => "异常",
                _ => "未知"
            };
        }
    }

    public bool IsFrpSuccess => Node.FrpStatus == FrpNexusStatus.Running && Version != "-";

    public bool IsFrpWarning => Node.FrpStatus == FrpNexusStatus.Stopped && Version != "-";

    public bool IsFrpError => Node.FrpStatus == FrpNexusStatus.Error;

    public bool IsFrpNeutral => !IsFrpSuccess && !IsFrpWarning && !IsFrpError;
}
