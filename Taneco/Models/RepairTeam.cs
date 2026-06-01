namespace Taneco.Models;

public class RepairTeam
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public int WorkerCount { get; set; }
}