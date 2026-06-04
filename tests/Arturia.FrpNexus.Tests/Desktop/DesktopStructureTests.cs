using Arturia.FrpNexus.Desktop.Composition;
using Arturia.FrpNexus.Desktop.Converters;
using Arturia.FrpNexus.Desktop.Logging;
using Arturia.FrpNexus.Desktop.ViewModels;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;
using Arturia.FrpNexus.Desktop.Views;
using Arturia.FrpNexus.Desktop.Views.Pages;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class DesktopStructureTests
{
    [Fact]
    public void StyleResources_ShouldUseSeparatedResponsibilityDictionaries()
    {
        var desktopProject = GetDesktopProjectPath();
        var stylesPath = Path.Combine(desktopProject, "Styles");

        Assert.True(File.Exists(Path.Combine(stylesPath, "DesignTokens.axaml")));
        Assert.True(File.Exists(Path.Combine(stylesPath, "Controls.axaml")));
        Assert.True(File.Exists(Path.Combine(stylesPath, "Navigation.axaml")));
        Assert.True(File.Exists(Path.Combine(stylesPath, "Status.axaml")));
        Assert.True(File.Exists(Path.Combine(stylesPath, "CodePanels.axaml")));
    }

    [Fact]
    public void AppStyles_ShouldReferenceSeparatedStyleDictionaries()
    {
        var appXaml = File.ReadAllText(Path.Combine(GetDesktopProjectPath(), "App.axaml"));

        Assert.Contains("Styles/Controls.axaml", appXaml);
        Assert.Contains("Styles/Navigation.axaml", appXaml);
        Assert.Contains("Styles/Status.axaml", appXaml);
        Assert.Contains("Styles/CodePanels.axaml", appXaml);
    }

    [Fact]
    public void DesignTokens_ShouldExposeFrpSemanticResourcesAndCompatibilityAliases()
    {
        var tokens = File.ReadAllText(Path.Combine(GetDesktopProjectPath(), "Styles", "DesignTokens.axaml"));

        Assert.Contains("FrpBackgroundBrush", tokens);
        Assert.Contains("FrpSidebarBackgroundBrush", tokens);
        Assert.Contains("FrpSurfaceWhiteBrush", tokens);
        Assert.Contains("FrpBorderDefaultBrush", tokens);
        Assert.Contains("FrpPrimaryBrush", tokens);
        Assert.Contains("FrpStatusSuccessBrush", tokens);
        Assert.Contains("FrpCodePanelBackgroundBrush", tokens);
        Assert.Contains("Brush.AppBackground", tokens);
    }

    [Fact]
    public void DesktopInfrastructure_ShouldStayInDedicatedNamespaces()
    {
        Assert.Equal("Arturia.FrpNexus.Desktop.Converters", typeof(StatusTextConverter).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.Converters", typeof(StatusClassesConverter).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.Converters", typeof(TunnelProtocolTextConverter).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.Logging", typeof(DesktopLogging).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.Composition", typeof(DesktopCompositionRoot).Namespace);
    }

    [Fact]
    public void PageViewsAndViewModels_ShouldMirrorMainModules()
    {
        Assert.Equal("Arturia.FrpNexus.Desktop.Views.Pages", typeof(DashboardPageView).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.Views.Pages", typeof(NodesPageView).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.Views.Pages", typeof(TunnelsPageView).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.Views.Pages", typeof(ConfigurationsPageView).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.Views.Pages", typeof(RuntimePageView).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.Views.Pages", typeof(LogsPageView).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.Views.Pages", typeof(SettingsPageView).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.ViewModels.Pages", typeof(DashboardPageViewModel).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.ViewModels.Pages", typeof(NodesPageViewModel).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.ViewModels.Pages", typeof(TunnelsPageViewModel).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.ViewModels.Pages", typeof(ConfigurationsPageViewModel).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.ViewModels.Pages", typeof(RuntimePageViewModel).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.ViewModels.Pages", typeof(LogsPageViewModel).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.ViewModels.Pages", typeof(SettingsPageViewModel).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.Views", typeof(MainWindow).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.ViewModels", typeof(MainWindowViewModel).Namespace);
    }

    private static string GetDesktopProjectPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "Arturia.FrpNexus.Desktop");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate src/Arturia.FrpNexus.Desktop from test output.");
    }
}
