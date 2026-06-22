using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Arturia.FrpNexus.Desktop.Converters;

public sealed class BoolToDoubleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var values = ParseParameter(parameter);
        return value is true ? values.TrueValue : values.FalseValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var values = ParseParameter(parameter);
        return value is double doubleValue && Math.Abs(doubleValue - values.TrueValue) < Math.Abs(doubleValue - values.FalseValue);
    }

    private static (double TrueValue, double FalseValue) ParseParameter(object? parameter)
    {
        if (parameter is string text)
        {
            var parts = text.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length == 2
                && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var trueValue)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var falseValue))
            {
                return (trueValue, falseValue);
            }
        }

        return (1, 0);
    }
}
