using System;
using System.Globalization;
using System.Windows.Data;

namespace ListenerGui.Main;

public class DateTimeOffsetValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (!(value is DateTimeOffset dto))
        {
            return value;
        }

        return dto.ToString("HH:mm:ss.ffff");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}