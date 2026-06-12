using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Taneco.Converters;

public class DateMaskConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime date)
            return date.ToString("dd.MM.yyyy");
        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string dateString && !string.IsNullOrWhiteSpace(dateString))
        {
            if (DateTime.TryParseExact(dateString, "dd.MM.yyyy", null, DateTimeStyles.None, out DateTime date))
                return date;
        }
        return null;
    }
}