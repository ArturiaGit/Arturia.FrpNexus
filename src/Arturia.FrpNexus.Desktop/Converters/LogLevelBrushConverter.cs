using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Arturia.FrpNexus.Desktop.Converters;

public sealed class LogLevelBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "OK" => SolidColorBrush.Parse("#16A34A"),
            "WARN" => SolidColorBrush.Parse("#D97706"),
            "ERR" or "ERROR" => SolidColorBrush.Parse("#DC2626"),
            _ => SolidColorBrush.Parse("#2563EB")
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
