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

    // Цветные статусы для всех 10 статусов
    public string StatusColor => Status switch
    {
        "Запланирована" => "#3498DB",           // Синий
        "В процессе выполнения" => "#F39C12",   // Оранжевый
        "Требует дополнительной диагностики" => "#9B59B6", // Фиолетовый
        "Ожидает подтверждения" => "#1ABC9C",   // Бирюзовый
        "На анализе данных" => "#3498DB",       // Синий
        "Завершена" => "#27AE60",               // Зеленый
        "Отложена" => "#E74C3C",                // Красный
        "Отменена" => "#95A5A6",                // Серый
        "Требует срочного вмешательства" => "#E67E22", // Темно-оранжевый
        "На согласовании" => "#F1C40F",         // Желтый
        _ => "#7F8C8D"                          // Темно-серый по умолчанию
    };

    // Результат соответствует статусу
    public string Result
    {
        get
        {
            return Status switch
            {
                "Завершена" => "Успешно завершена",
                "Отменена" => "Отменена",
                "Отложена" => "Отложена",
                "Запланирована" => "Ожидает выполнения",
                "В процессе выполнения" => "Выполняется",
                "Требует дополнительной диагностики" => "Требуется доп. диагностика",
                "Ожидает подтверждения" => "Ожидает подтверждения",
                "На анализе данных" => "Анализ данных",
                "Требует срочного вмешательства" => "Срочное вмешательство",
                "На согласовании" => "На согласовании",
                _ => "Не определено"
            };
        }
    }
}