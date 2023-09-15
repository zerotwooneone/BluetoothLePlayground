using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace ListenerGui.Converters;

public class MacAddressConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ulong num)
        {
            return value;
        }

        return string.Join(":",
            BitConverter.GetBytes(num).Reverse()
                .Select(b => b.ToString("X2"))).Substring(6);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if(!(value is string str))
        {
            return value;
        }
        string hex = str.Replace(":", "");
        return System.Convert.ToUInt64(hex, 16);
    }
}