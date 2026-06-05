using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Application.Configuration;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class ConfigurationsPageViewModelTests
{
    [Fact]
    public void Constructor_ShouldGenerateInitialTomlPreview()
    {
        var viewModel = CreateViewModel();

        Assert.Contains("[[proxies]]", viewModel.TomlPreview);
        Assert.Contains("type = \"http\"", viewModel.TomlPreview);
        Assert.Contains("customDomains = [\"example.com\"]", viewModel.TomlPreview);
        Assert.Contains(viewModel.TomlPreviewLines, line => line.Tokens.Any(token => token.Kind == TomlPreviewTokenKind.Section));
        Assert.Contains(viewModel.TomlPreviewLines, line => line.Tokens.Any(token => token.Kind == TomlPreviewTokenKind.Key));
        Assert.Contains(viewModel.TomlPreviewLines, line => line.Tokens.Any(token => token.Kind == TomlPreviewTokenKind.String));
        Assert.Contains(viewModel.TomlPreviewLines, line => line.Tokens.Any(token => token.Kind == TomlPreviewTokenKind.Number));
        Assert.Equal("已生成本地 TOML 预览，尚未上传到远程节点。", viewModel.StatusText);
    }

    [Fact]
    public void GenerateTomlCommand_ShouldGenerateTcpRemotePortToml()
    {
        var viewModel = CreateViewModel();
        viewModel.ProxyName = "ssh_bastion";
        viewModel.SelectedProtocol = TunnelProtocol.Tcp;
        viewModel.LocalAddress = "127.0.0.1";
        viewModel.LocalPort = "22";
        viewModel.RemoteEndpoint = "60022";

        viewModel.GenerateTomlCommand.Execute(null);

        Assert.Contains("name = \"ssh_bastion\"", viewModel.TomlPreview);
        Assert.Contains("type = \"tcp\"", viewModel.TomlPreview);
        Assert.Contains("remotePort = 60022", viewModel.TomlPreview);
        Assert.DoesNotContain("customDomains", viewModel.TomlPreview);
        Assert.Equal(string.Empty, viewModel.ErrorText);
    }

    [Fact]
    public void TomlPreviewLines_ShouldRefreshWhenTomlPreviewChanges()
    {
        var viewModel = CreateViewModel();

        viewModel.TomlPreview = """
        [[proxies]]
        name = "manual_proxy"
        localPort = 9000
        # comment
        """;

        Assert.Equal(4, viewModel.TomlPreviewLines.Count);
        Assert.Contains(viewModel.TomlPreviewLines[1].Tokens, token => token.Text == "manual_proxy" || token.Text == "\"manual_proxy\"");
        Assert.Contains(viewModel.TomlPreviewLines[2].Tokens, token => token.Kind == TomlPreviewTokenKind.Number && token.Text == "9000");
        Assert.Contains(viewModel.TomlPreviewLines[3].Tokens, token => token.Kind == TomlPreviewTokenKind.Section);
    }

    [Fact]
    public void ToggleAdvancedOptionsCommand_ShouldToggleAdvancedOptions()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.IsAdvancedOptionsVisible);
        Assert.Equal("chevron_right", viewModel.AdvancedOptionsChevronIcon);
        Assert.Equal(0, viewModel.AdvancedOptionsPanelMaxHeight);
        Assert.Equal(0, viewModel.AdvancedOptionsPanelOpacity);
        Assert.Equal(-4, viewModel.AdvancedOptionsPanelOffsetY);
        Assert.False(viewModel.IsAdvancedOptionsInteractive);

        viewModel.ToggleAdvancedOptionsCommand.Execute(null);

        Assert.True(viewModel.IsAdvancedOptionsVisible);
        Assert.Equal("chevron_down", viewModel.AdvancedOptionsChevronIcon);
        Assert.Equal(112, viewModel.AdvancedOptionsPanelMaxHeight);
        Assert.Equal(1, viewModel.AdvancedOptionsPanelOpacity);
        Assert.Equal(0, viewModel.AdvancedOptionsPanelOffsetY);
        Assert.True(viewModel.IsAdvancedOptionsInteractive);

        viewModel.ToggleAdvancedOptionsCommand.Execute(null);

        Assert.False(viewModel.IsAdvancedOptionsVisible);
        Assert.Equal("chevron_right", viewModel.AdvancedOptionsChevronIcon);
        Assert.Equal(0, viewModel.AdvancedOptionsPanelMaxHeight);
        Assert.Equal(0, viewModel.AdvancedOptionsPanelOpacity);
        Assert.Equal(-4, viewModel.AdvancedOptionsPanelOffsetY);
        Assert.False(viewModel.IsAdvancedOptionsInteractive);
    }

    [Fact]
    public void GenerateTomlCommand_ShouldRejectInvalidLocalPort()
    {
        var viewModel = CreateViewModel();
        viewModel.LocalPort = "70000";

        viewModel.GenerateTomlCommand.Execute(null);

        Assert.Equal("本地端口必须是 1 到 65535 之间的数字。", viewModel.ErrorText);
        Assert.Equal("TOML 生成失败。", viewModel.StatusText);
    }

    [Fact]
    public async Task ValidateTomlCommand_ShouldReportValidationSuccess()
    {
        var viewModel = CreateViewModel();

        await viewModel.ValidateTomlCommand.ExecuteAsync(null);

        Assert.Equal("TOML 本地校验通过。", viewModel.StatusText);
        Assert.Equal(string.Empty, viewModel.ErrorText);
    }

    [Fact]
    public async Task ValidateTomlCommand_ShouldReportValidationFailure()
    {
        var viewModel = CreateViewModel();
        viewModel.TomlPreview = "[[proxies]]";

        await viewModel.ValidateTomlCommand.ExecuteAsync(null);

        Assert.Equal("TOML 本地校验失败。", viewModel.StatusText);
        Assert.Contains("缺少必要字段", viewModel.ErrorText);
    }

    [Fact]
    public async Task SaveConfigurationCommand_ShouldPersistAndRefreshLocalConfigurations()
    {
        var service = new FakeConfigurationVersionService([]);
        var viewModel = CreateViewModel(service);
        viewModel.ProxyName = "api_https";
        viewModel.SelectedProtocol = TunnelProtocol.Https;
        viewModel.LocalAddress = "127.0.0.1";
        viewModel.LocalPort = "8443";
        viewModel.RemoteEndpoint = "api.example.com";
        viewModel.GenerateTomlCommand.Execute(null);

        await viewModel.SaveConfigurationCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Configurations);
        Assert.Equal("api_https", viewModel.SelectedConfiguration?.Name);
        Assert.Contains("type = \"https\"", viewModel.SelectedConfiguration?.Toml);
        Assert.Equal("共 1 个本地配置", viewModel.ConfigurationCountText);
        Assert.Contains("已保存本地配置", viewModel.StatusText);
    }

    [Fact]
    public async Task LoadConfigurationsAsync_ShouldLoadExistingLocalConfiguration()
    {
        var existing = new ConfigurationVersion(
            "saved_tcp",
            TunnelProtocol.Tcp,
            "127.0.0.1",
            22,
            "60022",
            """
            [[proxies]]
            name = "saved_tcp"
            type = "tcp"
            localIP = "127.0.0.1"
            localPort = 22
            remotePort = 60022
            """,
            DateTimeOffset.UtcNow);
        var viewModel = CreateViewModel(new FakeConfigurationVersionService([existing]));

        await viewModel.LoadConfigurationsCommand.ExecuteAsync(null);

        Assert.Equal("saved_tcp", viewModel.ProxyName);
        Assert.Equal(TunnelProtocol.Tcp, viewModel.SelectedProtocol);
        Assert.Equal("60022", viewModel.RemoteEndpoint);
        Assert.Contains("remotePort = 60022", viewModel.TomlPreview);
        Assert.Contains(viewModel.TomlPreviewLines, line => line.Tokens.Any(token => token.Kind == TomlPreviewTokenKind.Number && token.Text == "60022"));
    }

    [Fact]
    public async Task LoadConfigurationsAsync_ShouldReportRecoverableFailure()
    {
        var viewModel = CreateViewModel(new FailingConfigurationVersionService());

        await viewModel.LoadConfigurationsCommand.ExecuteAsync(null);

        Assert.Equal("配置加载失败", viewModel.ConfigurationCountText);
        Assert.Equal("本地配置加载失败，请检查输入、网络或本地数据状态后重试。", viewModel.StatusText);
    }

    [Fact]
    public async Task SaveConfigurationCommand_ShouldReportRecoverableFailure()
    {
        var viewModel = CreateViewModel(new FailingConfigurationVersionService());
        viewModel.ProxyName = "api_https";
        viewModel.SelectedProtocol = TunnelProtocol.Https;
        viewModel.LocalAddress = "127.0.0.1";
        viewModel.LocalPort = "8443";
        viewModel.RemoteEndpoint = "api.example.com";
        viewModel.GenerateTomlCommand.Execute(null);

        await viewModel.SaveConfigurationCommand.ExecuteAsync(null);

        Assert.Equal("保存配置失败。", viewModel.StatusText);
        Assert.Equal("保存配置失败，请检查输入、网络或本地数据状态后重试。", viewModel.ErrorText);
    }

    private static ConfigurationsPageViewModel CreateViewModel()
    {
        return CreateViewModel(new FakeConfigurationVersionService([]));
    }

    private static ConfigurationsPageViewModel CreateViewModel(IConfigurationVersionService configurationVersionService)
    {
        return new ConfigurationsPageViewModel(new TomlConfigurationService(), configurationVersionService);
    }

    private sealed class FakeConfigurationVersionService(IReadOnlyList<ConfigurationVersion> configurations)
        : IConfigurationVersionService
    {
        private readonly List<ConfigurationVersion> _configurations = [.. configurations];

        public Task<IReadOnlyList<ConfigurationVersion>> ListConfigurationsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ConfigurationVersion>>(_configurations.OrderByDescending(item => item.UpdatedAt).ToArray());
        }

        public Task<ConfigurationVersion?> GetConfigurationAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_configurations.FirstOrDefault(item => item.Name == name));
        }

        public Task SaveConfigurationAsync(ConfigurationVersion configuration, CancellationToken cancellationToken = default)
        {
            _configurations.RemoveAll(item => item.Name == configuration.Name);
            _configurations.Add(configuration);
            return Task.CompletedTask;
        }

        public Task DeleteConfigurationAsync(string name, CancellationToken cancellationToken = default)
        {
            _configurations.RemoveAll(item => item.Name == name);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingConfigurationVersionService : IConfigurationVersionService
    {
        public Task<IReadOnlyList<ConfigurationVersion>> ListConfigurationsAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("配置数据库不可用");
        }

        public Task<ConfigurationVersion?> GetConfigurationAsync(string name, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("配置数据库不可用");
        }

        public Task SaveConfigurationAsync(ConfigurationVersion configuration, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("配置数据库不可用");
        }

        public Task DeleteConfigurationAsync(string name, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("配置数据库不可用");
        }
    }
}
