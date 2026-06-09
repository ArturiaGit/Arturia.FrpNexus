using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Application.Configuration;
using Arturia.FrpNexus.Desktop.Logging;
using Arturia.FrpNexus.Desktop.Placeholders;
using Arturia.FrpNexus.Desktop.Services;
using Arturia.FrpNexus.Desktop.Theming;
using Arturia.FrpNexus.Desktop.ViewModels;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;
using Arturia.FrpNexus.Desktop.Views;
using Arturia.FrpNexus.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Arturia.FrpNexus.Desktop.Composition;

public static class DesktopCompositionRoot
{
    public static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ILogger>(_ => DesktopLogging.CreateLogger());
        services.AddFrpNexusInfrastructure();
        services.AddSingleton<IThemeService, AvaloniaThemeService>();
        services.AddSingleton<IFilePickerService, AvaloniaFilePickerService>();
        services.AddSingleton<IClipboardService, AvaloniaClipboardService>();
        services.AddSingleton<IRemoteDirectoryPickerService, AvaloniaRemoteDirectoryPickerService>();
        services.AddSingleton<INodeConnectionWorkflowDialogService, AvaloniaNodeConnectionWorkflowDialogService>();
        services.AddSingleton<INavigationRequestService, NavigationRequestService>();
        services.AddSingleton<IModalOverlayService, ModalOverlayService>();
        services.AddSingleton<IModalDialogHostService, ModalDialogHostService>();

        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();

        services.AddSingleton<ITomlConfigurationService, TomlConfigurationService>();
        services.AddTransient<DashboardPageViewModel>();
        services.AddTransient(sp => new NodesPageViewModel(
            sp.GetRequiredService<INodeManagementService>(),
            sp.GetRequiredService<INodeConnectionSessionService>(),
            sp.GetRequiredService<IRemoteRuntimeService>(),
            sp.GetRequiredService<IRemoteFileTransferService>(),
            sp.GetRequiredService<ITomlConfigurationService>(),
            sp.GetRequiredService<IFilePickerService>(),
            sp.GetRequiredService<IRemoteDirectoryPickerService>(),
            sp.GetRequiredService<INodeCredentialSecretService>(),
            sp.GetRequiredService<IDeploymentRecordService>(),
            sp.GetRequiredService<INodeConnectionWorkflowDialogService>()));
        services.AddTransient<TunnelsPageViewModel>();
        services.AddTransient<ConfigurationsPageViewModel>();
        services.AddTransient<RuntimePageViewModel>();
        services.AddTransient<LogsPageViewModel>();
        services.AddTransient<SettingsPageViewModel>();

        return services.BuildServiceProvider(validateScopes: true);
    }
}
