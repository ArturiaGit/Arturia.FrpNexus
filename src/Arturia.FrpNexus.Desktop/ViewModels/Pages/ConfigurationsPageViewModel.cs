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

public sealed partial class ConfigurationsPageViewModel : PageViewModel
{
    private readonly ITomlConfigurationService _tomlConfigurationService;
    private readonly IConfigurationVersionService _configurationVersionService;

    [ObservableProperty]
    private string _proxyName = "web_proxy_01";

    [ObservableProperty]
    private TunnelProtocol _selectedProtocol = TunnelProtocol.Http;

    [ObservableProperty]
    private string _localAddress = "127.0.0.1";

    [ObservableProperty]
    private string _localPort = "8080";

    [ObservableProperty]
    private string _remoteEndpoint = "example.com";

    [ObservableProperty]
    private string _tomlPreview = string.Empty;

    [ObservableProperty]
    private string _statusText = "本地 TOML 预览已就绪。";

    [ObservableProperty]
    private string _errorText = string.Empty;

    [ObservableProperty]
    private ConfigurationVersion? _selectedConfiguration;

    [ObservableProperty]
    private string _configurationCountText = "共 0 个本地配置";

    public ConfigurationsPageViewModel(
        ITomlConfigurationService tomlConfigurationService,
        IConfigurationVersionService configurationVersionService)
        : base("配置", "通过表单生成 TOML，并预览即将上传的 frpc 配置")
    {
        _tomlConfigurationService = tomlConfigurationService;
        _configurationVersionService = configurationVersionService;
        Configurations = [];
        GenerateToml();
        _ = LoadConfigurationsAsync();
    }

    public ObservableCollection<ConfigurationVersion> Configurations { get; }

    public IReadOnlyList<TunnelProtocol> ProtocolOptions { get; } =
    [
        TunnelProtocol.Tcp,
        TunnelProtocol.Udp,
        TunnelProtocol.Http,
        TunnelProtocol.Https
    ];

    public ConfigurationPreview Preview => CreatePreview(TomlPreview);

    [RelayCommand]
    public async Task LoadConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        var configurations = await _configurationVersionService.ListConfigurationsAsync(cancellationToken);

        Configurations.Clear();
        foreach (var configuration in configurations)
        {
            Configurations.Add(configuration);
        }

        ConfigurationCountText = $"共 {Configurations.Count} 个本地配置";

        if (SelectedConfiguration is null && Configurations.Count > 0)
        {
            SelectedConfiguration = Configurations[0];
            ApplyConfiguration(SelectedConfiguration);
        }
    }

    [RelayCommand]
    private void GenerateToml()
    {
        if (!TryCreatePreview(out var preview))
        {
            return;
        }

        try
        {
            TomlPreview = _tomlConfigurationService.GenerateProxyToml(preview);
            ErrorText = string.Empty;
            StatusText = "已生成本地 TOML 预览，尚未上传到远程节点。";
            OnPropertyChanged(nameof(Preview));
        }
        catch (InvalidOperationException ex)
        {
            ErrorText = ex.Message;
            StatusText = "TOML 生成失败。";
        }
    }

    [RelayCommand]
    private async Task SaveConfigurationAsync()
    {
        if (!TryCreatePreview(out var preview))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(TomlPreview))
        {
            GenerateToml();
        }

        try
        {
            await _tomlConfigurationService.ValidateAsync(TomlPreview);
            var configuration = new ConfigurationVersion(
                preview.ProxyName,
                preview.Protocol,
                preview.LocalAddress,
                preview.LocalPort,
                preview.RemoteEndpoint,
                TomlPreview,
                DateTimeOffset.UtcNow);

            await _configurationVersionService.SaveConfigurationAsync(configuration);
            await LoadConfigurationsAsync();
            SelectedConfiguration = Configurations.FirstOrDefault(item => item.Name == configuration.Name);
            ErrorText = string.Empty;
            StatusText = $"已保存本地配置 `{configuration.Name}`。";
        }
        catch (InvalidOperationException ex)
        {
            ErrorText = ex.Message;
            StatusText = "保存配置失败。";
        }
    }

    [RelayCommand]
    private async Task ValidateTomlAsync()
    {
        try
        {
            await _tomlConfigurationService.ValidateAsync(TomlPreview);
            ErrorText = string.Empty;
            StatusText = "TOML 本地校验通过。";
        }
        catch (InvalidOperationException ex)
        {
            ErrorText = ex.Message;
            StatusText = "TOML 本地校验失败。";
        }
    }

    partial void OnSelectedConfigurationChanged(ConfigurationVersion? value)
    {
        if (value is not null)
        {
            ApplyConfiguration(value);
        }
    }

    partial void OnProxyNameChanged(string value)
    {
        ClearValidationState();
    }

    partial void OnSelectedProtocolChanged(TunnelProtocol value)
    {
        RemoteEndpoint = value is TunnelProtocol.Http or TunnelProtocol.Https ? "example.com" : "60022";
        ClearValidationState();
    }

    partial void OnLocalAddressChanged(string value)
    {
        ClearValidationState();
    }

    partial void OnLocalPortChanged(string value)
    {
        ClearValidationState();
    }

    partial void OnRemoteEndpointChanged(string value)
    {
        ClearValidationState();
    }

    private bool TryCreatePreview(out ConfigurationPreview preview)
    {
        preview = CreatePreview(TomlPreview);

        if (string.IsNullOrWhiteSpace(ProxyName))
        {
            ErrorText = "代理名称不能为空。";
            StatusText = "TOML 生成失败。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(LocalAddress))
        {
            ErrorText = "本地 IP 不能为空。";
            StatusText = "TOML 生成失败。";
            return false;
        }

        if (!int.TryParse(LocalPort, out var localPort) || localPort is < 1 or > 65535)
        {
            ErrorText = "本地端口必须是 1 到 65535 之间的数字。";
            StatusText = "TOML 生成失败。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(RemoteEndpoint))
        {
            ErrorText = "远程配置不能为空。";
            StatusText = "TOML 生成失败。";
            return false;
        }

        preview = new ConfigurationPreview(
            ProxyName.Trim(),
            SelectedProtocol,
            LocalAddress.Trim(),
            localPort,
            RemoteEndpoint.Trim(),
            TomlPreview);

        return true;
    }

    private ConfigurationPreview CreatePreview(string toml)
    {
        var localPort = int.TryParse(LocalPort, out var port) ? port : 0;
        return new ConfigurationPreview(
            ProxyName,
            SelectedProtocol,
            LocalAddress,
            localPort,
            RemoteEndpoint,
            toml);
    }

    private void ClearValidationState()
    {
        ErrorText = string.Empty;
        StatusText = "表单已修改，请重新生成 TOML。";
        OnPropertyChanged(nameof(Preview));
    }

    private void ApplyConfiguration(ConfigurationVersion configuration)
    {
        ProxyName = configuration.Name;
        SelectedProtocol = configuration.Protocol;
        LocalAddress = configuration.LocalAddress;
        LocalPort = configuration.LocalPort.ToString();
        RemoteEndpoint = configuration.RemoteEndpoint;
        TomlPreview = configuration.Toml;
        ErrorText = string.Empty;
        StatusText = $"已加载本地配置 `{configuration.Name}`。";
        OnPropertyChanged(nameof(Preview));
    }
}
