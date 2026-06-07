using System;
using System.Collections.Generic;

namespace Taneco.Models;

public class EquipmentForReport
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string ManufacturerCountry { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string InstallationLocation { get; set; } = string.Empty;
    public string PipelineName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string MeasurementType { get; set; } = string.Empty;
    public string ControlPoint { get; set; } = string.Empty;
    public decimal? LastMeasurementValue { get; set; }
    public DateTime? LastMeasurementDate { get; set; }
    public string LastRepairDescription { get; set; } = string.Empty;
    public DateTime? LastRepairDate { get; set; }
    public decimal MinThreshold { get; set; }
    public decimal MaxThreshold { get; set; }
    public int? ProductionYear { get; set; }
    public DateTime? LastCalibration { get; set; }
}

public class SensorForDropdown
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class SensorStatisticsResult
{
    public int TotalMeasurements { get; set; }
    public int ProblemCount { get; set; }
    public decimal AverageValue { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<SensorDailyStat> DailyStats { get; set; } = new();
}

public class SensorDailyStat
{
    public DateTime Date { get; set; }
    public decimal AverageValue { get; set; }
    public int MeasurementCount { get; set; }
    public int ProblemCount { get; set; }
}

public class ComprehensiveReport
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime GeneratedAt { get; set; }
    public int TotalSensors { get; set; }
    public int TotalMeasurements { get; set; }
    public int TotalProblems { get; set; }
    public int TotalInspections { get; set; }
    public int TotalRepairs { get; set; }
    public decimal AverageMeasurementValue { get; set; }
    public List<EquipmentForReport> EquipmentList { get; set; } = new();
    public List<ProblemSummary> ProblemSummary { get; set; } = new();
}

public class ProblemSummary
{
    public string ProblemType { get; set; } = string.Empty;
    public int Count { get; set; }
    public string RiskCategory { get; set; } = string.Empty;
}