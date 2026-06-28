using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Arturia.FrpNexus.Desktop.ViewModels;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;
using Arturia.FrpNexus.Desktop.Views.Pages;

namespace Arturia.FrpNexus.Desktop;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        Control? view = param switch
        {
            DashboardPageViewModel => new DashboardPageView(),
            NodesPageViewModel => new NodesPageView(),
            TunnelsPageViewModel => new TunnelsPageView(),
            ConfigurationsPageViewModel => new ConfigurationsPageView(),
            RuntimePageViewModel => new RuntimePageView(),
            LogsPageViewModel => new LogsPageView(),
            SettingsPageViewModel => new SettingsPageView(),
            _ => null
        };

        return view ?? new TextBlock { Text = "Not Found: " + param.GetType().FullName };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
