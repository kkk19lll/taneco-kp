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
        "Новая" => "#E74C3C",
        "На проверке" => "#F39C12",
        "Подтверждена" => "#3498DB",
        "Завершена" => "#27AE60",
        "Ложная тревога" => "#95A5A6",
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