using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Taneco.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status switch
            {
                "Запланирована" => new SolidColorBrush(Color.Parse("#9B59B6")),           // Фиолетовый
                "В процессе выполнения" => new SolidColorBrush(Color.Parse("#3498DB")),    // Синий
                "Требует дополнительной диагностики" => new SolidColorBrush(Color.Parse("#F39C12")), // Оранжевый
                "Ожидает подтверждения" => new SolidColorBrush(Color.Parse("#E67E22")),   // Темно-оранжевый
                "На анализе данных" => new SolidColorBrush(Color.Parse("#1ABC9C")),       // Бирюзовый
                "Завершена" => new SolidColorBrush(Color.Parse("#27AE60")),               // Зеленый
                "Отложена" => new SolidColorBrush(Color.Parse("#95A5A6")),                // Серый
                "Отменена" => new SolidColorBrush(Color.Parse("#7F8C8D")),                // Темно-серый
                "Требует срочного вмешательства" => new SolidColorBrush(Color.Parse("#E74C3C")), // Красный
                "На согласовании" => new SolidColorBrush(Color.Parse("#F1C40F")),         // Желтый
                _ => new SolidColorBrush(Color.Parse("#7F8C8D"))
            };
        }

        return new SolidColorBrush(Color.Parse("#7F8C8D"));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}