using Arturia.FrpNexus.Application.Abstractions;
using Avalonia.Styling;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Arturia.FrpNexus.Desktop.Theming;

public sealed class AvaloniaThemeService(ISettingsService settingsService) : IThemeService
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
        ApplyTheme(settings.Theme);
    }

    public void ApplyTheme(string theme)
    {
        if (global::Avalonia.Application.Current is null)
        {
            return;
        }

        global::Avalonia.Application.Current.RequestedThemeVariant = NormalizeTheme(theme) switch
        {
            "Dark" => ThemeVariant.Dark,
            "System" => ThemeVariant.Default,
            _ => ThemeVariant.Light
        };
    }

    private static string NormalizeTheme(string theme)
    {
        if (string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            return "Dark";
        }

        if (string.Equals(theme, "System", StringComparison.OrdinalIgnoreCase))
        {
            return "System";
        }

        return "Light";
    }
}
