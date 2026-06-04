using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Desktop.Converters;

public sealed class StatusTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            FrpNexusStatus.Online => "在线",
            FrpNexusStatus.Offline => "离线",
            FrpNexusStatus.Running => "运行中",
            FrpNexusStatus.Stopped => "已停止",
            FrpNexusStatus.Warning => "警告",
            FrpNexusStatus.Error => "异常",
            FrpNexusStatus.Ready => "已就绪",
            FrpNexusStatus.Pending => "待执行",
            _ => "未知"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
