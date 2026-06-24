using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.Services;
using Arturia.FrpNexus.Desktop.ViewModels.Dialogs;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class SettingsPageViewModelTests
{
    [Fact]
    public async Task LoadSettingsAsync_ShouldPopulatePersistedSettings()
    {
        var settingsService = new FakeSettingsService(new FrpNexusSettingsSnapshot(
            "GHProxy",
            @"D:\FrpNexus\logs",
            @"D:\FrpNexus\data\frpnexus.db",
            "https://mirror.example.com/repos/fatedier/frp/releases"));
        var viewModel = CreateViewModel(
            settingsService,
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService(),
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService());

        await viewModel.LoadSettingsAsync();

        Assert.Equal("GHProxy", viewModel.FrpDownloadSource);
        Assert.Equal("GHProxy (国内加速)", viewModel.SelectedFrpDownloadSourceOption.DisplayText);
        Assert.Equal("https://mirror.example.com/repos/fatedier/frp/releases", viewModel.CustomFrpDownloadSourceUrl);
        Assert.False(viewModel.IsCustomFrpDownloadSourceSelected);
        Assert.Equal(@"D:\FrpNexus\logs", viewModel.LogDirectory);
        Assert.Equal(@"D:\FrpNexus\data\frpnexus.db", viewModel.SqliteDatabasePath);
        Assert.Equal("已加载本地设置。", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task SaveSettingsCommand_ShouldPersistEditableSettings()
    {
        var settingsService = new FakeSettingsService(new FrpNexusSettingsSnapshot(
            "GitHub Releases",
            @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\logs",
            @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\data\frpnexus.db",
            string.Empty));
        var viewModel = CreateViewModel(
            settingsService,
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService(),
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService());
        await viewModel.LoadSettingsAsync();

        viewModel.SelectedFrpDownloadSourceOption = viewModel.FrpDownloadSourceOptions.Single(option => option.Value == "Custom");
        viewModel.CustomFrpDownloadSourceUrl = "https://mirror.example.com/repos/fatedier/frp/releases";
        viewModel.LogDirectory = @"D:\Tools\frp-logs";

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.NotNull(settingsService.SavedSettings);
        Assert.Equal("Custom", settingsService.SavedSettings!.FrpDownloadSource);
        Assert.Equal("https://mirror.example.com/repos/fatedier/frp/releases", settingsService.SavedSettings.CustomFrpDownloadSourceUrl);
        Assert.Equal(@"D:\Tools\frp-logs", settingsService.SavedSettings.LogDirectory);
        Assert.Equal(@"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\data\frpnexus.db", settingsService.SavedSettings.SqliteDatabasePath);
        Assert.Contains("历史日志已复制到新目录", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task SaveCustomFrpDownloadSourceUrlCommand_ShouldPersistUrlAndShowSafetyNotice()
    {
        var settingsService = CreateDefaultSettingsService();
        var viewModel = CreateViewModel(
            settingsService,
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService(),
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService());
        await viewModel.LoadSettingsAsync();
        viewModel.SelectedFrpDownloadSourceOption = viewModel.FrpDownloadSourceOptions.Single(option => option.Value == "Custom");
        viewModel.CustomFrpDownloadSourceUrl = "https://mirror.example.com/repos/fatedier/frp/releases";

        await viewModel.SaveCustomFrpDownloadSourceUrlCommand.ExecuteAsync(null);

        Assert.NotNull(settingsService.SavedSettings);
        Assert.Equal("https://mirror.example.com/repos/fatedier/frp/releases", settingsService.SavedSettings!.CustomFrpDownloadSourceUrl);
        Assert.Contains("可信赖", viewModel.SaveStatusText);
        Assert.Contains("自行承担", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task SaveCustomFrpDownloadSourceUrlCommand_ShouldRejectInvalidUrl()
    {
        var settingsService = CreateDefaultSettingsService();
        var viewModel = CreateViewModel(
            settingsService,
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService(),
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService());
        await viewModel.LoadSettingsAsync();
        viewModel.SelectedFrpDownloadSourceOption = viewModel.FrpDownloadSourceOptions.Single(option => option.Value == "Custom");
        viewModel.CustomFrpDownloadSourceUrl = "not-a-url";

        await viewModel.SaveCustomFrpDownloadSourceUrlCommand.ExecuteAsync(null);

        Assert.Null(settingsService.SavedSettings);
        Assert.Equal("请输入有效的 HTTP/HTTPS 自定义镜像源地址。", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task SaveSettingsCommand_ShouldReportRecoverableFailure()
    {
        var viewModel = CreateViewModel(
            new FailingSettingsService(),
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService(),
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService());
        viewModel.LogDirectory = @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\logs";
        viewModel.SqliteDatabaseDirectory = @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\data";
        viewModel.SqliteDatabasePath = @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\data\frpnexus.db";

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.Equal("设置保存失败，请检查输入、网络或本地数据状态后重试。", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task OpenLogDirectoryCommand_ShouldOpenCurrentLogDirectory()
    {
        var folderLauncher = new FakeLocalFolderLauncherService();
        var viewModel = CreateViewModel(
            CreateDefaultSettingsService(),
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService(),
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService(),
            folderLauncher,
            new FakeLocalCacheMaintenanceService(),
            new FakeConfirmationDialogService());
        await viewModel.LoadSettingsAsync();

        await viewModel.OpenLogDirectoryCommand.ExecuteAsync(null);

        Assert.Equal(@"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\logs", folderLauncher.OpenedPath);
        Assert.Equal("已打开日志目录。", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task OpenLogDirectoryCommand_ShouldRejectEmptyLogDirectory()
    {
        var viewModel = CreateViewModel(
            new FakeSettingsService(new FrpNexusSettingsSnapshot(
                "GitHub Releases",
                string.Empty,
                @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\data\frpnexus.db",
                string.Empty)),
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService(),
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService(),
            new FakeLocalFolderLauncherService(),
            new FakeLocalCacheMaintenanceService(),
            new FakeConfirmationDialogService());
        await viewModel.LoadSettingsAsync();

        await viewModel.OpenLogDirectoryCommand.ExecuteAsync(null);

        Assert.Equal("日志目录为空，请先填写有效路径。", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task SelectLogDirectoryCommand_ShouldUpdateLogDirectoryWhenDirectoryIsPicked()
    {
        var viewModel = CreateViewModel(
            CreateDefaultSettingsService(),
            new FakeFrpReleaseService(),
            new FakeFilePickerService(localDirectory: @"D:\FrpNexus\logs"),
            new FakeFrpCoreDownloadOptionsDialogService(),
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService());

        await viewModel.SelectLogDirectoryCommand.ExecuteAsync(null);

        Assert.Equal(@"D:\FrpNexus\logs", viewModel.LogDirectory);
        Assert.Equal("已选择日志目录，请保存设置。", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task SelectSqliteDatabaseDirectoryCommand_ShouldUpdateDirectoryAndDerivedDatabasePath()
    {
        var viewModel = CreateViewModel(
            CreateDefaultSettingsService(),
            new FakeFrpReleaseService(),
            new FakeFilePickerService(localDirectory: @"D:\FrpNexus\data"),
            new FakeFrpCoreDownloadOptionsDialogService(),
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService());

        await viewModel.SelectSqliteDatabaseDirectoryCommand.ExecuteAsync(null);

        Assert.Equal(@"D:\FrpNexus\data", viewModel.SqliteDatabaseDirectory);
        Assert.Equal(@"D:\FrpNexus\data\frpnexus.db", viewModel.SqliteDatabasePath);
        Assert.Equal("已选择 SQLite 数据库目录，请保存设置并重启应用。", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task SaveSettingsCommand_ShouldPersistPathSettingsAndPrepareSqliteCopy()
    {
        var pathSettingsService = new FakeLocalStoragePathSettingsService(
            new LocalStoragePathSettings(
                @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\logs",
                @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\data"));
        var viewModel = CreateViewModel(
            CreateDefaultSettingsService(),
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService(),
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService(),
            localStoragePathSettingsService: pathSettingsService);
        await viewModel.LoadSettingsAsync();
        viewModel.LogDirectory = @"D:\FrpNexus\logs";
        viewModel.SqliteDatabaseDirectory = @"D:\FrpNexus\data";

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.NotNull(pathSettingsService.SavedSettings);
        Assert.Equal(@"D:\FrpNexus\logs", pathSettingsService.SavedSettings!.LogDirectory);
        Assert.Equal(@"D:\FrpNexus\data", pathSettingsService.SavedSettings.SqliteDatabaseDirectory);
        Assert.Equal(@"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\data\frpnexus.db",
            pathSettingsService.PreparedSourceDatabasePath);
        Assert.Equal(@"D:\FrpNexus\data", pathSettingsService.PreparedTargetDatabaseDirectory);
        Assert.Contains("重启", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task SaveSettingsCommand_ShouldCopyLogsWhenLogDirectoryChanges()
    {
        var pathSettingsService = new FakeLocalStoragePathSettingsService(
            new LocalStoragePathSettings(
                @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\logs",
                @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\data"));
        var viewModel = CreateViewModel(
            CreateDefaultSettingsService(),
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService(),
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService(),
            localStoragePathSettingsService: pathSettingsService);
        await viewModel.LoadSettingsAsync();
        viewModel.LogDirectory = @"D:\FrpNexus\logs";

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.Equal(@"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\logs",
            pathSettingsService.PreparedSourceLogDirectory);
        Assert.Equal(@"D:\FrpNexus\logs", pathSettingsService.PreparedTargetLogDirectory);
        Assert.Contains("历史日志已复制到新目录", viewModel.SaveStatusText);
        Assert.Contains("旧文件已保留", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task SaveSettingsCommand_ShouldReportDatabaseAndLogsWhenBothDirectoriesChange()
    {
        var pathSettingsService = new FakeLocalStoragePathSettingsService(
            new LocalStoragePathSettings(
                @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\logs",
                @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\data"));
        var viewModel = CreateViewModel(
            CreateDefaultSettingsService(),
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService(),
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService(),
            localStoragePathSettingsService: pathSettingsService);
        await viewModel.LoadSettingsAsync();
        viewModel.LogDirectory = @"D:\FrpNexus\logs";
        viewModel.SqliteDatabaseDirectory = @"D:\FrpNexus\data";

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.Equal(@"D:\FrpNexus\logs", pathSettingsService.PreparedTargetLogDirectory);
        Assert.Equal(@"D:\FrpNexus\data", pathSettingsService.PreparedTargetDatabaseDirectory);
        Assert.Contains("数据库和历史日志已复制到新目录", viewModel.SaveStatusText);
        Assert.Contains("SQLite 将在重启后生效", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task ClearLocalCacheCommand_ShouldClearDefaultReleaseCacheWhenConfirmed()
    {
        var cacheMaintenanceService = new FakeLocalCacheMaintenanceService(
            new LocalCacheCleanupResult(3, 2048, @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\core\releases"));
        var confirmationDialog = new FakeConfirmationDialogService(confirm: true);
        var viewModel = CreateViewModel(
            CreateDefaultSettingsService(),
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService(),
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService(),
            new FakeLocalFolderLauncherService(),
            cacheMaintenanceService,
            confirmationDialog);

        await viewModel.ClearLocalCacheCommand.ExecuteAsync(null);

        Assert.Equal(1, confirmationDialog.ShowCount);
        Assert.Equal(1, cacheMaintenanceService.ClearDefaultFrpReleaseCacheCallCount);
        Assert.Contains("已清理默认 FRP 核心下载缓存", viewModel.SaveStatusText);
        Assert.Contains("3 个文件", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task ClearLocalCacheCommand_ShouldNotClearCacheWhenCancelled()
    {
        var cacheMaintenanceService = new FakeLocalCacheMaintenanceService();
        var confirmationDialog = new FakeConfirmationDialogService(confirm: false);
        var viewModel = CreateViewModel(
            CreateDefaultSettingsService(),
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService(),
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService(),
            new FakeLocalFolderLauncherService(),
            cacheMaintenanceService,
            confirmationDialog);

        await viewModel.ClearLocalCacheCommand.ExecuteAsync(null);

        Assert.Equal(1, confirmationDialog.ShowCount);
        Assert.Equal(0, cacheMaintenanceService.ClearDefaultFrpReleaseCacheCallCount);
        Assert.Equal("已取消清理本地缓存。", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task DownloadLatestFrpReleaseCommand_ShouldPrepareSelectedFrpcWithLatestVersion()
    {
        var releaseService = new FakeFrpReleaseService(
            new FrpReleaseVersion("v0.60.0", new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero)),
            new FrpReleaseVersion("v0.61.1", new DateTimeOffset(2025, 2, 3, 0, 0, 0, TimeSpan.Zero)));
        var viewModel = CreateViewModel(
            CreateDefaultSettingsService(),
            releaseService,
            new FakeFilePickerService(@"D:\FrpNexus\downloads"),
            new FakeFrpCoreDownloadOptionsDialogService(),
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService());

        await viewModel.DownloadLatestFrpReleaseCommand.ExecuteAsync(null);

        Assert.NotNull(releaseService.PreparedRequest);
        Assert.Equal("v0.61.1", releaseService.PreparedRequest!.Version);
        Assert.Equal("windows_amd64", releaseService.PreparedRequest.TargetRuntime);
        Assert.Equal("frpc", releaseService.PreparedRequest.BinaryName);
        Assert.Equal(@"D:\FrpNexus\downloads", releaseService.PreparedRequest.DownloadDirectory);
        Assert.Equal("GitHub Releases", releaseService.LastListSourceOptions?.SourceKind);
        Assert.Equal("GitHub Releases", releaseService.PreparedRequest.SourceOptions?.SourceKind);
        Assert.Contains("v0.61.1", viewModel.SaveStatusText);
        Assert.Contains(@"D:\FrpNexus\downloads\v0.61.1\windows_amd64\frpc.exe", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task DownloadLatestFrpReleaseCommand_ShouldRequireSavedCustomUrlWhenCustomSourceSelected()
    {
        var releaseService = new FakeFrpReleaseService(
            new FrpReleaseVersion("v0.61.1", new DateTimeOffset(2025, 2, 3, 0, 0, 0, TimeSpan.Zero)));
        var filePicker = new FakeFilePickerService(@"D:\FrpNexus\downloads");
        var downloadOptionsDialog = new FakeFrpCoreDownloadOptionsDialogService();
        var viewModel = CreateViewModel(
            CreateDefaultSettingsService("Custom", customFrpDownloadSourceUrl: string.Empty),
            releaseService,
            filePicker,
            downloadOptionsDialog,
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService());
        await viewModel.LoadSettingsAsync();

        await viewModel.DownloadLatestFrpReleaseCommand.ExecuteAsync(null);

        Assert.Equal(0, downloadOptionsDialog.ShowCount);
        Assert.Equal(0, filePicker.PickFrpDownloadDirectoryCount);
        Assert.Equal(0, releaseService.ListAvailableVersionsCallCount);
        Assert.Null(releaseService.PreparedRequest);
        Assert.Equal("请先输入并保存可信赖的自定义镜像源地址。", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task DownloadLatestFrpReleaseCommand_ShouldUseSavedCustomUrlWhenCustomSourceSelected()
    {
        var releaseService = new FakeFrpReleaseService(
            new FrpReleaseVersion("v0.61.1", new DateTimeOffset(2025, 2, 3, 0, 0, 0, TimeSpan.Zero)));
        var viewModel = CreateViewModel(
            CreateDefaultSettingsService(
                "Custom",
                "https://mirror.example.com/repos/fatedier/frp/releases"),
            releaseService,
            new FakeFilePickerService(@"D:\FrpNexus\downloads"),
            new FakeFrpCoreDownloadOptionsDialogService(),
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService());
        await viewModel.LoadSettingsAsync();

        await viewModel.DownloadLatestFrpReleaseCommand.ExecuteAsync(null);

        Assert.Equal("Custom", releaseService.LastListSourceOptions?.SourceKind);
        Assert.Equal("https://mirror.example.com/repos/fatedier/frp/releases", releaseService.LastListSourceOptions?.CustomReleasesApiUrl);
        Assert.NotNull(releaseService.PreparedRequest);
        Assert.Equal("Custom", releaseService.PreparedRequest!.SourceOptions?.SourceKind);
        Assert.Equal("https://mirror.example.com/repos/fatedier/frp/releases", releaseService.PreparedRequest.SourceOptions?.CustomReleasesApiUrl);
    }

    [Fact]
    public async Task DownloadLatestFrpReleaseCommand_ShouldUseDialogSelectedBinaryAndRuntime()
    {
        var releaseService = new FakeFrpReleaseService(
            new FrpReleaseVersion("v0.61.1", new DateTimeOffset(2025, 2, 3, 0, 0, 0, TimeSpan.Zero)));
        var downloadOptionsDialog = new FakeFrpCoreDownloadOptionsDialogService(
            new FrpCoreDownloadOptions("frps", "linux_arm64"));
        var viewModel = CreateViewModel(
            CreateDefaultSettingsService(),
            releaseService,
            new FakeFilePickerService(@"D:\FrpNexus\downloads"),
            downloadOptionsDialog,
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService());

        await viewModel.DownloadLatestFrpReleaseCommand.ExecuteAsync(null);

        Assert.Equal(1, downloadOptionsDialog.ShowCount);
        Assert.NotNull(releaseService.PreparedRequest);
        Assert.Equal("v0.61.1", releaseService.PreparedRequest!.Version);
        Assert.Equal("linux_arm64", releaseService.PreparedRequest.TargetRuntime);
        Assert.Equal("frps", releaseService.PreparedRequest.BinaryName);
        Assert.Equal(@"D:\FrpNexus\downloads", releaseService.PreparedRequest.DownloadDirectory);
    }

    [Fact]
    public async Task DownloadLatestFrpReleaseCommand_ShouldCancelWhenDownloadOptionsDialogIsCancelled()
    {
        var releaseService = new FakeFrpReleaseService(
            new FrpReleaseVersion("v0.61.1", new DateTimeOffset(2025, 2, 3, 0, 0, 0, TimeSpan.Zero)));
        var filePicker = new FakeFilePickerService(@"D:\FrpNexus\downloads");
        var viewModel = CreateViewModel(
            CreateDefaultSettingsService(),
            releaseService,
            filePicker,
            new FakeFrpCoreDownloadOptionsDialogService(null),
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService());

        await viewModel.DownloadLatestFrpReleaseCommand.ExecuteAsync(null);

        Assert.Equal(0, filePicker.PickFrpDownloadDirectoryCount);
        Assert.Equal(0, releaseService.ListAvailableVersionsCallCount);
        Assert.Null(releaseService.PreparedRequest);
        Assert.Equal("已取消 FRP 核心下载。", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task DownloadLatestFrpReleaseCommand_ShouldReportEmptyReleaseList()
    {
        var viewModel = CreateViewModel(
            CreateDefaultSettingsService(),
            new FakeFrpReleaseService(),
            new FakeFilePickerService(@"D:\FrpNexus\downloads"),
            new FakeFrpCoreDownloadOptionsDialogService(),
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService());

        await viewModel.DownloadLatestFrpReleaseCommand.ExecuteAsync(null);

        Assert.Equal("未找到可下载的 FRP Release，请稍后重试。", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task DownloadLatestFrpReleaseCommand_ShouldCancelWhenDownloadDirectoryIsNotSelected()
    {
        var releaseService = new FakeFrpReleaseService(
            new FrpReleaseVersion("v0.61.1", new DateTimeOffset(2025, 2, 3, 0, 0, 0, TimeSpan.Zero)));
        var filePicker = new FakeFilePickerService();
        var viewModel = CreateViewModel(
            CreateDefaultSettingsService(),
            releaseService,
            filePicker,
            new FakeFrpCoreDownloadOptionsDialogService(),
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService());

        await viewModel.DownloadLatestFrpReleaseCommand.ExecuteAsync(null);

        Assert.Equal(1, filePicker.PickFrpDownloadDirectoryCount);
        Assert.Equal(0, releaseService.ListAvailableVersionsCallCount);
        Assert.Null(releaseService.PreparedRequest);
        Assert.Equal("已取消 FRP 核心下载。", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task DownloadLatestFrpReleaseCommand_ShouldPreventDuplicateExecutionWhileRunning()
    {
        var releaseService = new BlockingFrpReleaseService(
            new FrpReleaseVersion("v0.61.1", new DateTimeOffset(2025, 2, 3, 0, 0, 0, TimeSpan.Zero)));
        var viewModel = CreateViewModel(
            CreateDefaultSettingsService(),
            releaseService,
            new FakeFilePickerService(@"D:\FrpNexus\downloads"),
            new FakeFrpCoreDownloadOptionsDialogService(),
            new FakeNodeManagementService(),
            new FakeNodeCredentialSecretService());

        var runningTask = viewModel.DownloadLatestFrpReleaseCommand.ExecuteAsync(null);
        await releaseService.WaitUntilPreparingAsync();

        Assert.True(viewModel.IsFrpReleaseDownloading);
        Assert.False(viewModel.DownloadLatestFrpReleaseCommand.CanExecute(null));

        releaseService.CompletePreparation();
        await runningTask;

        Assert.False(viewModel.IsFrpReleaseDownloading);
        Assert.True(viewModel.DownloadLatestFrpReleaseCommand.CanExecute(null));
    }

    [Fact]
    public async Task LoadSettingsAsync_ShouldSummarizeCredentialSecurityState()
    {
        var nodeManagementService = new FakeNodeManagementService(
            CreateNode("session-node", "SessionPassword"),
            CreateNode("private-key-node", @"PrivateKey:C:\Users\Arturia\.ssh\id_ed25519"),
            CreateNode("agent-node", "SshAgent"));
        var secretService = new FakeNodeCredentialSecretService("session-node");
        var viewModel = CreateViewModel(
            CreateDefaultSettingsService(),
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService(),
            nodeManagementService,
            secretService);

        await viewModel.LoadSettingsAsync();

        Assert.Equal(3, viewModel.CredentialNodeCount);
        Assert.Equal(1, viewModel.SessionPasswordNodeCount);
        Assert.Equal(1, viewModel.PrivateKeyNodeCount);
        Assert.Equal(1, viewModel.SshAgentNodeCount);
        Assert.Equal(1, viewModel.SavedSessionPasswordNodeCount);
        Assert.Equal("已读取 3 个节点的认证状态，其中 1 个节点保存了会话密码。", viewModel.CredentialSecurityStatusText);
        var privateKeyRow = Assert.Single(viewModel.CredentialSecurityNodes, row => row.NodeName == "private-key-node");
        Assert.Equal("id_ed25519", privateKeyRow.PrivateKeySummary);
    }

    [Fact]
    public async Task ClearSavedSessionPasswordCommand_ShouldDeleteSingleNodeAndRefreshState()
    {
        var nodeManagementService = new FakeNodeManagementService(
            CreateNode("session-node", "SessionPassword"));
        var secretService = new FakeNodeCredentialSecretService("session-node");
        var viewModel = CreateViewModel(
            CreateDefaultSettingsService(),
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService(),
            nodeManagementService,
            secretService);
        await viewModel.LoadSettingsAsync();

        var row = Assert.Single(viewModel.CredentialSecurityNodes);
        await row.ClearSavedSessionPasswordCommand.ExecuteAsync(null);

        Assert.Equal(["session-node"], secretService.DeletedNodeNames);
        Assert.Equal(0, viewModel.SavedSessionPasswordNodeCount);
        Assert.False(row.HasSavedSessionPassword);
        Assert.Equal("已清除 session-node 的保存密码。", viewModel.CredentialSecurityStatusText);
    }

    [Fact]
    public async Task ClearAllSavedSessionPasswordsCommand_ShouldDeleteOnlySavedPasswordNodes()
    {
        var nodeManagementService = new FakeNodeManagementService(
            CreateNode("saved-a", "SessionPassword"),
            CreateNode("unsaved", "SessionPassword"),
            CreateNode("saved-b", "PrivateKey"));
        var secretService = new FakeNodeCredentialSecretService("saved-a", "saved-b");
        var viewModel = CreateViewModel(
            CreateDefaultSettingsService(),
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService(),
            nodeManagementService,
            secretService);
        await viewModel.LoadSettingsAsync();

        await viewModel.ClearAllSavedSessionPasswordsCommand.ExecuteAsync(null);

        Assert.Equal(["saved-a", "saved-b"], secretService.DeletedNodeNames);
        Assert.Equal(0, viewModel.SavedSessionPasswordNodeCount);
        Assert.Equal("已清除 2 个节点的保存密码。", viewModel.CredentialSecurityStatusText);
    }

    [Fact]
    public async Task LoadSettingsAsync_ShouldReportCredentialSecurityFailure()
    {
        var viewModel = CreateViewModel(
            CreateDefaultSettingsService(),
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService(),
            new FailingNodeManagementService(),
            new FakeNodeCredentialSecretService());

        await viewModel.LoadSettingsAsync();

        Assert.Equal("凭据安全状态失败，请检查输入、网络或本地数据状态后重试。", viewModel.CredentialSecurityStatusText);
    }

    private static SettingsPageViewModel CreateViewModel(
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
    {
        return new SettingsPageViewModel(
            settingsService,
            frpReleaseService,
            filePickerService,
            downloadOptionsDialogService,
            nodeManagementService,
            nodeCredentialSecretService,
            localFolderLauncherService ?? new FakeLocalFolderLauncherService(),
            localCacheMaintenanceService ?? new FakeLocalCacheMaintenanceService(),
            confirmationDialogService ?? new FakeConfirmationDialogService(),
            localStoragePathSettingsService ?? new FakeLocalStoragePathSettingsService(new LocalStoragePathSettings(
                @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\logs",
                @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\data")));
    }
    private static FakeSettingsService CreateDefaultSettingsService(
        string frpDownloadSource = "GitHub Releases",
        string customFrpDownloadSourceUrl = "")
    {
        return new FakeSettingsService(new FrpNexusSettingsSnapshot(
            frpDownloadSource,
            @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\logs",
            @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\data\frpnexus.db",
            customFrpDownloadSourceUrl,
            @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\data"));
    }

    private static NodeProfile CreateNode(string name, string authentication)
    {
        return new NodeProfile(
            name,
            "127.0.0.1",
            22,
            "root",
            authentication,
            "Linux",
            FrpNexusStatus.Ready,
            FrpNexusStatus.Stopped,
            "-",
            "-",
            "/etc/frp/frpc.toml");
    }

    private sealed class FakeSettingsService(FrpNexusSettingsSnapshot settings) : ISettingsService
    {
        public FrpNexusSettingsSnapshot? SavedSettings { get; private set; }

        public Task<FrpNexusSettingsSnapshot> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(settings);
        }

        public Task SaveSettingsAsync(FrpNexusSettingsSnapshot settingsSnapshot, CancellationToken cancellationToken = default)
        {
            SavedSettings = settingsSnapshot;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingSettingsService : ISettingsService
    {
        public Task<FrpNexusSettingsSnapshot> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("设置读取失败");
        }

        public Task SaveSettingsAsync(FrpNexusSettingsSnapshot settings, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("设置保存失败");
        }
    }

    private sealed class FakeNodeManagementService(params NodeProfile[] nodes) : INodeManagementService
    {
        public Task<IReadOnlyList<NodeProfile>> ListNodesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<NodeProfile>>(nodes);
        }

        public Task<NodeProfile?> GetNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(nodes.FirstOrDefault(node =>
                string.Equals(node.Name, nodeName, StringComparison.OrdinalIgnoreCase)));
        }

        public Task SaveNodeAsync(NodeProfile node, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateLastConnectionAsync(
            string nodeName,
            DateTimeOffset connectedAt,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateConnectionTestResultAsync(
            string nodeName,
            FrpNexusStatus status,
            DateTimeOffset testedAt,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FailingNodeManagementService : INodeManagementService
    {
        public Task<IReadOnlyList<NodeProfile>> ListNodesAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("节点读取失败");
        }

        public Task<NodeProfile?> GetNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SaveNodeAsync(NodeProfile node, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task UpdateLastConnectionAsync(
            string nodeName,
            DateTimeOffset connectedAt,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task UpdateConnectionTestResultAsync(
            string nodeName,
            FrpNexusStatus status,
            DateTimeOffset testedAt,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeNodeCredentialSecretService(params string[] savedNodeNames) : INodeCredentialSecretService
    {
        private readonly HashSet<string> _savedNodeNames = new(savedNodeNames, StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _deletedNodeNames = [];

        public IReadOnlyList<string> DeletedNodeNames => _deletedNodeNames;

        public Task<bool> HasSessionPasswordAsync(
            string nodeName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_savedNodeNames.Contains(nodeName));
        }

        public Task<string?> GetSessionPasswordAsync(
            string nodeName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(_savedNodeNames.Contains(nodeName) ? "secret" : null);
        }

        public Task SaveSessionPasswordAsync(
            string nodeName,
            string password,
            CancellationToken cancellationToken = default)
        {
            _savedNodeNames.Add(nodeName);
            return Task.CompletedTask;
        }

        public Task DeleteSessionPasswordAsync(
            string nodeName,
            CancellationToken cancellationToken = default)
        {
            _deletedNodeNames.Add(nodeName);
            _savedNodeNames.Remove(nodeName);
            return Task.CompletedTask;
        }
    }

    private class FakeFrpReleaseService(params FrpReleaseVersion[] versions) : IFrpReleaseService
    {
        public FrpReleasePreparationRequest? PreparedRequest { get; private set; }

        public FrpReleaseSourceOptions? LastListSourceOptions { get; private set; }

        public int ListAvailableVersionsCallCount { get; private set; }

        public Task<IReadOnlyList<FrpReleaseVersion>> ListAvailableVersionsAsync(
            FrpReleaseSourceOptions? sourceOptions = null,
            CancellationToken cancellationToken = default)
        {
            ListAvailableVersionsCallCount++;
            LastListSourceOptions = sourceOptions;
            return Task.FromResult<IReadOnlyList<FrpReleaseVersion>>(versions);
        }

        public virtual Task<FrpReleasePreparationResult> PrepareReleaseAsync(
            FrpReleasePreparationRequest request,
            CancellationToken cancellationToken = default)
        {
            PreparedRequest = request;
            return Task.FromResult(new FrpReleasePreparationResult(
                request.Version,
                request.TargetRuntime,
                request.BinaryName,
                $@"{request.DownloadDirectory}\{request.Version}\{request.TargetRuntime}\{request.BinaryName}.exe",
                new DateTimeOffset(2025, 2, 3, 0, 0, 0, TimeSpan.Zero),
                "FRP 核心已准备完成。"));
        }
    }

    private sealed class FakeFilePickerService(
        string? frpDownloadDirectory = null,
        string? localDirectory = null) : IFilePickerService
    {
        public int PickFrpDownloadDirectoryCount { get; private set; }

        public Task<string?> PickFrpBinaryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> PickLocalFrpcBinaryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> PickLocalFrpcConfigPathAsync(
            string suggestedFileName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> PickFrpDownloadDirectoryAsync(CancellationToken cancellationToken = default)
        {
            PickFrpDownloadDirectoryCount++;
            return Task.FromResult(frpDownloadDirectory);
        }

        public Task<string?> PickLocalDirectoryAsync(
            string title,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(localDirectory);
        }
    }

    private sealed class FakeFrpCoreDownloadOptionsDialogService : IFrpCoreDownloadOptionsDialogService
    {
        private readonly FrpCoreDownloadOptions? _result;

        public FakeFrpCoreDownloadOptionsDialogService()
            : this(new FrpCoreDownloadOptions("frpc", "windows_amd64"))
        {
        }

        public FakeFrpCoreDownloadOptionsDialogService(FrpCoreDownloadOptions? result)
        {
            _result = result;
        }

        public int ShowCount { get; private set; }

        public Task<FrpCoreDownloadOptions?> ShowAsync(CancellationToken cancellationToken = default)
        {
            ShowCount++;
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeLocalFolderLauncherService : ILocalFolderLauncherService
    {
        public string? OpenedPath { get; private set; }

        public Task OpenFolderAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            OpenedPath = folderPath;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLocalCacheMaintenanceService(
        LocalCacheCleanupResult? cleanupResult = null) : ILocalCacheMaintenanceService
    {
        public int ClearDefaultFrpReleaseCacheCallCount { get; private set; }

        public Task<LocalCacheCleanupResult> ClearDefaultFrpReleaseCacheAsync(
            CancellationToken cancellationToken = default)
        {
            ClearDefaultFrpReleaseCacheCallCount++;
            return Task.FromResult(cleanupResult
                ?? new LocalCacheCleanupResult(0, 0, @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\core\releases"));
        }
    }

    private sealed class FakeConfirmationDialogService(bool confirm = true) : IConfirmationDialogService
    {
        public int ShowCount { get; private set; }

        public Task<bool> ShowAsync(
            ConfirmationDialogRequest request,
            CancellationToken cancellationToken = default)
        {
            ShowCount++;
            return Task.FromResult(confirm);
        }

        public Task<ConfirmationDialogResult> ShowChoiceAsync(
            ConfirmationDialogChoiceRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeLocalStoragePathSettingsService(
        LocalStoragePathSettings settings) : ILocalStoragePathSettingsService
    {
        public LocalStoragePathSettings? SavedSettings { get; private set; }

        public string? PreparedSourceDatabasePath { get; private set; }

        public string? PreparedTargetDatabaseDirectory { get; private set; }

        public string? PreparedSourceLogDirectory { get; private set; }

        public string? PreparedTargetLogDirectory { get; private set; }

        public LocalStoragePathSettings GetSettings()
        {
            return SavedSettings ?? settings;
        }

        public string GetLogDirectory()
        {
            return GetSettings().LogDirectory;
        }

        public string GetSqliteDatabaseDirectory()
        {
            return GetSettings().SqliteDatabaseDirectory;
        }

        public string GetSqliteDatabasePath()
        {
            return Path.Combine(GetSettings().SqliteDatabaseDirectory, "frpnexus.db");
        }

        public Task SaveSettingsAsync(
            LocalStoragePathSettings pathSettings,
            CancellationToken cancellationToken = default)
        {
            SavedSettings = pathSettings;
            return Task.CompletedTask;
        }

        public Task<SqliteDatabaseRelocationResult> PrepareSqliteDatabaseDirectoryAsync(
            string currentDatabasePath,
            string targetDatabaseDirectory,
            CancellationToken cancellationToken = default)
        {
            PreparedSourceDatabasePath = currentDatabasePath;
            PreparedTargetDatabaseDirectory = targetDatabaseDirectory;
            SavedSettings = (SavedSettings ?? settings) with
            {
                SqliteDatabaseDirectory = targetDatabaseDirectory
            };

            return Task.FromResult(new SqliteDatabaseRelocationResult(
                currentDatabasePath,
                Path.Combine(targetDatabaseDirectory, "frpnexus.db"),
                Copied: true,
                BackupCreated: false,
                BackupPath: null));
        }

        public Task<LogDirectoryRelocationResult> PrepareLogDirectoryAsync(
            string currentLogDirectory,
            string targetLogDirectory,
            CancellationToken cancellationToken = default)
        {
            PreparedSourceLogDirectory = currentLogDirectory;
            PreparedTargetLogDirectory = targetLogDirectory;
            SavedSettings = (SavedSettings ?? settings) with
            {
                LogDirectory = targetLogDirectory
            };

            return Task.FromResult(new LogDirectoryRelocationResult(
                currentLogDirectory,
                targetLogDirectory,
                CopiedFileCount: 2,
                SkippedFileCount: 0));
        }
    }

    private sealed class BlockingFrpReleaseService(params FrpReleaseVersion[] versions) : FakeFrpReleaseService(versions)
    {
        private readonly TaskCompletionSource _preparing = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _complete = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitUntilPreparingAsync()
        {
            return _preparing.Task;
        }

        public void CompletePreparation()
        {
            _complete.SetResult();
        }

        public override async Task<FrpReleasePreparationResult> PrepareReleaseAsync(
            FrpReleasePreparationRequest request,
            CancellationToken cancellationToken = default)
        {
            _preparing.SetResult();
            await _complete.Task.WaitAsync(cancellationToken);
            return await base.PrepareReleaseAsync(request, cancellationToken);
        }
    }
}
