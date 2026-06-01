using System;

namespace Taneco.Models;

public class Sensor
{
    public int Id { get; set; }
    public string Model { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string ManufacturerCountry { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal MinValue { get; set; }
    public decimal MaxValue { get; set; }
    public string MeasurementType { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string ControlPoint { get; set; } = string.Empty;
    public int PipelineId { get; set; }
    public string PipelineName { get; set; } = string.Empty;
    public DateTime InstallationDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public int ProductionYear { get; set; }
    public DateTime? LastCalibration { get; set; }
    public string MeasurementTypeWithUnit => $"{MeasurementType} / {Unit}";
    public string LastMeasurementValue { get; set; } = "Нет данных";
    public string LastMeasurementDate { get; set; } = "Нет данных";
}