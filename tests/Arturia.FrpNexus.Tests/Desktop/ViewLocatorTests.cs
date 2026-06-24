using Avalonia.Controls;
using Arturia.FrpNexus.Desktop;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class ViewLocatorTests
{
    [Theory]
    [InlineData("DashboardPageViewModel => new DashboardPageView()")]
    [InlineData("NodesPageViewModel => new NodesPageView()")]
    [InlineData("TunnelsPageViewModel => new TunnelsPageView()")]
    [InlineData("ConfigurationsPageViewModel => new ConfigurationsPageView()")]
    [InlineData("RuntimePageViewModel => new RuntimePageView()")]
    [InlineData("LogsPageViewModel => new LogsPageView()")]
    [InlineData("SettingsPageViewModel => new SettingsPageView()")]
    public void ViewLocatorSource_ShouldExplicitlyMapMainPageViewModels(string expectedMapping)
    {
        var source = ReadViewLocatorSource();

        Assert.Contains(expectedMapping, source);
    }

    [Fact]
    public void Build_ShouldReturnFallbackTextBlockForUnknownViewModel()
    {
        var locator = new ViewLocator();

        var view = Assert.IsType<TextBlock>(locator.Build(new UnknownPageViewModel()));

        Assert.Contains(nameof(UnknownPageViewModel), view.Text);
    }

    [Fact]
    public void ViewLocatorSource_ShouldNotUseReflectionOrTrimmingSuppression()
    {
        var source = ReadViewLocatorSource();

        Assert.DoesNotContain("RequiresUnreferencedCode", source);
        Assert.DoesNotContain("Type.GetType", source);
        Assert.DoesNotContain("Activator.CreateInstance", source);
    }

    private static string ReadViewLocatorSource()
    {
        return File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "Arturia.FrpNexus.Desktop", "ViewLocator.cs"));
    }

    private sealed class UnknownPageViewModel : PageViewModel
    {
        public UnknownPageViewModel()
            : base("Unknown", "Unknown")
        {
        }
    }

    private static string GetRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!, "..", "..", ".."));
    }
}
