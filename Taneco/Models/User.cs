using System;

namespace Taneco.Models;

public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Patronymic { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime LastLogin { get; set; }

    public string FullName => $"{LastName} {FirstName} {Patronymic}".Trim();

    public bool CanMonitor => Role == "Администратор" || Role == "Оператор";

    public bool CanEditSensors => Role == "Инженер_КИПиА";

    public bool CanManageEquipment => Role == "Администратор";

    public bool CanManageProblems => Role == "Администратор" || Role == "Оператор" || Role == "Инспектор";

    public bool CanManageRepairs => Role == "Администратор" || Role == "Начальник_ремонтной_службы";

    public bool CanManageStaff => Role == "Администратор" || Role == "HR";

    public bool CanViewReports => Role == "Администратор" || Role == "Аналитик";

    public bool IsAdmin => Role == "Администратор";

    public bool CanSelectDate => Role == "Администратор" || Role == "Оператор";

    public bool CanCreateProblem => Role == "Инспектор";

    public bool CanExportReport => Role == "Аналитик" || Role == "Администратор";

    public bool CanViewInspections => Role == "Администратор" || Role == "Инспектор";

    public bool CanManageInspections => Role == "Инспектор";
}