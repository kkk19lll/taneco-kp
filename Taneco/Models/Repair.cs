using System;

namespace Taneco.Models;

public class Repair
{
    public int Id { get; set; }
    public int ProblemId { get; set; }
    public string ProblemDescription { get; set; } = string.Empty;
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal Budget { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CompletionReport { get; set; }
    public string LastRepairDate { get; set; } = "Нет ремонтов";
    public string LastRepairStatus { get; set; } = "Нет ремонтов";

    public string StatusColor => Status switch
    {
        "Завершен" => "#27AE60",
        "В процессе ремонта" => "#F39C12",
        "Аварийная остановка" => "#E74C3C",
        "Подготовка к ремонту" => "#3498DB",
        "Испытания после ремонта" => "#9B59B6",
        "Готов к запуску" => "#1ABC9C",
        "Ожидает поставки материалов" => "#E67E22",
        "Консервация оборудования" => "#95A5A6",
        _ => "#7F8C8D"
    };
}