using System;

namespace Taneco.Models;

public class Pipeline
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime InstallationDate { get; set; }
    public string Material { get; set; } = string.Empty;
    public decimal Length { get; set; }
    public decimal Diameter { get; set; }
    public int SensorCount { get; set; }
    public int ProblemCount { get; set; }
    
    // Добавленные свойства
    public string LastRepairDate { get; set; } = "Нет ремонтов";
    public string LastRepairStatus { get; set; } = "Нет ремонтов";
    
    public string InstallationYear => InstallationDate.ToString("yyyy");
    public string LengthWithUnit => $"{Length} м";
    public string DiameterWithUnit => $"{Diameter} м";
    
    public int Age
    {
        get
        {
            var today = DateTime.Today;
            var age = today.Year - InstallationDate.Year;
            if (InstallationDate.Date > today.AddYears(-age)) age--;
            return age;
        }
    }
    
    public string AgeWithUnit => $"{Age} лет";
}