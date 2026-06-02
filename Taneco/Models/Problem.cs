using System;

namespace Taneco.Models;

public class Problem
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime NotificationDate { get; set; }
    public TimeSpan NotificationTime { get; set; }
    public int PipelineId { get; set; }
    public string PipelineName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal MeasuredValue { get; set; }
    public decimal ThresholdValue { get; set; }
    public string RiskCategory { get; set; } = string.Empty;

    public string StatusColor => Status switch
    {
        "Запланирована" => "#9B59B6",           // Фиолетовый
        "В процессе выполнения" => "#3498DB",    // Синий
        "Требует дополнительной диагностики" => "#F39C12", // Оранжевый
        "Ожидает подтверждения" => "#E67E22",   // Темно-оранжевый
        "На анализе данных" => "#1ABC9C",       // Бирюзовый
        "Завершена" => "#27AE60",               // Зеленый
        "Отложена" => "#95A5A6",                // Серый
        "Отменена" => "#7F8C8D",                // Темно-серый
        "Требует срочного вмешательства" => "#E74C3C", // Красный
        "На согласовании" => "#F1C40F",         // Желтый
        _ => "#7F8C8D"
    };

    public string RiskColor => RiskCategory switch
    {
        "Критический" => "#E74C3C",
        "Высокий" => "#F39C12",
        "Средний" => "#3498DB",
        "Низкий" => "#27AE60",
        _ => "#95A5A6"
    };
}