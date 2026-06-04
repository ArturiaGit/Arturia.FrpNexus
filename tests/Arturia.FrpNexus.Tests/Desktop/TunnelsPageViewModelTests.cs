using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class TunnelsPageViewModelTests
{
    [Fact]
    public async Task LoadTunnelsAsync_ShouldPopulateTunnelsFromService()
    {
        var service = new FakeTunnelManagementService(
        [
            new("本地 HTTP", TunnelProtocol.Http, "本地测试节点", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Running, "运行中")
        ]);
        var viewModel = new TunnelsPageViewModel(service);

        await viewModel.LoadTunnelsAsync();

        Assert.Single(viewModel.Tunnels);
        Assert.Equal("本地 HTTP", viewModel.Tunnels[0].Name);
        Assert.Equal("共 1 条记录", viewModel.TunnelCountText);
    }

    [Fact]
    public async Task LoadTunnelsAsync_ShouldSeedProtocolExamplesWhenDatabaseIsEmpty()
    {
        var service = new FakeTunnelManagementService([]);
        var viewModel = new TunnelsPageViewModel(service);

        await viewModel.LoadTunnelsAsync();

        Assert.Equal(4, viewModel.Tunnels.Count);
        Assert.Equal("共 4 条记录", viewModel.TunnelCountText);
        Assert.Contains(viewModel.Tunnels, tunnel => tunnel.Protocol == TunnelProtocol.Tcp);
        Assert.Contains(viewModel.Tunnels, tunnel => tunnel.Protocol == TunnelProtocol.Udp);
        Assert.Contains(viewModel.Tunnels, tunnel => tunnel.Protocol == TunnelProtocol.Http);
        Assert.Contains(viewModel.Tunnels, tunnel => tunnel.Protocol == TunnelProtocol.Https);
    }

    [Fact]
    public async Task SaveTunnelCommand_ShouldCreateTunnelAndRefreshList()
    {
        var service = new FakeTunnelManagementService([]);
        var viewModel = new TunnelsPageViewModel(service);
        await viewModel.LoadTunnelsAsync();

        viewModel.StartCreateTunnelCommand.Execute(null);
        viewModel.FormName = "api-local";
        viewModel.FormProtocol = TunnelProtocol.Https;
        viewModel.FormNodeName = "Node-Alpha-HK";
        viewModel.FormLocalAddress = "127.0.0.1";
        viewModel.FormLocalPort = "8443";
        viewModel.FormRemoteEndpoint = "api.example.com";
        viewModel.FormStatusDetail = "本地新增";

        await viewModel.SaveTunnelCommand.ExecuteAsync(null);

        Assert.Contains(viewModel.Tunnels, tunnel => tunnel.Name == "api-local" && tunnel.Protocol == TunnelProtocol.Https);
        Assert.Equal("api-local", viewModel.SelectedTunnel?.Name);
        Assert.False(viewModel.IsEditorOpen);
    }

    [Fact]
    public async Task SaveTunnelCommand_ShouldUpdateExistingTunnel()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Stopped, "本地记录")
        ]);
        var viewModel = new TunnelsPageViewModel(service);
        await viewModel.LoadTunnelsAsync();

        viewModel.SelectedTunnel = viewModel.Tunnels.Single();
        viewModel.StartEditSelectedTunnelCommand.Execute(null);
        viewModel.FormLocalPort = "9090";
        viewModel.FormRemoteEndpoint = "new.example.com";

        await viewModel.SaveTunnelCommand.ExecuteAsync(null);

        var tunnel = Assert.Single(viewModel.Tunnels);
        Assert.Equal(9090, tunnel.LocalPort);
        Assert.Equal("new.example.com", tunnel.RemoteEndpoint);
    }

    [Fact]
    public async Task DeleteSelectedTunnelCommand_ShouldRequireConfirmationAndDelete()
    {
        var service = new FakeTunnelManagementService(
        [
            new("web-dev", TunnelProtocol.Http, "Node-Alpha-HK", "127.0.0.1", 8080, "dev.example.com", FrpNexusStatus.Stopped, "本地记录")
        ]);
        var viewModel = new TunnelsPageViewModel(service);
        await viewModel.LoadTunnelsAsync();
        viewModel.SelectedTunnel = viewModel.Tunnels.Single();

        await viewModel.DeleteSelectedTunnelCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Tunnels);
        Assert.Equal("确认删除", viewModel.DeleteButtonText);

        await viewModel.DeleteSelectedTunnelCommand.ExecuteAsync(null);

        Assert.DoesNotContain(viewModel.Tunnels, tunnel => tunnel.Name == "web-dev");
        Assert.Equal("删除", viewModel.DeleteButtonText);
    }

    [Fact]
    public async Task SaveTunnelCommand_ShouldRejectInvalidForm()
    {
        var service = new FakeTunnelManagementService([]);
        var viewModel = new TunnelsPageViewModel(service);
        viewModel.StartCreateTunnelCommand.Execute(null);
        viewModel.FormName = string.Empty;
        viewModel.FormLocalPort = "70000";

        await viewModel.SaveTunnelCommand.ExecuteAsync(null);

        Assert.Equal("隧道名称不能为空。", viewModel.FormErrorText);
        Assert.DoesNotContain(viewModel.Tunnels, tunnel => string.IsNullOrWhiteSpace(tunnel.Name));
    }

    [Fact]
    public async Task SaveTunnelCommand_ShouldSupportAllMvpProtocols()
    {
        var service = new FakeTunnelManagementService([]);
        var viewModel = new TunnelsPageViewModel(service);
        await viewModel.LoadTunnelsAsync();

        foreach (var protocol in new[] { TunnelProtocol.Tcp, TunnelProtocol.Udp, TunnelProtocol.Http, TunnelProtocol.Https })
        {
            viewModel.StartCreateTunnelCommand.Execute(null);
            viewModel.FormName = $"custom-{protocol}";
            viewModel.FormProtocol = protocol;
            viewModel.FormNodeName = "Node-Alpha-HK";
            viewModel.FormLocalAddress = "127.0.0.1";
            viewModel.FormLocalPort = protocol == TunnelProtocol.Tcp ? "22" : "8080";
            viewModel.FormRemoteEndpoint = protocol is TunnelProtocol.Http or TunnelProtocol.Https
                ? $"{protocol}.example.com"
                : "60022";

            await viewModel.SaveTunnelCommand.ExecuteAsync(null);
        }

        Assert.Contains(viewModel.Tunnels, tunnel => tunnel.Name == "custom-Tcp" && tunnel.Protocol == TunnelProtocol.Tcp);
        Assert.Contains(viewModel.Tunnels, tunnel => tunnel.Name == "custom-Udp" && tunnel.Protocol == TunnelProtocol.Udp);
        Assert.Contains(viewModel.Tunnels, tunnel => tunnel.Name == "custom-Http" && tunnel.Protocol == TunnelProtocol.Http);
        Assert.Contains(viewModel.Tunnels, tunnel => tunnel.Name == "custom-Https" && tunnel.Protocol == TunnelProtocol.Https);
    }

    private sealed class FakeTunnelManagementService(IReadOnlyList<TunnelProfile> tunnels) : ITunnelManagementService
    {
        private readonly List<TunnelProfile> _tunnels = [.. tunnels];

        public Task<IReadOnlyList<TunnelProfile>> ListTunnelsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TunnelProfile>>(_tunnels);
        }

        public Task<TunnelProfile?> GetTunnelAsync(string tunnelName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tunnels.FirstOrDefault(tunnel => tunnel.Name == tunnelName));
        }

        public Task SaveTunnelAsync(TunnelProfile tunnel, CancellationToken cancellationToken = default)
        {
            _tunnels.RemoveAll(item => item.Name == tunnel.Name);
            _tunnels.Add(tunnel);
            return Task.CompletedTask;
        }

        public Task DeleteTunnelAsync(string tunnelName, CancellationToken cancellationToken = default)
        {
            _tunnels.RemoveAll(tunnel => tunnel.Name == tunnelName);
            return Task.CompletedTask;
        }
    }
}
