using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Desktop.Theming;
using Arturia.FrpNexus.Desktop.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed partial class SettingsPageViewModel : PageViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;

    private static readonly SettingsOptionViewModel[] ThemeOptionValues =
    [
        new("System", "跟随系统"),
        new("Light", "浅色模式"),
        new("Dark", "深色模式")
    ];

    private static readonly SettingsOptionViewModel[] LanguageOptionValues =
    [
        new("zh-CN", "简体中文"),
        new("en-US", "English (US)")
    ];

    private static readonly SettingsOptionViewModel[] FrpDownloadSourceOptionValues =
    [
        new("GitHub Releases", "GitHub Releases (官方, 推荐)"),
        new("GHProxy", "GHProxy (国内加速)"),
        new("Custom", "自定义镜像源...")
    ];

    [ObservableProperty]
    private string _theme = "Light";

    [ObservableProperty]
    private SettingsOptionViewModel _selectedThemeOption = ThemeOptionValues[1];

    [ObservableProperty]
    private string _language = "zh-CN";

    [ObservableProperty]
    private SettingsOptionViewModel _selectedLanguageOption = LanguageOptionValues[0];

    [ObservableProperty]
    private string _frpDownloadSource = "GitHub Releases";

    [ObservableProperty]
    private SettingsOptionViewModel _selectedFrpDownloadSourceOption = FrpDownloadSourceOptionValues[0];

    [ObservableProperty]
    private string _coreDirectory = string.Empty;

    [ObservableProperty]
    private string _configDirectory = string.Empty;

    [ObservableProperty]
    private string _logDirectory = string.Empty;

    [ObservableProperty]
    private string _sqliteDatabasePath = string.Empty;

    [ObservableProperty]
    private string _saveStatusText = "设置会保存到本地 SQLite 数据库";

    public SettingsPageViewModel(ISettingsService settingsService, IThemeService themeService)
        : base("设置", "配置界面偏好、FRP 下载源、本地路径和 SSH 密钥")
    {
        _settingsService = settingsService;
        _themeService = themeService;

        SshKeys =
        [
            new("id_rsa_prod_server", "SHA256: 4a6x9p...L8Q=", "生产服务器"),
            new("id_ed25519_home_nas", "SHA256: zT91bc...K3M=", "家庭 NAS")
        ];

        _ = LoadSettingsAsync();
    }

    public ObservableCollection<SshKeyViewModel> SshKeys { get; }

    public IReadOnlyList<SettingsOptionViewModel> ThemeOptions => ThemeOptionValues;

    public IReadOnlyList<SettingsOptionViewModel> LanguageOptions => LanguageOptionValues;

    public IReadOnlyList<SettingsOptionViewModel> FrpDownloadSourceOptions => FrpDownloadSourceOptionValues;

    public async Task LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        FrpNexusSettingsSnapshot settings;
        try
        {
            settings = await _settingsService.GetSettingsAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            SaveStatusText = "设置加载已取消。";
            return;
        }
        catch (Exception ex)
        {
            SaveStatusText = ViewModelErrorText.ForUser("设置加载", ex);
            return;
        }

        Theme = settings.Theme;
        SelectedThemeOption = FindOption(ThemeOptions, settings.Theme);
        Language = settings.Language;
        SelectedLanguageOption = FindOption(LanguageOptions, settings.Language);
        FrpDownloadSource = settings.FrpDownloadSource;
        SelectedFrpDownloadSourceOption = FindOption(FrpDownloadSourceOptions, settings.FrpDownloadSource);
        CoreDirectory = settings.CoreDirectory;
        ConfigDirectory = settings.ConfigDirectory;
        LogDirectory = settings.LogDirectory;
        SqliteDatabasePath = settings.SqliteDatabasePath;
        _themeService.ApplyTheme(Theme);
        SaveStatusText = "已加载本地设置";
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var settings = new FrpNexusSettingsSnapshot(
            Theme,
            Language,
            FrpDownloadSource,
            CoreDirectory,
            ConfigDirectory,
            LogDirectory,
            SqliteDatabasePath);

        try
        {
            await _settingsService.SaveSettingsAsync(settings);
            _themeService.ApplyTheme(Theme);
            SaveStatusText = "设置已保存到本地 SQLite，主题已应用";
        }
        catch (OperationCanceledException)
        {
            SaveStatusText = "设置保存已取消。";
        }
        catch (Exception ex)
        {
            SaveStatusText = ViewModelErrorText.ForUser("设置保存", ex);
        }
    }

    partial void OnSelectedThemeOptionChanged(SettingsOptionViewModel value)
    {
        Theme = value.Value;
    }

    partial void OnSelectedLanguageOptionChanged(SettingsOptionViewModel value)
    {
        Language = value.Value;
    }

    partial void OnSelectedFrpDownloadSourceOptionChanged(SettingsOptionViewModel value)
    {
        FrpDownloadSource = value.Value;
    }

    private static SettingsOptionViewModel FindOption(
        IReadOnlyList<SettingsOptionViewModel> options,
        string value)
    {
        return options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase))
            ?? options[0];
    }
}
