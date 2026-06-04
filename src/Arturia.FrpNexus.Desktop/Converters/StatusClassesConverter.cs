using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Desktop.Converters;

public sealed class StatusClassesConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var statusClass = value switch
        {
            FrpNexusStatus.Online or FrpNexusStatus.Running => "success",
            FrpNexusStatus.Warning => "warning",
            FrpNexusStatus.Error => "error",
            FrpNexusStatus.Offline or FrpNexusStatus.Stopped => "neutral",
            FrpNexusStatus.Pending => "neutral",
            _ => "info"
        };

        return parameter is string expected
            ? string.Equals(statusClass, expected, StringComparison.Ordinal)
            : statusClass;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
