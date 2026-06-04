using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Desktop.Converters;

public sealed class TunnelProtocolTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            TunnelProtocol.Tcp => "TCP",
            TunnelProtocol.Udp => "UDP",
            TunnelProtocol.Http => "HTTP",
            TunnelProtocol.Https => "HTTPS",
            _ => string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
