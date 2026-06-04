using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Arturia.FrpNexus.Desktop.Converters;

public sealed class NavigationIconConverter : IValueConverter
{
    private static readonly Dictionary<string, StreamGeometry> Icons = new()
    {
        ["dashboard"] = StreamGeometry.Parse("M4 4h7v7H4V4Zm9 0h7v7h-7V4ZM4 13h7v7H4v-7Zm9 0h7v7h-7v-7Z"),
        ["nodes"] = StreamGeometry.Parse("M6 4h12v4H6V4Zm0 6h12v4H6v-4Zm0 6h12v4H6v-4ZM4 5h1v2H4V5Zm0 6h1v2H4v-2Zm0 6h1v2H4v-2Z"),
        ["tunnels"] = StreamGeometry.Parse("M5 7h7v2H5a3 3 0 0 0 0 6h2v-2H5a1 1 0 0 1 0-2h7v2l4-3-4-3v2H5Zm14 10h-7v-2h7a3 3 0 0 0 0-6h-2v2h2a1 1 0 0 1 0 2h-7v-2l-4 3 4 3v-2h7Z"),
        ["configurations"] = StreamGeometry.Parse("M5 5h14v3H5V5Zm0 5h14v9H5v-9Zm2 2v2h2v-2H7Zm4 0v2h6v-2h-6Zm-4 4v1h10v-1H7Z"),
        ["runtime"] = StreamGeometry.Parse("M8 5v14l11-7L8 5Z"),
        ["logs"] = StreamGeometry.Parse("M5 4h14v16H5V4Zm2 3v2h10V7H7Zm0 4v2h10v-2H7Zm0 4v2h7v-2H7Z"),
        ["settings"] = StreamGeometry.Parse("M12 8a4 4 0 1 1 0 8 4 4 0 0 1 0-8Zm0 2a2 2 0 1 0 0 4 2 2 0 0 0 0-4Zm8 2 2 1-2 4-2-.5a7 7 0 0 1-1.5 1l-.5 2.5h-4l-.5-2.5a7 7 0 0 1-1.5-1L7 17l-2-4 2-1v-2L5 9l2-4 2 .5a7 7 0 0 1 1.5-1L11 2h4l.5 2.5a7 7 0 0 1 1.5 1L19 5l2 4-2 1v2Z")
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string key && Icons.TryGetValue(key, out var icon)
            ? icon
            : Icons["dashboard"];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
