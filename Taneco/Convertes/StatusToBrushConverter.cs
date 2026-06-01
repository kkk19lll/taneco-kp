using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Taneco.Converters;

public class StatusToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status switch
            {
                "Норма" => new SolidColorBrush(Color.Parse("#27AE60")),
                "Предупреждение" => new SolidColorBrush(Color.Parse("#F39C12")),
                "Критично" => new SolidColorBrush(Color.Parse("#E74C3C")),
                _ => new SolidColorBrush(Color.Parse("#95A5A6"))
            };
        }
        return new SolidColorBrush(Color.Parse("#95A5A6"));
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}