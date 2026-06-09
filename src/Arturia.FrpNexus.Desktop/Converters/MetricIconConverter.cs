using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Arturia.FrpNexus.Desktop.Converters;

public sealed class MetricIconConverter : IValueConverter
{
    private static readonly Dictionary<string, StreamGeometry> Icons = new()
    {
        ["dns"] = StreamGeometry.Parse("M5 4h14v5H5V4Zm2 2v1h7V6H7Zm9 0v1h1V6h-1ZM5 10.5h14v5H5v-5Zm2 2v1h7v-1H7Zm9 0v1h1v-1h-1ZM5 17h14v3H5v-3Z"),
        ["check_circle"] = StreamGeometry.Parse("M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18Zm-1.1 12.4-3.3-3.3 1.2-1.2 2.1 2.1 4.5-4.5 1.2 1.2-5.7 5.7Z"),
        ["memory"] = StreamGeometry.Parse("M7 5h10a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2Zm1.5 3v8h7V8h-7ZM3 8h2v1.5H3V8Zm0 3.2h2v1.5H3v-1.5Zm0 3.3h2V16H3v-1.5ZM19 8h2v1.5h-2V8Zm0 3.2h2v1.5h-2v-1.5Zm0 3.3h2V16h-2v-1.5ZM8 3h1.5v2H8V3Zm3.2 0h1.5v2h-1.5V3Zm3.3 0H16v2h-1.5V3ZM8 19h1.5v2H8v-2Zm3.2 0h1.5v2h-1.5v-2Zm3.3 0H16v2h-1.5v-2Z"),
        ["rebase_edit"] = StreamGeometry.Parse("M7 4a3 3 0 0 1 2.8 2h4.4A3 3 0 1 1 17 10a3 3 0 0 1-2.8-2H9.8A3 3 0 0 1 8 9.8v4.4A3 3 0 1 1 6 14.2V9.8A3 3 0 0 1 7 4Zm0 2a1 1 0 1 0 0 2 1 1 0 0 0 0-2Zm10 0a1 1 0 1 0 0 2 1 1 0 0 0 0-2ZM7 16a1 1 0 1 0 0 2 1 1 0 0 0 0-2Zm8.8-2.2 1.4-1.4 3.4 3.4-1.4 1.4-3.4-3.4Zm-.7.7 3.4 3.4L15 19l1.1-3.5Z"),
        ["add"] = StreamGeometry.Parse("M11 5h2v6h6v2h-6v6h-2v-6H5v-2h6V5Z"),
        ["route"] = StreamGeometry.Parse("M6.5 5A2.5 2.5 0 0 1 9 7.5c0 1-.6 1.9-1.5 2.3V12h8.2c1.9 0 3.3 1.5 3.3 3.3S17.5 18.5 15.7 18.5H9v-2h6.7c.7 0 1.3-.6 1.3-1.3s-.6-1.3-1.3-1.3H7.5V16H9l-2.5 3L4 16h1.5V9.8A2.5 2.5 0 0 1 6.5 5Zm0 2a.5.5 0 1 0 0 1 .5.5 0 0 0 0-1Z"),
        ["autorenew"] = StreamGeometry.Parse("M12 5a7 7 0 0 1 6.2 3.8L20 7v5h-5l1.8-1.8A5.4 5.4 0 0 0 7.2 9H5.1A7.4 7.4 0 0 1 12 5Zm-6.2 8.2A5.4 5.4 0 0 0 16.8 15h2.1A7.4 7.4 0 0 1 5.8 15.2L4 17v-5h5l-3.2 1.2Z"),
        ["error"] = StreamGeometry.Parse("M12 2 2 20h20L12 2Zm0 5 1 7h-2l1-7Zm-1 9h2v2h-2v-2Z"),
        ["notifications"] = StreamGeometry.Parse("M12 22a2.5 2.5 0 0 0 2.45-2h-4.9A2.5 2.5 0 0 0 12 22ZM5 17h14l-1.7-2.2V10a5.3 5.3 0 0 0-10.6 0v4.8L5 17Zm2.2-1.5 1-1.3V10a3.8 3.8 0 0 1 7.6 0v4.2l1 1.3H7.2Z"),
        ["info"] = StreamGeometry.Parse("M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18Zm-1 7h2v7h-2v-7Zm0-3h2v2h-2V7Z"),
        ["folder"] = StreamGeometry.Parse("M4 6h7l2 2h7v12H4V6Zm2 2v10h12v-8h-5.8l-2-2H6Z"),
        ["folder_open"] = StreamGeometry.Parse("M3 8h7l2 2h9l-2.4 10H4.8L3 8Zm2.5 2 1.4 8h10.2l1.4-6H11.2l-2-2H5.5ZM5 6h7l2 2h6v2h-7.2l-2-2H5V6Z"),
        ["create_new_folder"] = StreamGeometry.Parse("M4 6h7l2 2h7v12H4V6Zm2 2v10h12v-8h-5.8l-2-2H6Zm8 4h2v2h2v2h-2v2h-2v-2h-2v-2h2v-2Z"),
        ["arrow_upward"] = StreamGeometry.Parse("M11 20V8.8L6.4 13.4 5 12l7-7 7 7-1.4 1.4L13 8.8V20h-2Z"),
        ["check"] = StreamGeometry.Parse("M9.5 16.8 4.8 12l1.4-1.4 3.3 3.3 8.3-8.3L19.2 7 9.5 16.8Z"),
        ["lan"] = StreamGeometry.Parse("M5 5h6v5H8v3h8v-3h-3V5h6v5h-2v4.5H8V19H5v-6h2v-3H5V5Zm2 2v1h2V7H7Zm8 0v1h2V7h-2ZM7 15v2h2v-2H7Zm8 0v2h2v-2h-2Z"),
        ["cloud_done"] = StreamGeometry.Parse("M8.5 19A5.5 5.5 0 0 1 8 8.1 6.5 6.5 0 0 1 20 11a4 4 0 0 1-.5 8H8.5Zm0-2h10.6A2 2 0 0 0 19 13h-1v-1a4.5 4.5 0 0 0-8.5-2.1l-.3.7-.8-.1A3.5 3.5 0 0 0 8.5 17Zm2.4-1.4-2.6-2.6 1.2-1.2 1.4 1.4 3.7-3.7 1.2 1.2-4.9 4.9Z"),
        ["refresh"] = StreamGeometry.Parse("M17.7 7.1A7 7 0 0 0 5.2 9H3.6a8.5 8.5 0 0 1 15.1-3l1.8-1.8V9h-4.8l2-1.9ZM6.3 16.9A7 7 0 0 0 18.8 15h1.6a8.5 8.5 0 0 1-15.1 3l-1.8 1.8V15h4.8l-2 1.9Z"),
        ["search"] = StreamGeometry.Parse("M10.5 4a6.5 6.5 0 0 1 5.1 10.5l4 4-1.4 1.4-4-4A6.5 6.5 0 1 1 10.5 4Zm0 2a4.5 4.5 0 1 0 0 9 4.5 4.5 0 0 0 0-9Z"),
        ["terminal"] = StreamGeometry.Parse("M4 5h16v14H4V5Zm2 2v10h12V7H6Zm1.2 3 1-1 3 3-3 3-1-1 2-2-2-2Zm5.3 5h4v1.5h-4V15Z"),
        ["code"] = StreamGeometry.Parse("M9.4 16.6 4.8 12l4.6-4.6L8 6l-6 6 6 6 1.4-1.4Zm5.2 0L19.2 12l-4.6-4.6L16 6l6 6-6 6-1.4-1.4ZM11 19l-1.9-.6L13 5l1.9.6L11 19Z"),
        ["upload"] = StreamGeometry.Parse("M11 16V7.8L8.4 10.4 7 9l5-5 5 5-1.4 1.4L13 7.8V16h-2ZM5 18h14v2H5v-2Z"),
        ["save"] = StreamGeometry.Parse("M5 4h12l2 2v14H5V4Zm2 2v12h10V7.2L15.8 6H15v5H8V6H7Zm3 0v3h3V6h-3Zm-1 8h6v2H9v-2Z"),
        ["content_copy"] = StreamGeometry.Parse("M8 7h11v14H8V7Zm2 2v10h7V9h-7ZM5 3h11v2H7v11H5V3Z"),
        ["restart_alt"] = StreamGeometry.Parse("M12 5a7 7 0 1 1-6.6 9.3h2.2A5 5 0 1 0 12 7H9.8l2.6 2.6L11 11 6 6l5-5 1.4 1.4L9.8 5H12Z"),
        ["chevron_left"] = StreamGeometry.Parse("M14.8 6 9 12l5.8 6 1.4-1.4L11.8 12l4.4-4.6L14.8 6Z"),
        ["chevron_right"] = StreamGeometry.Parse("M9.2 18 15 12 9.2 6 7.8 7.4l4.4 4.6-4.4 4.6L9.2 18Z"),
        ["chevron_down"] = StreamGeometry.Parse("M6 9.2 12 15l6-5.8-1.4-1.4-4.6 4.4-4.6-4.4L6 9.2Z"),
        ["edit"] = StreamGeometry.Parse("M5 17.2V20h2.8L17.9 9.9l-2.8-2.8L5 17.2ZM19.1 8.7 20.5 7.3a1 1 0 0 0 0-1.4l-2.4-2.4a1 1 0 0 0-1.4 0l-1.4 1.4 3.8 3.8Z"),
        ["delete"] = StreamGeometry.Parse("M7 21a2 2 0 0 1-2-2V7h14v12a2 2 0 0 1-2 2H7ZM9 4h6l1 1h4v2H4V5h4l1-1Zm0 6v8h2v-8H9Zm4 0v8h2v-8h-2Z"),
        ["stop_circle"] = StreamGeometry.Parse("M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18Zm-3.5 5.5h7v7h-7v-7Z"),
        ["play_circle"] = StreamGeometry.Parse("M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18Zm-2 5.2 6 3.8-6 3.8V8.2Z"),
        ["warning"] = StreamGeometry.Parse("M12 3 2.5 20h19L12 3Zm0 5 1 6h-2l1-6Zm-1 8h2v2h-2v-2Z"),
        ["logout"] = StreamGeometry.Parse("M5 4h8v2H7v12h6v2H5V4Zm10.6 4.4L20.2 13l-4.6 4.6-1.4-1.4 2.2-2.2H10v-2h6.4l-2.2-2.2 1.4-1.4Z")
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string key && Icons.TryGetValue(key, out var icon)
            ? icon
            : Icons["dns"];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
