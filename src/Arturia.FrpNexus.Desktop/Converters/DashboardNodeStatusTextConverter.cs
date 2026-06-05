using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Desktop.Converters;

public sealed class DashboardNodeStatusTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            FrpNexusStatus.Running or FrpNexusStatus.Online => "运行中",
            FrpNexusStatus.Offline or FrpNexusStatus.Stopped => "已离线",
            FrpNexusStatus.Warning => "负载较高",
            FrpNexusStatus.Error => "异常",
            _ => "未知"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
