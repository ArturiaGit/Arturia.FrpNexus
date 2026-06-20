using Arturia.FrpNexus.Desktop.Composition;
using Arturia.FrpNexus.Desktop.Converters;
using Arturia.FrpNexus.Desktop.Logging;
using Arturia.FrpNexus.Desktop.Services;
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
        Assert.Equal("Arturia.FrpNexus.Desktop.Services", typeof(ILocalApplicationLogService).Namespace);
        Assert.Equal("Arturia.FrpNexus.Desktop.Services", typeof(LocalApplicationLogService).Namespace);
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

    [Fact]
    public void NodeConnectionWorkflowDialogView_ShouldNotContainRuntimeCommandButtons()
    {
        var dialogXaml = File.ReadAllText(Path.Combine(
            GetDesktopProjectPath(),
            "Views",
            "Dialogs",
            "NodeConnectionWorkflowDialogView.axaml"));

        Assert.DoesNotContain("StartRemoteFrpsCommand", dialogXaml);
        Assert.DoesNotContain("StopRemoteFrpsCommand", dialogXaml);
        Assert.DoesNotContain("RestartRemoteFrpsCommand", dialogXaml);
        Assert.DoesNotContain("RefreshRemoteFrpsStatusCommand", dialogXaml);
    }

    [Fact]
    public void NodeConnectionWorkflowDialog_ShouldBeHostedInsideMainWindow()
    {
        var desktopProject = GetDesktopProjectPath();
        var serviceCode = File.ReadAllText(Path.Combine(
            desktopProject,
            "Services",
            "AvaloniaNodeConnectionWorkflowDialogService.cs"));
        var mainWindowXaml = File.ReadAllText(Path.Combine(desktopProject, "Views", "MainWindow.axaml"));

        Assert.DoesNotContain("NodeConnectionWorkflowWindow", serviceCode);
        Assert.DoesNotContain(".Show(", serviceCode);
        Assert.Contains("CurrentModalDialog", mainWindowXaml);
        Assert.Contains("IsWorkflowDialogVisible", mainWindowXaml);
        Assert.Contains("ZIndex=\"1001\"", mainWindowXaml);
        Assert.Contains("Width=\"640\"", mainWindowXaml);
        Assert.Contains("MaxHeight=\"680\"", mainWindowXaml);
        Assert.Contains("VerticalAlignment=\"Stretch\"", mainWindowXaml);
        Assert.Contains("HorizontalAlignment=\"Stretch\"", mainWindowXaml);
    }

    [Fact]
    public void MainWindow_ShouldExposeConfirmationDialogTemplate()
    {
        var desktopProject = GetDesktopProjectPath();
        var mainWindowXaml = File.ReadAllText(Path.Combine(desktopProject, "Views", "MainWindow.axaml"));
        var confirmationXaml = File.ReadAllText(Path.Combine(
            desktopProject,
            "Views",
            "Dialogs",
            "ConfirmationDialogView.axaml"));

        Assert.Contains("ConfirmationDialogViewModel", mainWindowXaml);
        Assert.Contains("ConfirmationDialogView", mainWindowXaml);
        Assert.Contains("IsConfirmationDialogVisible", mainWindowXaml);
        Assert.Contains("Width=\"460\"", mainWindowXaml);
        Assert.Contains("MaxHeight=\"360\"", mainWindowXaml);
        Assert.Contains("VerticalAlignment=\"Center\"", mainWindowXaml);
        Assert.Contains("ConfirmCommand", confirmationXaml);
        Assert.Contains("CancelCommand", confirmationXaml);
        Assert.Contains("ConfirmButtonText", confirmationXaml);
        Assert.Contains("CancelButtonText", confirmationXaml);
        Assert.DoesNotContain("Width=\"640\"", confirmationXaml);
        Assert.DoesNotContain("MaxHeight=\"680\"", confirmationXaml);
    }

    [Fact]
    public void NodeConnectionWorkflowDialogView_ShouldKeepFooterOutsideScrollableContent()
    {
        var dialogXaml = File.ReadAllText(Path.Combine(
            GetDesktopProjectPath(),
            "Views",
            "Dialogs",
            "NodeConnectionWorkflowDialogView.axaml"));

        Assert.Contains("Grid RowDefinitions=\"Auto,*,Auto\"", dialogXaml);
        Assert.Contains("<ScrollViewer Grid.Row=\"1\"", dialogXaml);
        Assert.Contains("VerticalAlignment=\"Stretch\"", dialogXaml);
        Assert.Contains("Padding=\"16,16,16,0\"", dialogXaml);
        Assert.Contains("ClipToBounds=\"True\"", dialogXaml);
        Assert.Contains("Height=\"72\"", dialogXaml);
        Assert.Contains("<Border Grid.Row=\"2\"", dialogXaml);
        Assert.Contains("Command=\"{Binding CloseCommand}\"", dialogXaml);
    }

    [Fact]
    public void TunnelsPageEditor_ShouldUseRemarkInsteadOfEditableStatusDetail()
    {
        var tunnelsXaml = File.ReadAllText(Path.Combine(
            GetDesktopProjectPath(),
            "Views",
            "Pages",
            "TunnelsPageView.axaml"));

        Assert.Contains("Text=\"备注\"", tunnelsXaml);
        Assert.Contains("Text=\"{Binding FormRemark}\"", tunnelsXaml);
        Assert.DoesNotContain("状态说明", tunnelsXaml);
        Assert.DoesNotContain("FormStatusDetail", tunnelsXaml);
        Assert.DoesNotContain("StatusDetail", tunnelsXaml);
    }

    [Fact]
    public void TunnelsPage_ShouldExposeNodeLevelLocalFrpcControls()
    {
        var tunnelsXaml = File.ReadAllText(Path.Combine(
            GetDesktopProjectPath(),
            "Views",
            "Pages",
            "TunnelsPageView.axaml"));

        Assert.Contains("Text=\"本地 frpc\"", tunnelsXaml);
        Assert.Contains("ClientNodeOptions", tunnelsXaml);
        Assert.Contains("LocalFrpcStatusText", tunnelsXaml);
        Assert.Contains("LocalFrpcEnabledTunnelCountText", tunnelsXaml);
        Assert.DoesNotContain("LocalFrpcManagementPortInput", tunnelsXaml);
        Assert.DoesNotContain("LocalFrpcSuggestedManagementPortText", tunnelsXaml);
        Assert.DoesNotContain("LocalFrpcManagementPortText", tunnelsXaml);
        Assert.DoesNotContain("管理端口", tunnelsXaml);
        Assert.Contains("LocalFrpcBinaryPath", tunnelsXaml);
        Assert.Contains("LocalFrpcConfigPath", tunnelsXaml);
        Assert.Contains("SelectLocalFrpcBinaryCommand", tunnelsXaml);
        Assert.Contains("SelectLocalFrpcConfigCommand", tunnelsXaml);
        Assert.Contains("ToggleLocalFrpcCommand", tunnelsXaml);
        Assert.Contains("LocalFrpcToggleButtonText", tunnelsXaml);
        Assert.DoesNotContain("StartLocalFrpcCommand", tunnelsXaml);
        Assert.DoesNotContain("StopLocalFrpcCommand", tunnelsXaml);
        Assert.Contains("ReloadLocalFrpcCommand", tunnelsXaml);
        Assert.Contains("Content=\"{Binding LocalFrpcToggleButtonText}\"", tunnelsXaml);
        Assert.DoesNotContain("Content=\"启动 frpc\"", tunnelsXaml);
        Assert.DoesNotContain("Content=\"停止 frpc\"", tunnelsXaml);
        Assert.Contains("Content=\"重载配置\"", tunnelsXaml);
        Assert.Contains("Content=\"选择核心\"", tunnelsXaml);
        Assert.Contains("Content=\"选择配置\"", tunnelsXaml);
        Assert.Contains("ToggleTunnelEnabledCommand", tunnelsXaml);
        Assert.DoesNotContain("ToggleTunnelRuntimeCommand", tunnelsXaml);
    }

    [Fact]
    public void ConfigurationsPage_ShouldBeReadonlyFrpcPreview()
    {
        var configurationsXaml = File.ReadAllText(Path.Combine(
            GetDesktopProjectPath(),
            "Views",
            "Pages",
            "ConfigurationsPageView.axaml"));

        Assert.Contains("frpc.toml 只读预览", configurationsXaml);
        Assert.Contains("已启用隧道来源", configurationsXaml);
        Assert.Contains("RefreshClientTunnelsCommand", configurationsXaml);
        Assert.Contains("ValidateTomlCommand", configurationsXaml);
        Assert.Contains("CopyTomlCommand", configurationsXaml);
        Assert.DoesNotContain("GenerateTomlCommand", configurationsXaml);
        Assert.DoesNotContain("UploadServerTomlCommand", configurationsXaml);
        Assert.DoesNotContain("GenerateServerTomlCommand", configurationsXaml);
        Assert.DoesNotContain("上传 frps.toml", configurationsXaml);
        Assert.DoesNotContain("生成 frps.toml", configurationsXaml);
        Assert.DoesNotContain("服务端端口", configurationsXaml);
        Assert.DoesNotContain("远程 frps 配置", configurationsXaml);
    }

    [Fact]
    public void LogsPage_ShouldExposeRealLogCommandsAndNoStaticSampleNodes()
    {
        var logsXaml = File.ReadAllText(Path.Combine(
            GetDesktopProjectPath(),
            "Views",
            "Pages",
            "LogsPageView.axaml"));

        Assert.Contains("RefreshLogsCommand", logsXaml);
        Assert.Contains("ToggleRemoteCredentialsCommand", logsXaml);
        Assert.DoesNotContain("ReadRemoteLogsCommand", logsXaml);
        Assert.Contains("RemoteLogPath", logsXaml);
        Assert.DoesNotContain("CanReadRemoteLogs", logsXaml);
        Assert.Contains("LogFileText", logsXaml);
        Assert.DoesNotContain("SshSessionPassword", logsXaml);
        Assert.DoesNotContain("会话密码", logsXaml);
        Assert.DoesNotContain("Web-Server-HK", logsXaml);
        Assert.DoesNotContain("DB-Node-SH", logsXaml);
    }

    [Fact]
    public void NodesPage_ShouldKeepRuntimeCopyFocusedOnRemoteFrps()
    {
        var nodesXaml = File.ReadAllText(Path.Combine(
            GetDesktopProjectPath(),
            "Views",
            "Pages",
            "NodesPageView.axaml"));

        Assert.Contains("Text=\"远程 frps\"", nodesXaml);
        Assert.Contains("Text=\"远程 frps 运行\"", nodesXaml);
        Assert.Contains("Label=\"frps 状态\"", nodesXaml);
        Assert.DoesNotContain("启动 frpc", nodesXaml);
        Assert.DoesNotContain("停止 frpc", nodesXaml);
        Assert.DoesNotContain("重载配置", nodesXaml);
        Assert.DoesNotContain("选择核心", nodesXaml);
        Assert.DoesNotContain("选择配置", nodesXaml);
        Assert.DoesNotContain("LocalFrpcBinaryPath", nodesXaml);
        Assert.DoesNotContain("LocalFrpcConfigPath", nodesXaml);
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
