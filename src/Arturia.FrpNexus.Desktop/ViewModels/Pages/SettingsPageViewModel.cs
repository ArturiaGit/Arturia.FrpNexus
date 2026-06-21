using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Desktop.Services;
using Arturia.FrpNexus.Desktop.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed partial class SettingsPageViewModel : PageViewModel
{
    private const string GitHubDownloadSource = "GitHub Releases";
    private const string CustomDownloadSource = "Custom";

    private readonly ISettingsService _settingsService;
    private readonly IFrpReleaseService _frpReleaseService;
    private readonly IFilePickerService _filePickerService;
    private readonly IFrpCoreDownloadOptionsDialogService _downloadOptionsDialogService;

    private static readonly SettingsOptionViewModel[] FrpDownloadSourceOptionValues =
    [
        new("GitHub Releases", "GitHub Releases (官方, 推荐)"),
        new("GHProxy", "GHProxy (国内加速)"),
        new("Custom", "自定义镜像源...")
    ];

    [ObservableProperty]
    private string _frpDownloadSource = "GitHub Releases";

    [ObservableProperty]
    private SettingsOptionViewModel _selectedFrpDownloadSourceOption = FrpDownloadSourceOptionValues[0];

    [ObservableProperty]
    private string _customFrpDownloadSourceUrl = string.Empty;

    [ObservableProperty]
    private bool _isCustomFrpDownloadSourceSelected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadLatestFrpReleaseCommand))]
    private bool _isFrpReleaseDownloading;

    [ObservableProperty]
    private string _logDirectory = string.Empty;

    [ObservableProperty]
    private string _sqliteDatabasePath = string.Empty;

    [ObservableProperty]
    private string _saveStatusText = "设置会保存在本地 SQLite 数据库。";

    public SettingsPageViewModel(
        ISettingsService settingsService,
        IFrpReleaseService frpReleaseService,
        IFilePickerService filePickerService,
        IFrpCoreDownloadOptionsDialogService downloadOptionsDialogService)
        : base("设置", "FRP 下载源、本地路径、密钥、日志和本地数据")
    {
        _settingsService = settingsService;
        _frpReleaseService = frpReleaseService;
        _filePickerService = filePickerService;
        _downloadOptionsDialogService = downloadOptionsDialogService;

        SshKeys =
        [
            new("id_rsa_prod_server", "SHA256: 4a6x9p...L8Q=", "生产服务器"),
            new("id_ed25519_home_nas", "SHA256: zT91bc...K3M=", "家庭 NAS")
        ];

        _ = LoadSettingsAsync();
    }

    public ObservableCollection<SshKeyViewModel> SshKeys { get; }

    public IReadOnlyList<SettingsOptionViewModel> FrpDownloadSourceOptions => FrpDownloadSourceOptionValues;

    public string DownloadLatestFrpReleaseButtonText => IsFrpReleaseDownloading ? "下载中..." : "下载最新版";

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

        FrpDownloadSource = settings.FrpDownloadSource;
        SelectedFrpDownloadSourceOption = FindOption(FrpDownloadSourceOptions, settings.FrpDownloadSource);
        CustomFrpDownloadSourceUrl = settings.CustomFrpDownloadSourceUrl;
        IsCustomFrpDownloadSourceSelected = IsCustomDownloadSource(FrpDownloadSource);
        LogDirectory = settings.LogDirectory;
        SqliteDatabasePath = settings.SqliteDatabasePath;
        SaveStatusText = "已加载本地设置。";
    }

    [RelayCommand(CanExecute = nameof(CanDownloadLatestFrpRelease))]
    private async Task DownloadLatestFrpReleaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsFrpReleaseDownloading = true;

            var sourceOptions = await CreateReleaseSourceOptionsForDownloadAsync(cancellationToken);
            if (sourceOptions is null)
            {
                return;
            }

            var downloadOptions = await _downloadOptionsDialogService.ShowAsync(cancellationToken);
            if (downloadOptions is null)
            {
                SaveStatusText = "已取消 FRP 核心下载。";
                return;
            }

            var downloadDirectory = await _filePickerService.PickFrpDownloadDirectoryAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(downloadDirectory))
            {
                SaveStatusText = "已取消 FRP 核心下载。";
                return;
            }

            SaveStatusText = $"正在下载 {downloadOptions.BinaryName} 最新版...";

            var versions = await _frpReleaseService.ListAvailableVersionsAsync(sourceOptions, cancellationToken);
            var latestVersion = versions
                .Where(version => !string.IsNullOrWhiteSpace(version.Version))
                .OrderByDescending(version => version.PublishedAt)
                .FirstOrDefault();

            if (latestVersion is null)
            {
                SaveStatusText = "未找到可下载的 FRP Release，请稍后重试。";
                return;
            }

            var result = await _frpReleaseService.PrepareReleaseAsync(new FrpReleasePreparationRequest(
                latestVersion.Version,
                downloadOptions.TargetRuntime,
                downloadOptions.BinaryName,
                downloadDirectory,
                sourceOptions),
                cancellationToken);

            SaveStatusText =
                $"{result.BinaryName} {result.Version} ({result.TargetRuntime}) 已下载到本地缓存：{result.LocalPath}";
        }
        catch (OperationCanceledException)
        {
            SaveStatusText = "FRP 核心下载已取消。";
        }
        catch (Exception ex)
        {
            SaveStatusText = ViewModelErrorText.ForUser("FRP 核心下载", ex);
        }
        finally
        {
            IsFrpReleaseDownloading = false;
        }
    }

    [RelayCommand]
    private async Task SaveCustomFrpDownloadSourceUrlAsync()
    {
        if (!TryNormalizeHttpUrl(CustomFrpDownloadSourceUrl, out var normalizedUrl))
        {
            SaveStatusText = "请输入有效的 HTTP/HTTPS 自定义镜像源地址。";
            return;
        }

        CustomFrpDownloadSourceUrl = normalizedUrl;
        var settings = new FrpNexusSettingsSnapshot(
            FrpDownloadSource,
            LogDirectory,
            SqliteDatabasePath,
            CustomFrpDownloadSourceUrl);

        try
        {
            await _settingsService.SaveSettingsAsync(settings);
            SaveStatusText = "自定义镜像源地址已保存。请仅使用可信赖的下载源；第三方下载源可能带来安全风险，使用后果需自行承担。";
        }
        catch (OperationCanceledException)
        {
            SaveStatusText = "自定义镜像源保存已取消。";
        }
        catch (Exception ex)
        {
            SaveStatusText = ViewModelErrorText.ForUser("自定义镜像源保存", ex);
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var settings = new FrpNexusSettingsSnapshot(
            FrpDownloadSource,
            LogDirectory,
            SqliteDatabasePath,
            CustomFrpDownloadSourceUrl);

        try
        {
            await _settingsService.SaveSettingsAsync(settings);
            SaveStatusText = "设置已保存到本地 SQLite。";
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

    partial void OnSelectedFrpDownloadSourceOptionChanged(SettingsOptionViewModel value)
    {
        FrpDownloadSource = value.Value;
        IsCustomFrpDownloadSourceSelected = IsCustomDownloadSource(value.Value);
    }

    partial void OnIsFrpReleaseDownloadingChanged(bool value)
    {
        OnPropertyChanged(nameof(DownloadLatestFrpReleaseButtonText));
    }

    private bool CanDownloadLatestFrpRelease()
    {
        return !IsFrpReleaseDownloading;
    }

    private async Task<FrpReleaseSourceOptions?> CreateReleaseSourceOptionsForDownloadAsync(
        CancellationToken cancellationToken)
    {
        if (!IsCustomFrpDownloadSourceSelected)
        {
            return new FrpReleaseSourceOptions(GitHubDownloadSource);
        }

        var settings = await _settingsService.GetSettingsAsync(cancellationToken);
        if (!TryNormalizeHttpUrl(settings.CustomFrpDownloadSourceUrl, out var savedUrl))
        {
            SaveStatusText = "请先输入并保存可信赖的自定义镜像源地址。";
            return null;
        }

        return new FrpReleaseSourceOptions(CustomDownloadSource, savedUrl);
    }

    private static bool IsCustomDownloadSource(string value)
    {
        return string.Equals(value, CustomDownloadSource, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryNormalizeHttpUrl(string value, out string normalizedUrl)
    {
        normalizedUrl = string.Empty;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        normalizedUrl = uri.ToString();
        return true;
    }

    private static SettingsOptionViewModel FindOption(
        IReadOnlyList<SettingsOptionViewModel> options,
        string value)
    {
        return options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase))
            ?? options[0];
    }
}
