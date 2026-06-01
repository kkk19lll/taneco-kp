using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Taneco.Models;
using Taneco.Services;

namespace Taneco.ViewModels;

public class AssignInspectionViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private readonly int _problemId;
    private ObservableCollection<Employee> _inspectors;
    private Employee? _selectedInspector;
    private string _selectedUrgency = "Средняя";
    private string _comment = string.Empty;
    private bool _isAssigned;

    public ObservableCollection<string> UrgencyLevels { get; } = new() { "Низкая", "Средняя", "Высокая", "Критическая" };

    public AssignInspectionViewModel(int problemId, DatabaseService db)
    {
        _db = db;
        _problemId = problemId;
        _inspectors = new ObservableCollection<Employee>();

        AssignCommand = new RelayCommand(() => Task.Run(async () => await Assign()), () => SelectedInspector != null);
        CloseCommand = new RelayCommand(Close);

        Task.Run(async () => await LoadInspectors());
    }

    public ObservableCollection<Employee> Inspectors
    {
        get => _inspectors;
        set => SetProperty(ref _inspectors, value);
    }

    public Employee? SelectedInspector
    {
        get => _selectedInspector;
        set 
        { 
            if (SetProperty(ref _selectedInspector, value))
                ((RelayCommand)AssignCommand).RaiseCanExecuteChanged();
        }
    }

    public string SelectedUrgency
    {
        get => _selectedUrgency;
        set => SetProperty(ref _selectedUrgency, value);
    }

    public string Comment
    {
        get => _comment;
        set => SetProperty(ref _comment, value);
    }

    public bool CanAssign => SelectedInspector != null;
    public bool IsAssigned => _isAssigned;

    public ICommand AssignCommand { get; }
    public ICommand CloseCommand { get; }

    private async Task LoadInspectors()
    {
        var allEmployees = await _db.GetEmployeesAsync();
        var inspectors = allEmployees.Where(e => e.Role == "Инспектор").ToList();
        Inspectors.Clear();
        foreach (var i in inspectors)
            Inspectors.Add(i);
    }

    private async Task Assign()
    {
        if (SelectedInspector == null) return;
        
        var description = $"Срочность: {SelectedUrgency}. {Comment}";
        var success = await _db.CreateInspectionAsync(_problemId, SelectedInspector.Id, description, SelectedUrgency);
        
        if (success)
        {
            _isAssigned = true;
            Close();
        }
    }

    private void Close()
    {
        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.Windows.OfType<Views.AssignInspectionWindow>().FirstOrDefault();
            window?.Close();
        }
    }
}