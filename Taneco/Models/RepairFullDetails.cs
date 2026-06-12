using System;

namespace Taneco.Models;

public class RepairFullDetails
{
    public int Id { get; set; }
    public int ProblemId { get; set; }
    public string ProblemDescription { get; set; } = string.Empty;
    public string ProblemType { get; set; } = string.Empty;
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal Budget { get; set; }
    public string Status { get; set; } = string.Empty;
    public string InspectionDescription { get; set; } = string.Empty;
    public string AssignedBy { get; set; } = string.Empty;
    public string InspectedBy { get; set; } = string.Empty;
    public string EquipmentName { get; set; } = string.Empty;
    public decimal MeasuredValue { get; set; }
    public decimal ThresholdValue { get; set; }

    public string StatusColor => Status switch
    {
        "Завершён" => "#27AE60",
        "В процессе" => "#F39C12",
        "Запланирован" => "#3498DB",
        "Аварийная остановка" => "#E74C3C",
        "Подготовка к ремонту" => "#3498DB",
        "В процессе ремонта" => "#F39C12",
        "Испытания после ремонта" => "#9B59B6",
        "Готов к запуску" => "#1ABC9C",
        "Ожидает поставки материалов" => "#E67E22",
        "Требуется проектная документация" => "#E67E22",
        "На согласовании метода ремонта" => "#E67E22",
        "Консервация оборудования" => "#95A5A6",
        _ => "#7F8C8D"
    };
}