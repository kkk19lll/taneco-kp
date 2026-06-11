using System;
using System.Collections.ObjectModel;

namespace Taneco.Models;

// Модель для инспектора (выпадающий список)
public class InspectorItem
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
}

// Модель для начальника ремонтной службы (выпадающий список)
public class RepairManagerItem
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
}

// Статусы проблем (цвета и тексты)
public static class ProblemStatuses
{
    public const string New = "Новая";
    public const string InspectionScheduled = "Запланирована проверка";
    public const string WaitingConfirmation = "Ожидает подтверждения";
    public const string UnderInspection = "В процессе выполнения";
    public const string FalseReading = "Ложные показания";
    public const string RepairRequired = "Требуется ремонт";
    public const string UnderRepair = "В процессе ремонта";
    public const string Completed = "Завершен";

    public static string GetColorForStatus(string status)
    {
        return status switch
        {
            New => "#FF6B6B",           // красный
            InspectionScheduled => "#4D9DE0",  // голубой
            WaitingConfirmation => "#FFB347",  // оранжевый
            UnderInspection => "#5D9B9B",      // серо-голубой
            FalseReading => "#9E9E9E",         // серый
            RepairRequired => "#FF6B6B",       // красный
            UnderRepair => "#4D9DE0",          // голубой
            Completed => "#4CAF50",            // зеленый
            _ => "#757575"
        };
    }
}

// Информация о проверке (для уведомления от инспектора)
public class InspectionResult
{
    public int ProblemId { get; set; }
    public bool IsFalseReading { get; set; } // true - ложные показания, false - требуется ремонт
    public string? RepairDescription { get; set; }
    public decimal? CurrentValue { get; set; }
    public decimal? ThresholdValue { get; set; }
}