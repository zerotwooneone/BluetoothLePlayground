using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace ListenerGui.Converters;

public class MacAddressConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
        {
            return value;
        }
        var num = value as ulong?;
        if (num == null)
        {
            return value;
        }

        return string.Join(":",
            BitConverter.GetBytes(num.Value).Reverse()
                .Select(b => b.ToString("X2"))).Substring(6);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}