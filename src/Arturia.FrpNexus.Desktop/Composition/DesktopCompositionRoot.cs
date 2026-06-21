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
        services.AddSingleton<ILocalApplicationLogService, LocalApplicationLogService>();
        services.AddSingleton<INavigationRequestService, NavigationRequestService>();
        services.AddSingleton<IModalOverlayService, ModalOverlayService>();
        services.AddSingleton<IModalDialogHostService, ModalDialogHostService>();
        services.AddSingleton<IConfirmationDialogService, ConfirmationDialogService>();
        services.AddSingleton<IFrpCoreDownloadOptionsDialogService, FrpCoreDownloadOptionsDialogService>();
        services.AddSingleton<IFrpLifecycleStateService, FrpLifecycleStateService>();
        services.AddSingleton<IRemoteFrpsRetentionService, RemoteFrpsRetentionService>();

        services.AddSingleton<MainWindow>();
        services.AddSingleton(sp => new MainWindowViewModel(
            sp.GetRequiredService<DashboardPageViewModel>(),
            sp.GetRequiredService<NodesPageViewModel>(),
            sp.GetRequiredService<TunnelsPageViewModel>(),
            sp.GetRequiredService<ConfigurationsPageViewModel>(),
            sp.GetRequiredService<RuntimePageViewModel>(),
            sp.GetRequiredService<LogsPageViewModel>(),
            sp.GetRequiredService<SettingsPageViewModel>(),
            sp.GetRequiredService<INavigationRequestService>(),
            sp.GetRequiredService<INodeConnectionSessionService>(),
            sp.GetRequiredService<INodeManagementService>(),
            sp.GetRequiredService<IRemoteRuntimeService>(),
            sp.GetRequiredService<ILocalFrpcProcessService>(),
            sp.GetRequiredService<IFrpLifecycleStateService>(),
            sp.GetRequiredService<IRemoteFrpsRetentionService>(),
            sp.GetRequiredService<IConfirmationDialogService>(),
            sp.GetRequiredService<IModalOverlayService>(),
            sp.GetRequiredService<IModalDialogHostService>()));

        services.AddSingleton<ITomlConfigurationService, TomlConfigurationService>();
        services.AddTransient(sp => new DashboardPageViewModel(
            sp.GetRequiredService<INodeManagementService>(),
            sp.GetRequiredService<ITunnelManagementService>(),
            sp.GetRequiredService<IRuntimeRecordService>(),
            sp.GetRequiredService<IDeploymentRecordService>(),
            sp.GetRequiredService<INodeConnectionSessionService>(),
            sp.GetRequiredService<ILocalFrpcProcessService>(),
            sp.GetRequiredService<IFrpLifecycleStateService>(),
            sp.GetRequiredService<INavigationRequestService>(),
            sp.GetRequiredService<ILocalApplicationLogService>(),
            sp.GetRequiredService<IRemoteLogService>(),
            sp.GetRequiredService<IRemoteRuntimeService>()));
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
            sp.GetRequiredService<INodeConnectionWorkflowDialogService>(),
            sp.GetRequiredService<IConfirmationDialogService>(),
            sp.GetRequiredService<IFrpLifecycleStateService>(),
            sp.GetRequiredService<IRemoteFrpsRetentionService>()));
        services.AddTransient(sp => new TunnelsPageViewModel(
            sp.GetRequiredService<ITunnelManagementService>(),
            sp.GetRequiredService<INodeManagementService>(),
            sp.GetRequiredService<ILocalFrpcProcessService>(),
            sp.GetRequiredService<ILocalFrpcConfigurationService>(),
            sp.GetRequiredService<IRuntimeRecordService>(),
            sp.GetRequiredService<IFilePickerService>()));
        services.AddTransient<ConfigurationsPageViewModel>();
        services.AddTransient<RuntimePageViewModel>();
        services.AddTransient(sp => new LogsPageViewModel(
            sp.GetRequiredService<INodeManagementService>(),
            sp.GetRequiredService<IRemoteLogService>(),
            sp.GetRequiredService<ILocalApplicationLogService>(),
            sp.GetRequiredService<INodeConnectionSessionService>(),
            sp.GetRequiredService<IRemoteRuntimeService>()));
        services.AddTransient<SettingsPageViewModel>();

        return services.BuildServiceProvider(validateScopes: true);
    }
}
