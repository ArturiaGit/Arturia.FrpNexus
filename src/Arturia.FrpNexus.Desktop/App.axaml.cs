using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Arturia.FrpNexus.Desktop.Composition;
using Arturia.FrpNexus.Desktop.Theming;
using Arturia.FrpNexus.Desktop.ViewModels;
using Arturia.FrpNexus.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;

namespace Arturia.FrpNexus.Desktop;

public partial class App : Avalonia.Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _serviceProvider = DesktopCompositionRoot.BuildServiceProvider();
        _ = InitializeThemeAsync();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = mainWindow;
            desktop.Exit += OnDesktopExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async System.Threading.Tasks.Task InitializeThemeAsync()
    {
        if (_serviceProvider is null)
        {
            return;
        }

        try
        {
            await _serviceProvider.GetRequiredService<IThemeService>().InitializeAsync();
        }
        catch (Exception ex)
        {
            _serviceProvider.GetService<ILogger>()?.Warning(ex, "Failed to initialize application theme.");
        }
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _serviceProvider?.GetService<ILogger>()?.Information("FrpNexus desktop application exited.");
        _serviceProvider?.Dispose();
        Log.CloseAndFlush();
    }
}
