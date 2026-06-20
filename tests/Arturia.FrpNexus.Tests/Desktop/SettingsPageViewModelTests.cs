using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class SettingsPageViewModelTests
{
    [Fact]
    public async Task LoadSettingsAsync_ShouldPopulatePersistedSettings()
    {
        var settingsService = new FakeSettingsService(new FrpNexusSettingsSnapshot(
            "GHProxy",
            @"D:\FrpNexus\core",
            @"D:\FrpNexus\configs",
            @"D:\FrpNexus\logs",
            @"D:\FrpNexus\data\frpnexus.db"));
        var viewModel = new SettingsPageViewModel(settingsService);

        await viewModel.LoadSettingsAsync();

        Assert.Equal("GHProxy", viewModel.FrpDownloadSource);
        Assert.Equal("GHProxy (国内加速)", viewModel.SelectedFrpDownloadSourceOption.DisplayText);
        Assert.Equal(@"D:\FrpNexus\core", viewModel.CoreDirectory);
        Assert.Equal(@"D:\FrpNexus\configs", viewModel.ConfigDirectory);
        Assert.Equal(@"D:\FrpNexus\logs", viewModel.LogDirectory);
        Assert.Equal(@"D:\FrpNexus\data\frpnexus.db", viewModel.SqliteDatabasePath);
        Assert.Equal("已加载本地设置。", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task SaveSettingsCommand_ShouldPersistEditableSettings()
    {
        var settingsService = new FakeSettingsService(new FrpNexusSettingsSnapshot(
            "GitHub Releases",
            @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\core",
            @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\configs",
            @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\logs",
            @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\data\frpnexus.db"));
        var viewModel = new SettingsPageViewModel(settingsService);
        await viewModel.LoadSettingsAsync();

        viewModel.SelectedFrpDownloadSourceOption = viewModel.FrpDownloadSourceOptions.Single(option => option.Value == "Custom");
        viewModel.CoreDirectory = @"D:\Tools\frp-core";
        viewModel.ConfigDirectory = @"D:\Tools\frp-configs";
        viewModel.LogDirectory = @"D:\Tools\frp-logs";

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.NotNull(settingsService.SavedSettings);
        Assert.Equal("Custom", settingsService.SavedSettings!.FrpDownloadSource);
        Assert.Equal(@"D:\Tools\frp-core", settingsService.SavedSettings.CoreDirectory);
        Assert.Equal(@"D:\Tools\frp-configs", settingsService.SavedSettings.ConfigDirectory);
        Assert.Equal(@"D:\Tools\frp-logs", settingsService.SavedSettings.LogDirectory);
        Assert.Equal(@"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\data\frpnexus.db", settingsService.SavedSettings.SqliteDatabasePath);
        Assert.Equal("设置已保存到本地 SQLite。", viewModel.SaveStatusText);
    }

    [Fact]
    public async Task SaveSettingsCommand_ShouldReportRecoverableFailure()
    {
        var viewModel = new SettingsPageViewModel(new FailingSettingsService());

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.Equal("设置保存失败，请检查输入、网络或本地数据状态后重试。", viewModel.SaveStatusText);
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
}
