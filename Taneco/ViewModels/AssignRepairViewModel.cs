using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Taneco.Models;
using Taneco.Services;

namespace Taneco.ViewModels;

public class AssignRepairViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private readonly int _problemId;
    private ObservableCollection<Employee> _repairManagers;
    private Employee? _selectedRepairManager;
    private string _selectedPriority = "Средний";
    private string _comment = string.Empty;
    private bool _isAssigned;

    public ObservableCollection<string> Priorities { get; } = new() { "Низкий", "Средний", "Высокий", "Критический" };

    public AssignRepairViewModel(int problemId, DatabaseService db)
    {
        _db = db;
        _problemId = problemId;
        _repairManagers = new ObservableCollection<Employee>();

        AssignCommand = new RelayCommand(() => Task.Run(async () => await Assign()), () => SelectedRepairManager != null);
        CloseCommand = new RelayCommand(Close);

        Task.Run(async () => await LoadRepairManagers());
    }

    public ObservableCollection<Employee> RepairManagers
    {
        get => _repairManagers;
        set => SetProperty(ref _repairManagers, value);
    }

    public Employee? SelectedRepairManager
    {
        get => _selectedRepairManager;
        set 
        { 
            if (SetProperty(ref _selectedRepairManager, value))
                ((RelayCommand)AssignCommand).RaiseCanExecuteChanged();
        }
    }

    public string SelectedPriority
    {
        get => _selectedPriority;
        set => SetProperty(ref _selectedPriority, value);
    }

    public string Comment
    {
        get => _comment;
        set => SetProperty(ref _comment, value);
    }

    public bool CanAssign => SelectedRepairManager != null;
    public bool IsAssigned => _isAssigned;

    public ICommand AssignCommand { get; }
    public ICommand CloseCommand { get; }

    private async Task LoadRepairManagers()
    {
        var allEmployees = await _db.GetEmployeesAsync();
        var managers = allEmployees.Where(e => e.Role == "Начальник_ремонтной_службы").ToList();
        RepairManagers.Clear();
        foreach (var m in managers)
            RepairManagers.Add(m);
    }

    private async Task Assign()
    {
        if (SelectedRepairManager == null) return;
        
        // Здесь логика создания ремонта
        _isAssigned = true;
        Close();
    }

    private void Close()
    {
        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.Windows.OfType<Views.AssignRepairWindow>().FirstOrDefault();
            window?.Close();
        }
    }
}