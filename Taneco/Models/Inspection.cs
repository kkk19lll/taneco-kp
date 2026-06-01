using System;

namespace Taneco.Models;

public class Inspection
{
    public int Id { get; set; }
    public int ProblemId { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProblemDescription { get; set; } = string.Empty;
    public string ProblemType { get; set; } = string.Empty;

    public string StatusColor => Status switch
    {
        "Запланирована" => "#3498DB",
        "В процессе выполнения" => "#F39C12",
        "Завершена" => "#27AE60",
        "Отложена" => "#E74C3C",
        "Отменена" => "#95A5A6",
        _ => "#7F8C8D"
    };
}