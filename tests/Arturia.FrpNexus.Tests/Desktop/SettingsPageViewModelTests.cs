using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class SettingsPageViewModelTests
{
    [Fact]
    public async Task LoadSettingsAsync_ShouldPopulatePersistedSettings()
    {
        var settingsService = new FakeSettingsService(new FrpNexusSettingsSnapshot(
            "Dark",
            "en-US",
            "GHProxy",
            @"D:\FrpNexus\core",
            @"D:\FrpNexus\configs",
            @"D:\FrpNexus\logs",
            @"D:\FrpNexus\data\frpnexus.db"));
        var viewModel = new SettingsPageViewModel(settingsService);

        await viewModel.LoadSettingsAsync();

        Assert.Equal("Dark", viewModel.Theme);
        Assert.Equal("深色模式", viewModel.SelectedThemeOption.DisplayText);
        Assert.Equal("en-US", viewModel.Language);
        Assert.Equal("English (US)", viewModel.SelectedLanguageOption.DisplayText);
        Assert.Equal("GHProxy", viewModel.FrpDownloadSource);
        Assert.Equal("GHProxy (国内加速)", viewModel.SelectedFrpDownloadSourceOption.DisplayText);
        Assert.Equal(@"D:\FrpNexus\core", viewModel.CoreDirectory);
        Assert.Equal(@"D:\FrpNexus\configs", viewModel.ConfigDirectory);
        Assert.Equal(@"D:\FrpNexus\logs", viewModel.LogDirectory);
        Assert.Equal(@"D:\FrpNexus\data\frpnexus.db", viewModel.SqliteDatabasePath);
    }

    [Fact]
    public async Task SaveSettingsCommand_ShouldPersistEditableSettings()
    {
        var settingsService = new FakeSettingsService(new FrpNexusSettingsSnapshot(
            "Light",
            "zh-CN",
            "GitHub Releases",
            @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\core",
            @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\configs",
            @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\logs",
            @"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\data\frpnexus.db"));
        var viewModel = new SettingsPageViewModel(settingsService);
        await viewModel.LoadSettingsAsync();

        viewModel.SelectedThemeOption = viewModel.ThemeOptions.Single(option => option.Value == "System");
        viewModel.SelectedLanguageOption = viewModel.LanguageOptions.Single(option => option.Value == "en-US");
        viewModel.SelectedFrpDownloadSourceOption = viewModel.FrpDownloadSourceOptions.Single(option => option.Value == "Custom");
        viewModel.CoreDirectory = @"D:\Tools\frp-core";
        viewModel.ConfigDirectory = @"D:\Tools\frp-configs";
        viewModel.LogDirectory = @"D:\Tools\frp-logs";

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.NotNull(settingsService.SavedSettings);
        Assert.Equal("System", settingsService.SavedSettings.Theme);
        Assert.Equal("en-US", settingsService.SavedSettings.Language);
        Assert.Equal("Custom", settingsService.SavedSettings.FrpDownloadSource);
        Assert.Equal(@"D:\Tools\frp-core", settingsService.SavedSettings.CoreDirectory);
        Assert.Equal(@"D:\Tools\frp-configs", settingsService.SavedSettings.ConfigDirectory);
        Assert.Equal(@"D:\Tools\frp-logs", settingsService.SavedSettings.LogDirectory);
        Assert.Equal(@"C:\Users\Arturia\AppData\Local\Arturia\FrpNexus\data\frpnexus.db", settingsService.SavedSettings.SqliteDatabasePath);
        Assert.Equal("设置已保存到本地 SQLite", viewModel.SaveStatusText);
    }

    [Fact]
    public void SettingsViewModel_ShouldExposeOnlyOrdinarySettingsForPersistence()
    {
        var persistedPropertyNames = typeof(FrpNexusSettingsSnapshot)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(persistedPropertyNames, name => name.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(persistedPropertyNames, name => name.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(persistedPropertyNames, name => name.Contains("PrivateKey", StringComparison.OrdinalIgnoreCase));
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
}
