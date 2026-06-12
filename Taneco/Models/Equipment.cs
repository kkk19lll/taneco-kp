using System;

namespace Taneco.Models;

public class Equipment
{
    public int Id { get; set; }

    // Для отображения в списке
    public string DisplayName { get; set; } = string.Empty;
    public string DisplayDetail { get; set; } = string.Empty;

    // Общие поля
    public string Model { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string InstallationLocation { get; set; } = string.Empty;
    public string ManufacturerCountry { get; set; } = string.Empty;
    public string ControlPoint { get; set; } = string.Empty;
    public string PipelineName { get; set; } = string.Empty;
    public int? ProductionYear { get; set; }
    public DateTime? LastCalibration { get; set; }
    public decimal MinValue { get; set; }
    public decimal MaxValue { get; set; }
    public string? Unit { get; set; }
    public string? MeasurementType { get; set; }

    // Поля для трубопроводов
    public decimal Length { get; set; }
    public decimal Diameter { get; set; }
    public DateTime InstallationDate { get; set; }
    public string Material { get; set; } = string.Empty;

    // Флаг типа
    public bool IsPipeline { get; set; }
    public bool IsSensor => !IsPipeline;
}