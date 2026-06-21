using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
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
    private readonly INodeManagementService _nodeManagementService;
    private readonly INodeCredentialSecretService _nodeCredentialSecretService;

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

    [ObservableProperty]
    private string _credentialSecurityStatusText = "凭据状态会从本地节点和 Windows DPAPI 安全存储读取。";

    [ObservableProperty]
    private int _credentialNodeCount;

    [ObservableProperty]
    private int _sessionPasswordNodeCount;

    [ObservableProperty]
    private int _privateKeyNodeCount;

    [ObservableProperty]
    private int _sshAgentNodeCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearAllSavedSessionPasswordsCommand))]
    private int _savedSessionPasswordNodeCount;

    public SettingsPageViewModel(
        ISettingsService settingsService,
        IFrpReleaseService frpReleaseService,
        IFilePickerService filePickerService,
        IFrpCoreDownloadOptionsDialogService downloadOptionsDialogService,
        INodeManagementService nodeManagementService,
        INodeCredentialSecretService nodeCredentialSecretService)
        : base("设置", "FRP 下载源、本地路径、密钥、日志和本地数据")
    {
        _settingsService = settingsService;
        _frpReleaseService = frpReleaseService;
        _filePickerService = filePickerService;
        _downloadOptionsDialogService = downloadOptionsDialogService;
        _nodeManagementService = nodeManagementService;
        _nodeCredentialSecretService = nodeCredentialSecretService;

        CredentialSecurityNodes = [];

        _ = LoadSettingsAsync();
    }

    public ObservableCollection<CredentialSecurityNodeViewModel> CredentialSecurityNodes { get; }

    public IReadOnlyList<SettingsOptionViewModel> FrpDownloadSourceOptions => FrpDownloadSourceOptionValues;

    public string DownloadLatestFrpReleaseButtonText => IsFrpReleaseDownloading ? "下载中..." : "下载最新版";

    public bool HasSavedSessionPasswords => SavedSessionPasswordNodeCount > 0;

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
        await LoadCredentialSecurityAsync(cancellationToken);
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

    [RelayCommand]
    private Task RefreshCredentialSecurityAsync(CancellationToken cancellationToken = default)
    {
        return LoadCredentialSecurityAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(HasSavedSessionPasswords))]
    private async Task ClearAllSavedSessionPasswordsAsync(CancellationToken cancellationToken = default)
    {
        var rowsToClear = CredentialSecurityNodes
            .Where(row => row.HasSavedSessionPassword)
            .ToArray();

        if (rowsToClear.Length == 0)
        {
            CredentialSecurityStatusText = "当前没有已保存的会话密码。";
            return;
        }

        try
        {
            foreach (var row in rowsToClear)
            {
                await _nodeCredentialSecretService.DeleteSessionPasswordAsync(row.NodeName, cancellationToken);
                row.HasSavedSessionPassword = false;
            }

            await LoadCredentialSecurityAsync(cancellationToken);
            CredentialSecurityStatusText = $"已清除 {rowsToClear.Length} 个节点的保存密码。";
        }
        catch (OperationCanceledException)
        {
            CredentialSecurityStatusText = "清除保存密码已取消。";
        }
        catch (Exception ex)
        {
            CredentialSecurityStatusText = ViewModelErrorText.ForUser("清除保存密码", ex);
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

    partial void OnSavedSessionPasswordNodeCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasSavedSessionPasswords));
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

    private async Task LoadCredentialSecurityAsync(CancellationToken cancellationToken)
    {
        try
        {
            var nodes = await _nodeManagementService.ListNodesAsync(cancellationToken);
            var rows = new List<CredentialSecurityNodeViewModel>(nodes.Count);
            var sessionPasswordCount = 0;
            var privateKeyCount = 0;
            var sshAgentCount = 0;
            var savedPasswordCount = 0;

            foreach (var node in nodes.OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase))
            {
                var authenticationMode = ResolveAuthenticationMode(node.Authentication);
                switch (authenticationMode)
                {
                    case SshAuthenticationMode.PrivateKey:
                        privateKeyCount++;
                        break;
                    case SshAuthenticationMode.SshAgent:
                        sshAgentCount++;
                        break;
                    default:
                        sessionPasswordCount++;
                        break;
                }

                var hasSavedPassword = await _nodeCredentialSecretService.HasSessionPasswordAsync(
                    node.Name,
                    cancellationToken);
                if (hasSavedPassword)
                {
                    savedPasswordCount++;
                }

                rows.Add(new CredentialSecurityNodeViewModel(
                    node.Name,
                    ToAuthenticationModeText(authenticationMode),
                    ResolvePrivateKeySummary(node.Authentication, authenticationMode),
                    hasSavedPassword,
                    ClearSavedSessionPasswordForNodeAsync));
            }

            CredentialSecurityNodes.Clear();
            foreach (var row in rows)
            {
                CredentialSecurityNodes.Add(row);
            }

            CredentialNodeCount = nodes.Count;
            SessionPasswordNodeCount = sessionPasswordCount;
            PrivateKeyNodeCount = privateKeyCount;
            SshAgentNodeCount = sshAgentCount;
            SavedSessionPasswordNodeCount = savedPasswordCount;
            CredentialSecurityStatusText =
                $"已读取 {nodes.Count} 个节点的认证状态，其中 {savedPasswordCount} 个节点保存了会话密码。";
        }
        catch (OperationCanceledException)
        {
            CredentialSecurityStatusText = "凭据安全状态读取已取消。";
        }
        catch (Exception ex)
        {
            CredentialSecurityStatusText = ViewModelErrorText.ForUser("凭据安全状态", ex);
        }
    }

    private async Task ClearSavedSessionPasswordForNodeAsync(
        CredentialSecurityNodeViewModel row,
        CancellationToken cancellationToken)
    {
        if (!row.HasSavedSessionPassword)
        {
            CredentialSecurityStatusText = $"{row.NodeName} 没有已保存的会话密码。";
            return;
        }

        try
        {
            await _nodeCredentialSecretService.DeleteSessionPasswordAsync(row.NodeName, cancellationToken);
            row.HasSavedSessionPassword = false;
            await LoadCredentialSecurityAsync(cancellationToken);
            CredentialSecurityStatusText = $"已清除 {row.NodeName} 的保存密码。";
        }
        catch (OperationCanceledException)
        {
            CredentialSecurityStatusText = "清除保存密码已取消。";
        }
        catch (Exception ex)
        {
            CredentialSecurityStatusText = ViewModelErrorText.ForUser("清除保存密码", ex);
        }
    }

    private static SshAuthenticationMode ResolveAuthenticationMode(string value)
    {
        if (ContainsIgnoreCase(value, "SshAgent") || ContainsIgnoreCase(value, "SSH Agent"))
        {
            return SshAuthenticationMode.SshAgent;
        }

        if (ContainsIgnoreCase(value, "PrivateKey")
            || ContainsIgnoreCase(value, "私钥")
            || ContainsIgnoreCase(value, "密钥"))
        {
            return SshAuthenticationMode.PrivateKey;
        }

        return SshAuthenticationMode.SessionPassword;
    }

    private static string ToAuthenticationModeText(SshAuthenticationMode mode)
    {
        return mode switch
        {
            SshAuthenticationMode.PrivateKey => "私钥路径",
            SshAuthenticationMode.SshAgent => "SSH Agent",
            _ => "会话密码"
        };
    }

    private static string ResolvePrivateKeySummary(string value, SshAuthenticationMode mode)
    {
        if (mode != SshAuthenticationMode.PrivateKey)
        {
            return "-";
        }

        var candidatePath = ExtractPrivateKeyPath(value);
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return "仅保存路径引用";
        }

        return Path.GetFileName(candidatePath) is { Length: > 0 } fileName
            ? fileName
            : "仅保存路径引用";
    }

    private static string ExtractPrivateKeyPath(string value)
    {
        var trimmedValue = value.Trim();
        foreach (var prefix in new[] { "PrivateKey:", "PrivateKey=", "私钥文件:", "私钥路径:" })
        {
            if (trimmedValue.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return trimmedValue[prefix.Length..].Trim();
            }
        }

        return trimmedValue.Contains('\\') || trimmedValue.Contains('/')
            ? trimmedValue
            : string.Empty;
    }

    private static bool ContainsIgnoreCase(string value, string candidate)
    {
        return value.Contains(candidate, StringComparison.OrdinalIgnoreCase);
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
