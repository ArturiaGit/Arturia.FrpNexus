using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed partial class ConfigurationsPageViewModel : PageViewModel
{
    private static readonly Regex TomlAssignmentRegex = new(
        @"^(\s*)([A-Za-z0-9_.-]+)(\s*=\s*)(.*)$",
        RegexOptions.Compiled);

    private static readonly Regex TomlValueTokenRegex = new(
        @"""(?:\\""|[^""])*""|\b\d+\b",
        RegexOptions.Compiled);

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

    [ObservableProperty]
    private bool _isAdvancedOptionsVisible;

    public ConfigurationsPageViewModel(
        ITomlConfigurationService tomlConfigurationService,
        IConfigurationVersionService configurationVersionService)
        : base("配置", "通过表单生成 TOML，并预览即将上传的 frpc 配置")
    {
        _tomlConfigurationService = tomlConfigurationService;
        _configurationVersionService = configurationVersionService;
        Configurations = [];
        TomlPreviewLines = [];
        GenerateToml();
        _ = LoadConfigurationsAsync();
    }

    public ObservableCollection<ConfigurationVersion> Configurations { get; }

    public ObservableCollection<TomlPreviewLineViewModel> TomlPreviewLines { get; }

    public IReadOnlyList<TunnelProtocol> ProtocolOptions { get; } =
    [
        TunnelProtocol.Tcp,
        TunnelProtocol.Udp,
        TunnelProtocol.Http,
        TunnelProtocol.Https
    ];

    public ConfigurationPreview Preview => CreatePreview(TomlPreview);

    public string AdvancedOptionsChevronIcon => IsAdvancedOptionsVisible ? "chevron_down" : "chevron_right";

    public double AdvancedOptionsPanelMaxHeight => IsAdvancedOptionsVisible ? 112 : 0;

    public double AdvancedOptionsPanelOpacity => IsAdvancedOptionsVisible ? 1 : 0;

    public double AdvancedOptionsPanelOffsetY => IsAdvancedOptionsVisible ? 0 : -4;

    public bool IsAdvancedOptionsInteractive => IsAdvancedOptionsVisible;

    [RelayCommand]
    public async Task LoadConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ConfigurationVersion> configurations;
        try
        {
            configurations = await _configurationVersionService.ListConfigurationsAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            StatusText = "本地配置加载已取消。";
            return;
        }
        catch (Exception ex)
        {
            ConfigurationCountText = "配置加载失败";
            StatusText = ViewModelErrorText.ForUser("本地配置加载", ex);
            return;
        }

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
            RefreshTomlPreviewLines();
            OnPropertyChanged(nameof(Preview));
        }
        catch (InvalidOperationException ex)
        {
            ErrorText = ex.Message;
            StatusText = "TOML 生成失败。";
        }
        catch (Exception ex)
        {
            ErrorText = ViewModelErrorText.ForUser("TOML 生成", ex);
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
        }
        catch (InvalidOperationException ex)
        {
            ErrorText = ex.Message;
            StatusText = "保存配置失败。";
            return;
        }
        catch (OperationCanceledException)
        {
            ErrorText = "保存配置已取消。";
            StatusText = "保存配置失败。";
            return;
        }

        var configuration = new ConfigurationVersion(
            preview.ProxyName,
            preview.Protocol,
            preview.LocalAddress,
            preview.LocalPort,
            preview.RemoteEndpoint,
            TomlPreview,
            DateTimeOffset.UtcNow);

        try
        {
            await _configurationVersionService.SaveConfigurationAsync(configuration);
            await LoadConfigurationsAsync();
            SelectedConfiguration = Configurations.FirstOrDefault(item => item.Name == configuration.Name);
            ErrorText = string.Empty;
            StatusText = $"已保存本地配置 `{configuration.Name}`。";
        }
        catch (OperationCanceledException)
        {
            ErrorText = "保存配置已取消。";
            StatusText = "保存配置失败。";
        }
        catch (Exception ex)
        {
            ErrorText = ViewModelErrorText.ForUser("保存配置", ex);
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
        catch (OperationCanceledException)
        {
            ErrorText = "TOML 本地校验已取消。";
            StatusText = "TOML 本地校验失败。";
        }
        catch (Exception ex)
        {
            ErrorText = ViewModelErrorText.ForUser("TOML 本地校验", ex);
            StatusText = "TOML 本地校验失败。";
        }
    }

    [RelayCommand]
    private void ToggleAdvancedOptions()
    {
        IsAdvancedOptionsVisible = !IsAdvancedOptionsVisible;
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

    partial void OnTomlPreviewChanged(string value)
    {
        RefreshTomlPreviewLines();
        OnPropertyChanged(nameof(Preview));
    }

    partial void OnIsAdvancedOptionsVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(AdvancedOptionsChevronIcon));
        OnPropertyChanged(nameof(AdvancedOptionsPanelMaxHeight));
        OnPropertyChanged(nameof(AdvancedOptionsPanelOpacity));
        OnPropertyChanged(nameof(AdvancedOptionsPanelOffsetY));
        OnPropertyChanged(nameof(IsAdvancedOptionsInteractive));
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

    private void RefreshTomlPreviewLines()
    {
        TomlPreviewLines.Clear();

        if (string.IsNullOrWhiteSpace(TomlPreview))
        {
            TomlPreviewLines.Add(new TomlPreviewLineViewModel(1, [new TomlPreviewTokenViewModel("TOML 预览将在生成后显示。", TomlPreviewTokenKind.Comment)]));
            return;
        }

        var lines = TomlPreview.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            TomlPreviewLines.Add(new TomlPreviewLineViewModel(index + 1, HighlightTomlLine(lines[index])));
        }
    }

    private static IReadOnlyList<TomlPreviewTokenViewModel> HighlightTomlLine(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return [new TomlPreviewTokenViewModel(" ", TomlPreviewTokenKind.Plain)];
        }

        var trimmed = line.TrimStart();
        if (trimmed.StartsWith('#') || trimmed.StartsWith('['))
        {
            return [new TomlPreviewTokenViewModel(line, TomlPreviewTokenKind.Section)];
        }

        var match = TomlAssignmentRegex.Match(line);
        if (!match.Success)
        {
            return [new TomlPreviewTokenViewModel(line, TomlPreviewTokenKind.Plain)];
        }

        var tokens = new List<TomlPreviewTokenViewModel>();
        AddIfNotEmpty(tokens, match.Groups[1].Value, TomlPreviewTokenKind.Plain);
        AddIfNotEmpty(tokens, match.Groups[2].Value, TomlPreviewTokenKind.Key);
        AddIfNotEmpty(tokens, match.Groups[3].Value, TomlPreviewTokenKind.Plain);

        var value = match.Groups[4].Value;
        var lastIndex = 0;
        foreach (Match valueMatch in TomlValueTokenRegex.Matches(value))
        {
            AddIfNotEmpty(tokens, value[lastIndex..valueMatch.Index], TomlPreviewTokenKind.Plain);

            var kind = valueMatch.Value.StartsWith('"')
                ? TomlPreviewTokenKind.String
                : TomlPreviewTokenKind.Number;
            AddIfNotEmpty(tokens, valueMatch.Value, kind);
            lastIndex = valueMatch.Index + valueMatch.Length;
        }

        AddIfNotEmpty(tokens, value[lastIndex..], TomlPreviewTokenKind.Plain);
        return tokens;
    }

    private static void AddIfNotEmpty(ICollection<TomlPreviewTokenViewModel> tokens, string text, TomlPreviewTokenKind kind)
    {
        if (text.Length > 0)
        {
            tokens.Add(new TomlPreviewTokenViewModel(text, kind));
        }
    }
}

public sealed class TomlPreviewLineViewModel(int number, IReadOnlyList<TomlPreviewTokenViewModel> tokens)
{
    public int Number { get; } = number;

    public IReadOnlyList<TomlPreviewTokenViewModel> Tokens { get; } = tokens;
}

public sealed class TomlPreviewTokenViewModel(string text, TomlPreviewTokenKind kind)
{
    public string Text { get; } = text;

    public TomlPreviewTokenKind Kind { get; } = kind;

    public bool IsKey => Kind == TomlPreviewTokenKind.Key;

    public bool IsString => Kind == TomlPreviewTokenKind.String;

    public bool IsNumber => Kind == TomlPreviewTokenKind.Number;

    public bool IsSection => Kind == TomlPreviewTokenKind.Section || Kind == TomlPreviewTokenKind.Comment;
}

public enum TomlPreviewTokenKind
{
    Plain,
    Key,
    String,
    Number,
    Section,
    Comment
}
