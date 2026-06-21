using Arturia.FrpNexus.Application.Abstractions;
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
        var viewModel = new SettingsPageViewModel(
            settingsService,
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService());

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
        var viewModel = new SettingsPageViewModel(
            settingsService,
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService());
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
        Assert.Equal("设置已保存到本地 SQLite。", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task SaveCustomFrpDownloadSourceUrlCommand_ShouldPersistUrlAndShowSafetyNotice()
    {
        var settingsService = CreateDefaultSettingsService();
        var viewModel = new SettingsPageViewModel(
            settingsService,
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService());
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
        var viewModel = new SettingsPageViewModel(
            settingsService,
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService());
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
        var viewModel = new SettingsPageViewModel(
            new FailingSettingsService(),
            new FakeFrpReleaseService(),
            new FakeFilePickerService(),
            new FakeFrpCoreDownloadOptionsDialogService());

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.Equal("设置保存失败，请检查输入、网络或本地数据状态后重试。", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task DownloadLatestFrpReleaseCommand_ShouldPrepareSelectedFrpcWithLatestVersion()
    {
        var releaseService = new FakeFrpReleaseService(
            new FrpReleaseVersion("v0.60.0", new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero)),
            new FrpReleaseVersion("v0.61.1", new DateTimeOffset(2025, 2, 3, 0, 0, 0, TimeSpan.Zero)));
        var viewModel = new SettingsPageViewModel(
            CreateDefaultSettingsService(),
            releaseService,
            new FakeFilePickerService(@"D:\FrpNexus\downloads"),
            new FakeFrpCoreDownloadOptionsDialogService());

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
        var viewModel = new SettingsPageViewModel(
            CreateDefaultSettingsService("Custom", customFrpDownloadSourceUrl: string.Empty),
            releaseService,
            filePicker,
            downloadOptionsDialog);
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
        var viewModel = new SettingsPageViewModel(
            CreateDefaultSettingsService(
                "Custom",
                "https://mirror.example.com/repos/fatedier/frp/releases"),
            releaseService,
            new FakeFilePickerService(@"D:\FrpNexus\downloads"),
            new FakeFrpCoreDownloadOptionsDialogService());
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
        var viewModel = new SettingsPageViewModel(
            CreateDefaultSettingsService(),
            releaseService,
            new FakeFilePickerService(@"D:\FrpNexus\downloads"),
            downloadOptionsDialog);

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
        var viewModel = new SettingsPageViewModel(
            CreateDefaultSettingsService(),
            releaseService,
            filePicker,
            new FakeFrpCoreDownloadOptionsDialogService(null));

        await viewModel.DownloadLatestFrpReleaseCommand.ExecuteAsync(null);

        Assert.Equal(0, filePicker.PickFrpDownloadDirectoryCount);
        Assert.Equal(0, releaseService.ListAvailableVersionsCallCount);
        Assert.Null(releaseService.PreparedRequest);
        Assert.Equal("已取消 FRP 核心下载。", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task DownloadLatestFrpReleaseCommand_ShouldReportEmptyReleaseList()
    {
        var viewModel = new SettingsPageViewModel(
            CreateDefaultSettingsService(),
            new FakeFrpReleaseService(),
            new FakeFilePickerService(@"D:\FrpNexus\downloads"),
            new FakeFrpCoreDownloadOptionsDialogService());

        await viewModel.DownloadLatestFrpReleaseCommand.ExecuteAsync(null);

        Assert.Equal("未找到可下载的 FRP Release，请稍后重试。", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task DownloadLatestFrpReleaseCommand_ShouldCancelWhenDownloadDirectoryIsNotSelected()
    {
        var releaseService = new FakeFrpReleaseService(
            new FrpReleaseVersion("v0.61.1", new DateTimeOffset(2025, 2, 3, 0, 0, 0, TimeSpan.Zero)));
        var filePicker = new FakeFilePickerService();
        var viewModel = new SettingsPageViewModel(
            CreateDefaultSettingsService(),
            releaseService,
            filePicker,
            new FakeFrpCoreDownloadOptionsDialogService());

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
        var viewModel = new SettingsPageViewModel(
            CreateDefaultSettingsService(),
            releaseService,
            new FakeFilePickerService(@"D:\FrpNexus\downloads"),
            new FakeFrpCoreDownloadOptionsDialogService());

        var runningTask = viewModel.DownloadLatestFrpReleaseCommand.ExecuteAsync(null);
        await releaseService.WaitUntilPreparingAsync();

        Assert.True(viewModel.IsFrpReleaseDownloading);
        Assert.False(viewModel.DownloadLatestFrpReleaseCommand.CanExecute(null));

        releaseService.CompletePreparation();
        await runningTask;

        Assert.False(viewModel.IsFrpReleaseDownloading);
        Assert.True(viewModel.DownloadLatestFrpReleaseCommand.CanExecute(null));
    }

    private static FakeSettingsService CreateDefaultSettingsService(
        string frpDownloadSource = "GitHub Releases",
        string customFrpDownloadSourceUrl = "")
    {
        return new FakeSettingsService(new FrpNexusSettingsSnapshot(
            frpDownloadSource,
            @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\logs",
            @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\data\frpnexus.db",
            customFrpDownloadSourceUrl));
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

    private sealed class FakeFilePickerService(string? frpDownloadDirectory = null) : IFilePickerService
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
