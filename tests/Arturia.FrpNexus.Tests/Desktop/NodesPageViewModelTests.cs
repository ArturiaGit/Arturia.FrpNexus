using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class NodesPageViewModelTests
{
    [Fact]
    public async Task LoadNodesAsync_ShouldPopulateNodesFromService()
    {
        var service = new FakeNodeManagementService(
        [
            new("本地测试节点", "127.0.0.1", 22, "deploy", "密钥 (LOCAL_TEST)", "Ubuntu 22.04 LTS", FrpNexusStatus.Online, FrpNexusStatus.Running, "v0.61.1", "1h", "/etc/frp/frpc.toml")
        ]);
        var viewModel = new NodesPageViewModel(service, new FakeSshConnectionService());

        await viewModel.LoadNodesAsync();

        Assert.Single(viewModel.Nodes);
        Assert.Single(viewModel.NodeRows);
        Assert.Equal("本地测试节点", viewModel.SelectedNode?.Name);
        Assert.True(viewModel.NodeRows.Single().IsSelected);
        Assert.Equal("共 1 个节点", viewModel.NodeCountText);
    }

    [Fact]
    public async Task LoadNodesAsync_ShouldSeedSafeSampleNodesWhenDatabaseIsEmpty()
    {
        var service = new FakeNodeManagementService([]);
        var viewModel = new NodesPageViewModel(service, new FakeSshConnectionService());

        await viewModel.LoadNodesAsync();

        Assert.Equal(3, viewModel.Nodes.Count);
        Assert.Equal(3, viewModel.NodeRows.Count);
        Assert.Equal("共 3 个节点", viewModel.NodeCountText);
        Assert.NotNull(viewModel.SelectedNode);
        Assert.Equal("未安装", viewModel.NodeRows.Single(row => row.Name == "Edge-Router-BJ").FrpServiceText);
        Assert.DoesNotContain(service.SavedNodes, node => node.Authentication.Contains("密码", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(service.SavedNodes, node => node.Authentication.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(service.SavedNodes, node => node.Authentication.Contains("私钥内容", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SelectNodeCommand_ShouldUpdateSelectedRowState()
    {
        var service = new FakeNodeManagementService(
        [
            new("节点-A", "203.0.113.10", 22, "deploy", "密钥 (A)", "Ubuntu 22.04 LTS", FrpNexusStatus.Online, FrpNexusStatus.Running, "v0.61.1", "1h", "/etc/frp/frpc.toml"),
            new("节点-B", "203.0.113.11", 22, "deploy", "密钥 (B)", "Debian 12", FrpNexusStatus.Offline, FrpNexusStatus.Stopped, "-", "-", "/etc/frp/frpc.toml")
        ]);
        var viewModel = new NodesPageViewModel(service, new FakeSshConnectionService());
        await viewModel.LoadNodesAsync();

        viewModel.SelectNodeCommand.Execute(viewModel.NodeRows[1]);

        Assert.Equal("节点-B", viewModel.SelectedNode?.Name);
        Assert.False(viewModel.NodeRows[0].IsSelected);
        Assert.True(viewModel.NodeRows[1].IsSelected);
        Assert.Equal("未安装", viewModel.NodeRows[1].FrpServiceText);
    }

    [Fact]
    public async Task SaveNodeCommand_ShouldCreateNodeAndRefreshList()
    {
        var service = new FakeNodeManagementService([]);
        var viewModel = new NodesPageViewModel(service, new FakeSshConnectionService());
        await viewModel.LoadNodesAsync();

        viewModel.StartCreateNodeCommand.Execute(null);
        viewModel.FormName = "北京-边缘节点";
        viewModel.FormHost = "203.0.113.20";
        viewModel.FormSshPort = "2222";
        viewModel.FormUserName = "deploy";
        viewModel.FormAuthentication = "密钥 (ID_RSA_BJ)";
        viewModel.FormOperatingSystem = "Debian 12";
        viewModel.FormFrpVersion = "v0.61.1";
        viewModel.FormConfigPath = "/opt/frp/frpc.toml";

        await viewModel.SaveNodeCommand.ExecuteAsync(null);

        Assert.Contains(viewModel.Nodes, node => node.Name == "北京-边缘节点" && node.SshPort == 2222);
        Assert.Equal("北京-边缘节点", viewModel.SelectedNode?.Name);
        Assert.False(viewModel.IsEditorOpen);
    }

    [Fact]
    public async Task SaveNodeCommand_ShouldApplyDefaultsWhenOptionalFieldsAreEmpty()
    {
        var service = new FakeNodeManagementService([]);
        var viewModel = new NodesPageViewModel(service, new FakeSshConnectionService());
        await viewModel.LoadNodesAsync();

        viewModel.StartCreateNodeCommand.Execute(null);
        viewModel.FormName = "默认值节点";
        viewModel.FormHost = "203.0.113.24";

        await viewModel.SaveNodeCommand.ExecuteAsync(null);

        var saved = Assert.Single(service.SavedNodes.Where(node => node.Name == "默认值节点"));
        Assert.Equal(NodesPageViewModel.DefaultUserName, saved.UserName);
        Assert.Equal(22, saved.SshPort);
        Assert.Equal(NodesPageViewModel.DefaultAuthentication, saved.Authentication);
        Assert.Equal(NodesPageViewModel.DefaultOperatingSystem, saved.OperatingSystem);
        Assert.Equal(NodesPageViewModel.DefaultFrpVersion, saved.FrpVersion);
        Assert.Equal(NodesPageViewModel.DefaultConfigPath, saved.ConfigPath);
    }

    [Fact]
    public async Task SaveNodeCommand_ShouldUseCustomOperatingSystemWhenOtherIsSelected()
    {
        var service = new FakeNodeManagementService([]);
        var viewModel = new NodesPageViewModel(service, new FakeSshConnectionService());
        await viewModel.LoadNodesAsync();

        viewModel.StartCreateNodeCommand.Execute(null);
        viewModel.FormName = "自定义系统节点";
        viewModel.FormHost = "203.0.113.25";
        viewModel.SelectedOperatingSystem = NodesPageViewModel.OtherOperatingSystemOption;
        viewModel.CustomOperatingSystem = "OpenWrt 23.05";

        await viewModel.SaveNodeCommand.ExecuteAsync(null);

        var saved = Assert.Single(service.SavedNodes.Where(node => node.Name == "自定义系统节点"));
        Assert.Equal("OpenWrt 23.05", saved.OperatingSystem);
    }

    [Fact]
    public async Task SaveNodeCommand_ShouldFallbackCustomOperatingSystemToLinuxWhenEmpty()
    {
        var service = new FakeNodeManagementService([]);
        var viewModel = new NodesPageViewModel(service, new FakeSshConnectionService());
        await viewModel.LoadNodesAsync();

        viewModel.StartCreateNodeCommand.Execute(null);
        viewModel.FormName = "其他系统空值节点";
        viewModel.FormHost = "203.0.113.26";
        viewModel.SelectedOperatingSystem = NodesPageViewModel.OtherOperatingSystemOption;

        await viewModel.SaveNodeCommand.ExecuteAsync(null);

        var saved = Assert.Single(service.SavedNodes.Where(node => node.Name == "其他系统空值节点"));
        Assert.Equal("Linux", saved.OperatingSystem);
    }

    [Fact]
    public async Task SaveNodeCommand_ShouldUpdateExistingNode()
    {
        var service = new FakeNodeManagementService(
        [
            new("上海-生产节点", "203.0.113.21", 22, "deploy", "密钥 (ID_RSA_SH)", "Ubuntu 22.04 LTS", FrpNexusStatus.Online, FrpNexusStatus.Running, "v0.61.1", "2h", "/etc/frp/frpc.toml")
        ]);
        var viewModel = new NodesPageViewModel(service, new FakeSshConnectionService());
        await viewModel.LoadNodesAsync();

        viewModel.SelectedNode = viewModel.Nodes.Single();
        viewModel.StartEditSelectedNodeCommand.Execute(null);
        viewModel.FormHost = "203.0.113.88";
        viewModel.FormSshPort = "22022";
        viewModel.FormConfigPath = "/opt/frpnexus/frpc.toml";

        await viewModel.SaveNodeCommand.ExecuteAsync(null);

        var node = Assert.Single(viewModel.Nodes);
        Assert.Equal("203.0.113.88", node.Host);
        Assert.Equal(22022, node.SshPort);
        Assert.Equal("/opt/frpnexus/frpc.toml", node.ConfigPath);
    }

    [Fact]
    public async Task DeleteSelectedNodeCommand_ShouldRequireConfirmationAndDelete()
    {
        var service = new FakeNodeManagementService(
        [
            new("待删除节点", "203.0.113.30", 22, "deploy", "密钥 (LOCAL_KEY)", "Ubuntu 22.04 LTS", FrpNexusStatus.Offline, FrpNexusStatus.Stopped, "v0.61.1", "-", "/etc/frp/frpc.toml")
        ]);
        var viewModel = new NodesPageViewModel(service, new FakeSshConnectionService());
        await viewModel.LoadNodesAsync();
        viewModel.SelectedNode = viewModel.Nodes.Single();

        await viewModel.DeleteSelectedNodeCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Nodes);
        Assert.Equal("确认删除", viewModel.DeleteButtonText);

        await viewModel.DeleteSelectedNodeCommand.ExecuteAsync(null);

        Assert.DoesNotContain(viewModel.Nodes, node => node.Name == "待删除节点");
        Assert.Equal("删除", viewModel.DeleteButtonText);
    }

    [Theory]
    [InlineData("", "203.0.113.40", "deploy", "22", "节点名称不能为空。")]
    [InlineData("测试节点", "", "deploy", "22", "Host 不能为空。")]
    [InlineData("测试节点", "203.0.113.40", "deploy", "70000", "SSH 端口必须是 1 到 65535 之间的数字。")]
    public async Task SaveNodeCommand_ShouldRejectInvalidForm(string name, string host, string userName, string port, string expectedError)
    {
        var service = new FakeNodeManagementService([]);
        var viewModel = new NodesPageViewModel(service, new FakeSshConnectionService());
        viewModel.StartCreateNodeCommand.Execute(null);
        viewModel.FormName = name;
        viewModel.FormHost = host;
        viewModel.FormUserName = userName;
        viewModel.FormSshPort = port;

        await viewModel.SaveNodeCommand.ExecuteAsync(null);

        Assert.Equal(expectedError, viewModel.FormErrorText);
        Assert.DoesNotContain(viewModel.Nodes, node => string.IsNullOrWhiteSpace(node.Name));
    }

    [Fact]
    public async Task SaveNodeCommand_ShouldNotIntroduceSensitiveCredentialFields()
    {
        var service = new FakeNodeManagementService([]);
        var viewModel = new NodesPageViewModel(service, new FakeSshConnectionService());
        viewModel.StartCreateNodeCommand.Execute(null);
        viewModel.FormName = "安全边界节点";
        viewModel.FormHost = "203.0.113.50";
        viewModel.FormUserName = "deploy";
        viewModel.FormSshPort = "22";
        viewModel.FormAuthentication = "密钥 (ID_RSA_SAFE)";

        await viewModel.SaveNodeCommand.ExecuteAsync(null);

        var saved = Assert.Single(service.SavedNodes.Where(node => node.Name == "安全边界节点"));
        Assert.Equal("密钥 (ID_RSA_SAFE)", saved.Authentication);
        Assert.DoesNotContain("密码", saved.Authentication, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Token", saved.Authentication, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("私钥内容", saved.Authentication, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestSelectedNodeConnectionCommand_ShouldUseSessionCredentialAndClearSecret()
    {
        var service = new FakeNodeManagementService(
        [
            new("连接测试节点", "203.0.113.60", 22, "deploy", "会话密码", "Ubuntu 22.04 LTS", FrpNexusStatus.Offline, FrpNexusStatus.Stopped, "v0.61.1", "-", "/etc/frp/frpc.toml")
        ]);
        var sshService = new FakeSshConnectionService();
        var viewModel = new NodesPageViewModel(service, sshService);
        await viewModel.LoadNodesAsync();
        viewModel.SelectedSshAuthenticationMode = "SessionPassword";
        viewModel.SshSessionPassword = "SESSION_PASSWORD_PLACEHOLDER";

        await viewModel.TestSelectedNodeConnectionCommand.ExecuteAsync(null);

        Assert.Equal("SSH 连接测试成功。", viewModel.ConnectionTestStatusText);
        Assert.Equal(string.Empty, viewModel.SshSessionPassword);
        Assert.Equal(SshAuthenticationMode.SessionPassword, sshService.LastRequest?.Credential.AuthenticationMode);
        Assert.Equal("连接测试节点", sshService.LastRequest?.Node.Name);
        Assert.DoesNotContain(service.SavedNodes, node => node.Authentication.Contains("SESSION_PASSWORD_PLACEHOLDER", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TestSelectedNodeConnectionCommand_ShouldRejectMissingSessionPassword()
    {
        var service = new FakeNodeManagementService(
        [
            new("连接测试节点", "203.0.113.60", 22, "deploy", "会话密码", "Ubuntu 22.04 LTS", FrpNexusStatus.Offline, FrpNexusStatus.Stopped, "v0.61.1", "-", "/etc/frp/frpc.toml")
        ]);
        var sshService = new FakeSshConnectionService();
        var viewModel = new NodesPageViewModel(service, sshService);
        await viewModel.LoadNodesAsync();
        viewModel.SelectedSshAuthenticationMode = "SessionPassword";

        await viewModel.TestSelectedNodeConnectionCommand.ExecuteAsync(null);

        Assert.Equal("请输入本次会话使用的 SSH 密码，密码不会保存到 SQLite。", viewModel.ConnectionTestStatusText);
        Assert.Null(sshService.LastRequest);
    }

    [Fact]
    public async Task LoadNodesAsync_ShouldReportRecoverableFailure()
    {
        var viewModel = new NodesPageViewModel(
            new FailingNodeManagementService("节点数据库不可用"),
            new FakeSshConnectionService());

        await viewModel.LoadNodesAsync();

        Assert.Equal("节点加载失败", viewModel.NodeCountText);
        Assert.Equal("节点列表加载失败，请检查输入、网络或本地数据状态后重试。", viewModel.ConnectionTestStatusText);
        Assert.Empty(viewModel.Nodes);
    }

    [Fact]
    public async Task TestSelectedNodeConnectionCommand_ShouldReportFailureAndClearSecret()
    {
        var service = new FakeNodeManagementService(
        [
            new("连接失败节点", "203.0.113.61", 22, "deploy", "会话密码", "Ubuntu 22.04 LTS", FrpNexusStatus.Offline, FrpNexusStatus.Stopped, "v0.61.1", "-", "/etc/frp/frpc.toml")
        ]);
        var viewModel = new NodesPageViewModel(service, new FailingSshConnectionService());
        await viewModel.LoadNodesAsync();
        viewModel.SelectedSshAuthenticationMode = "SessionPassword";
        viewModel.SshSessionPassword = "SESSION_PASSWORD_PLACEHOLDER";

        await viewModel.TestSelectedNodeConnectionCommand.ExecuteAsync(null);

        Assert.Equal("SSH 连接测试失败，请检查输入、网络或本地数据状态后重试。", viewModel.ConnectionTestStatusText);
        Assert.Equal(string.Empty, viewModel.SshSessionPassword);
        Assert.False(viewModel.IsTestingConnection);
    }

    private sealed class FakeNodeManagementService(IReadOnlyList<NodeProfile> nodes) : INodeManagementService
    {
        private readonly List<NodeProfile> _nodes = [.. nodes];

        public IReadOnlyList<NodeProfile> SavedNodes => _nodes;

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

        public Task UpdateConnectionTestResultAsync(
            string nodeName,
            FrpNexusStatus status,
            DateTimeOffset testedAt,
            CancellationToken cancellationToken = default)
        {
            var index = _nodes.FindIndex(node => node.Name == nodeName);
            if (index >= 0)
            {
                var node = _nodes[index];
                _nodes[index] = node with
                {
                    ConnectionStatus = status,
                    LastConnectionTestedAt = testedAt
                };
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeSshConnectionService : ISshConnectionService
    {
        public SshConnectionTestRequest? LastRequest { get; private set; }

        public Task<SshConnectionTestResult> TestConnectionAsync(SshConnectionTestRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new SshConnectionTestResult(
                request.Node.Name,
                FrpNexusStatus.Online,
                DateTimeOffset.UtcNow,
                "SSH 连接测试成功。"));
        }
    }

    private sealed class FailingNodeManagementService(string message) : INodeManagementService
    {
        public Task<IReadOnlyList<NodeProfile>> ListNodesAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(message);
        }

        public Task<NodeProfile?> GetNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(message);
        }

        public Task SaveNodeAsync(NodeProfile node, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(message);
        }

        public Task DeleteNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(message);
        }

        public Task UpdateConnectionTestResultAsync(
            string nodeName,
            FrpNexusStatus status,
            DateTimeOffset testedAt,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FailingSshConnectionService : ISshConnectionService
    {
        public Task<SshConnectionTestResult> TestConnectionAsync(SshConnectionTestRequest request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("SSH 握手失败");
        }
    }
}
