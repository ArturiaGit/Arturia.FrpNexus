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
        var viewModel = new NodesPageViewModel(service);

        await viewModel.LoadNodesAsync();

        Assert.Single(viewModel.Nodes);
        Assert.Equal("本地测试节点", viewModel.SelectedNode?.Name);
        Assert.Equal("共 1 个节点", viewModel.NodeCountText);
    }

    [Fact]
    public async Task LoadNodesAsync_ShouldSeedSafeSampleNodesWhenDatabaseIsEmpty()
    {
        var service = new FakeNodeManagementService([]);
        var viewModel = new NodesPageViewModel(service);

        await viewModel.LoadNodesAsync();

        Assert.Equal(3, viewModel.Nodes.Count);
        Assert.Equal("共 3 个节点", viewModel.NodeCountText);
        Assert.NotNull(viewModel.SelectedNode);
        Assert.DoesNotContain(service.SavedNodes, node => node.Authentication.Contains("密码", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(service.SavedNodes, node => node.Authentication.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(service.SavedNodes, node => node.Authentication.Contains("私钥内容", StringComparison.OrdinalIgnoreCase));
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
    }
}
