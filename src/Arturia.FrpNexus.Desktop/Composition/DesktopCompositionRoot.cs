using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Desktop.Logging;
using Arturia.FrpNexus.Desktop.Placeholders;
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

        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();

        services.AddSingleton<INodeManagementService, PhaseOneNodeManagementService>();
        services.AddSingleton<ISshConnectionService, PhaseOneSshConnectionService>();
        services.AddSingleton<IRemoteFileTransferService, PhaseOneRemoteFileTransferService>();
        services.AddSingleton<IFrpReleaseService, PhaseOneFrpReleaseService>();
        services.AddSingleton<ITomlConfigurationService, PhaseOneTomlConfigurationService>();
        services.AddSingleton<IRemoteRuntimeService, PhaseOneRemoteRuntimeService>();
        services.AddSingleton<IRemoteLogService, PhaseOneRemoteLogService>();

        services.AddTransient<DashboardPageViewModel>();
        services.AddTransient<NodesPageViewModel>();
        services.AddTransient<TunnelsPageViewModel>();
        services.AddTransient<ConfigurationsPageViewModel>();
        services.AddTransient<RuntimePageViewModel>();
        services.AddTransient<LogsPageViewModel>();
        services.AddTransient<SettingsPageViewModel>();

        return services.BuildServiceProvider(validateScopes: true);
    }
}
