using System;

namespace Taneco.Models;

public class ProblemHistoryEvent
{
    public int Id { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public TimeSpan EventTime { get; set; }
    public string Details { get; set; } = string.Empty;
    public string EventType { get; set; } = "default";
    
    public string FormattedDateTime => EventTime == TimeSpan.Zero 
        ? EventDate.ToString("dd.MM.yyyy")
        : EventDate.ToString("dd.MM.yyyy HH:mm");
    
    public string EventColor => EventType switch
    {
        "detection" => "#E74C3C",   // Красный - обнаружение проблемы
        "inspection" => "#F39C12",  // Оранжевый - проверка
        "repair" => "#3498DB",      // Синий - ремонт
        "completion" => "#27AE60",  // Зеленый - завершение
        _ => "#7F8C8D"              // Серый - остальное
    };
}