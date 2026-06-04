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
