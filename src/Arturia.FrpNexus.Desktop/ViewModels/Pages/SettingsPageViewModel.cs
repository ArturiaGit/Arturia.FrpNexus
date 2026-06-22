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
    private readonly ILocalFolderLauncherService _localFolderLauncherService;
    private readonly ILocalCacheMaintenanceService _localCacheMaintenanceService;
    private readonly IConfirmationDialogService _confirmationDialogService;
    private readonly ILocalStoragePathSettingsService _localStoragePathSettingsService;
    private string _loadedLogDirectory = string.Empty;
    private string _loadedSqliteDatabaseDirectory = string.Empty;
    private string _loadedSqliteDatabasePath = string.Empty;

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
    private string _sqliteDatabaseDirectory = string.Empty;

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
        INodeCredentialSecretService nodeCredentialSecretService,
        ILocalFolderLauncherService? localFolderLauncherService = null,
        ILocalCacheMaintenanceService? localCacheMaintenanceService = null,
        IConfirmationDialogService? confirmationDialogService = null,
        ILocalStoragePathSettingsService? localStoragePathSettingsService = null)
        : base("设置", "FRP 下载源、本地路径、密钥、日志和本地数据")
    {
        _settingsService = settingsService;
        _frpReleaseService = frpReleaseService;
        _filePickerService = filePickerService;
        _downloadOptionsDialogService = downloadOptionsDialogService;
        _nodeManagementService = nodeManagementService;
        _nodeCredentialSecretService = nodeCredentialSecretService;
        _localFolderLauncherService = localFolderLauncherService ?? new NoopLocalFolderLauncherService();
        _localCacheMaintenanceService = localCacheMaintenanceService ?? new NoopLocalCacheMaintenanceService();
        _confirmationDialogService = confirmationDialogService ?? new NoopConfirmationDialogService();
        _localStoragePathSettingsService = localStoragePathSettingsService ?? new NoopLocalStoragePathSettingsService();

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
        SqliteDatabaseDirectory = ResolveSqliteDatabaseDirectory(settings);
        _loadedLogDirectory = LogDirectory;
        _loadedSqliteDatabaseDirectory = SqliteDatabaseDirectory;
        _loadedSqliteDatabasePath = SqliteDatabasePath;
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
            CustomFrpDownloadSourceUrl,
            SqliteDatabaseDirectory);

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
        if (string.IsNullOrWhiteSpace(LogDirectory))
        {
            SaveStatusText = "日志目录不能为空，请选择有效目录。";
            return;
        }

        if (string.IsNullOrWhiteSpace(SqliteDatabaseDirectory))
        {
            SaveStatusText = "SQLite 数据库目录不能为空，请选择有效目录。";
            return;
        }

        var normalizedLogDirectory = Path.GetFullPath(LogDirectory.Trim());
        var normalizedSqliteDirectory = Path.GetFullPath(SqliteDatabaseDirectory.Trim());
        var sqliteDirectoryChanged = !PathEquals(normalizedSqliteDirectory, _loadedSqliteDatabaseDirectory);
        var logDirectoryChanged = !PathEquals(normalizedLogDirectory, _loadedLogDirectory);
        var nextSqliteDatabasePath = Path.Combine(normalizedSqliteDirectory, "frpnexus.db");

        var settings = new FrpNexusSettingsSnapshot(
            FrpDownloadSource,
            normalizedLogDirectory,
            nextSqliteDatabasePath,
            CustomFrpDownloadSourceUrl,
            normalizedSqliteDirectory);

        try
        {
            await _settingsService.SaveSettingsAsync(settings);
            if (sqliteDirectoryChanged)
            {
                await _localStoragePathSettingsService.PrepareSqliteDatabaseDirectoryAsync(
                    string.IsNullOrWhiteSpace(_loadedSqliteDatabasePath) ? SqliteDatabasePath : _loadedSqliteDatabasePath,
                    normalizedSqliteDirectory);
            }

            if (logDirectoryChanged)
            {
                await _localStoragePathSettingsService.PrepareLogDirectoryAsync(
                    _loadedLogDirectory,
                    normalizedLogDirectory);
            }

            await _localStoragePathSettingsService.SaveSettingsAsync(
                new LocalStoragePathSettings(normalizedLogDirectory, normalizedSqliteDirectory));
            LogDirectory = normalizedLogDirectory;
            SqliteDatabaseDirectory = normalizedSqliteDirectory;
            SqliteDatabasePath = nextSqliteDatabasePath;
            _loadedLogDirectory = normalizedLogDirectory;
            _loadedSqliteDatabaseDirectory = normalizedSqliteDirectory;
            _loadedSqliteDatabasePath = nextSqliteDatabasePath;
            SaveStatusText = BuildPathSaveStatusText(sqliteDirectoryChanged, logDirectoryChanged);
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
    private async Task SelectLogDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var directory = await _filePickerService.PickLocalDirectoryAsync("选择日志目录", cancellationToken);
        if (string.IsNullOrWhiteSpace(directory))
        {
            SaveStatusText = "已取消选择日志目录。";
            return;
        }

        LogDirectory = directory;
        SaveStatusText = "已选择日志目录，请保存设置。";
    }

    [RelayCommand]
    private async Task SelectSqliteDatabaseDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var directory = await _filePickerService.PickLocalDirectoryAsync("选择 SQLite 数据库目录", cancellationToken);
        if (string.IsNullOrWhiteSpace(directory))
        {
            SaveStatusText = "已取消选择 SQLite 数据库目录。";
            return;
        }

        SqliteDatabaseDirectory = directory;
        SqliteDatabasePath = Path.Combine(directory, "frpnexus.db");
        SaveStatusText = "已选择 SQLite 数据库目录，请保存设置并重启应用。";
    }

    [RelayCommand]
    private async Task OpenLogDirectoryAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(LogDirectory))
        {
            SaveStatusText = "日志目录为空，请先填写有效路径。";
            return;
        }

        try
        {
            await _localFolderLauncherService.OpenFolderAsync(LogDirectory.Trim(), cancellationToken);
            SaveStatusText = "已打开日志目录。";
        }
        catch (OperationCanceledException)
        {
            SaveStatusText = "打开日志目录已取消。";
        }
        catch (Exception ex)
        {
            SaveStatusText = ViewModelErrorText.ForUser("打开日志目录", ex);
        }
    }

    [RelayCommand]
    private async Task OpenSqliteDatabaseDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var directory = !string.IsNullOrWhiteSpace(SqliteDatabaseDirectory)
            ? SqliteDatabaseDirectory
            : Path.GetDirectoryName(SqliteDatabasePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            SaveStatusText = "SQLite 数据库目录为空，请先选择有效目录。";
            return;
        }

        try
        {
            await _localFolderLauncherService.OpenFolderAsync(directory.Trim(), cancellationToken);
            SaveStatusText = "已打开 SQLite 数据库目录。";
        }
        catch (OperationCanceledException)
        {
            SaveStatusText = "打开 SQLite 数据库目录已取消。";
        }
        catch (Exception ex)
        {
            SaveStatusText = ViewModelErrorText.ForUser("打开 SQLite 数据库目录", ex);
        }
    }

    [RelayCommand]
    private async Task ClearLocalCacheAsync(CancellationToken cancellationToken = default)
    {
        var confirmed = await _confirmationDialogService.ShowAsync(
            new ConfirmationDialogRequest(
                "清理本地缓存",
                "将清理 FrpNexus 默认 FRP 核心下载缓存，不会删除 SQLite 数据库、日志、凭据、配置或用户选择的下载目录。",
                "清理缓存",
                "取消",
                "warning"),
            cancellationToken);
        if (!confirmed)
        {
            SaveStatusText = "已取消清理本地缓存。";
            return;
        }

        try
        {
            var result = await _localCacheMaintenanceService.ClearDefaultFrpReleaseCacheAsync(cancellationToken);
            SaveStatusText = result.DeletedFileCount == 0
                ? "默认 FRP 核心下载缓存无需清理。"
                : $"已清理默认 FRP 核心下载缓存：{result.DeletedFileCount} 个文件，释放 {FormatBytes(result.DeletedByteCount)}。";
        }
        catch (OperationCanceledException)
        {
            SaveStatusText = "清理本地缓存已取消。";
        }
        catch (Exception ex)
        {
            SaveStatusText = ViewModelErrorText.ForUser("清理本地缓存", ex);
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

    private static string ResolveSqliteDatabaseDirectory(FrpNexusSettingsSnapshot settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.SqliteDatabaseDirectory))
        {
            return settings.SqliteDatabaseDirectory;
        }

        return Path.GetDirectoryName(settings.SqliteDatabasePath) ?? string.Empty;
    }

    private static bool PathEquals(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(left.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildPathSaveStatusText(bool sqliteDirectoryChanged, bool logDirectoryChanged)
    {
        return (sqliteDirectoryChanged, logDirectoryChanged) switch
        {
            (true, true) => "设置已保存。数据库和历史日志已复制到新目录，旧文件已保留用于回滚；SQLite 将在重启后生效。",
            (true, false) => "设置已保存。SQLite 数据库已复制到新目录，旧文件已保留用于回滚；SQLite 将在重启后生效。",
            (false, true) => "设置已保存。历史日志已复制到新目录，旧文件已保留用于回滚；新日志文件将在下次启动完全生效。",
            _ => "设置已保存到本地 SQLite。"
        };
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var kib = bytes / 1024d;
        if (kib < 1024)
        {
            return $"{kib:0.#} KB";
        }

        var mib = kib / 1024d;
        return $"{mib:0.#} MB";
    }

    private sealed class NoopLocalFolderLauncherService : ILocalFolderLauncherService
    {
        public Task OpenFolderAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NoopLocalCacheMaintenanceService : ILocalCacheMaintenanceService
    {
        public Task<LocalCacheCleanupResult> ClearDefaultFrpReleaseCacheAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LocalCacheCleanupResult(0, 0, string.Empty));
        }
    }

    private sealed class NoopConfirmationDialogService : IConfirmationDialogService
    {
        public Task<bool> ShowAsync(
            ConfirmationDialogRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<ConfirmationDialogResult> ShowChoiceAsync(
            ConfirmationDialogChoiceRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ConfirmationDialogResult.Cancel);
        }
    }

    private sealed class NoopLocalStoragePathSettingsService : ILocalStoragePathSettingsService
    {
        public LocalStoragePathSettings GetSettings()
        {
            return new LocalStoragePathSettings(string.Empty, string.Empty);
        }

        public string GetLogDirectory()
        {
            return string.Empty;
        }

        public string GetSqliteDatabaseDirectory()
        {
            return string.Empty;
        }

        public string GetSqliteDatabasePath()
        {
            return string.Empty;
        }

        public Task SaveSettingsAsync(
            LocalStoragePathSettings pathSettings,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<SqliteDatabaseRelocationResult> PrepareSqliteDatabaseDirectoryAsync(
            string currentDatabasePath,
            string targetDatabaseDirectory,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SqliteDatabaseRelocationResult(
                currentDatabasePath,
                Path.Combine(targetDatabaseDirectory, "frpnexus.db"),
                false,
                false,
                null));
        }

        public Task<LogDirectoryRelocationResult> PrepareLogDirectoryAsync(
            string currentLogDirectory,
            string targetLogDirectory,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LogDirectoryRelocationResult(
                currentLogDirectory,
                targetLogDirectory,
                0,
                0));
        }
    }
}
