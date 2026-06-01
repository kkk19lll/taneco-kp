using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Taneco.Models;
using Taneco.Services;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace Taneco.ViewModels;

public class RepairDetailsViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private Repair _repair;
    private User? _currentUser;
    private string _editBudget;
    private RepairTeam? _selectedTeam;
    private ObservableCollection<RepairTeam> _repairTeams;
    private string _selectedStatus;
    private ObservableCollection<string> _availableStatuses;

    public RepairDetailsViewModel(Repair repair, DatabaseService db, User? currentUser)
    {
        _db = db;
        _repair = repair;
        _currentUser = currentUser;
        _editBudget = repair.Budget.ToString();
        _repairTeams = new ObservableCollection<RepairTeam>();
        _availableStatuses = new ObservableCollection<string>
        {
            "Подготовка к ремонту", "В процессе ремонта", "Испытания после ремонта",
            "Готов к запуску", "Завершен"
        };
        _selectedStatus = repair.Status;

        SaveBudgetCommand = new RelayCommand(() => Task.Run(async () => await SaveBudget()));
        AssignTeamCommand = new RelayCommand(() => Task.Run(async () => await AssignTeam()), () => SelectedTeam != null);
        UpdateStatusCommand = new RelayCommand(() => Task.Run(async () => await UpdateStatus()));
        CloseCommand = new RelayCommand(Close);

        Task.Run(async () => await LoadTeams());
    }

    public Repair Repair
    {
        get => _repair;
        set => SetProperty(ref _repair, value);
    }

    public bool CanManage => _currentUser?.Role == "Начальник_ремонтной_службы" || _currentUser?.Role == "Администратор";

    public bool CanAssignTeam => _currentUser?.Role == "Начальник_ремонтной_службы" && Repair.Status != "Завершен";

    public string EditBudget
    {
        get => _editBudget;
        set => SetProperty(ref _editBudget, value);
    }

    public ObservableCollection<RepairTeam> RepairTeams
    {
        get => _repairTeams;
        set => SetProperty(ref _repairTeams, value);
    }

    public RepairTeam? SelectedTeam
    {
        get => _selectedTeam;
        set
        {
            SetProperty(ref _selectedTeam, value);
            ((RelayCommand)AssignTeamCommand).RaiseCanExecuteChanged();
        }
    }

    public ObservableCollection<string> AvailableStatuses
    {
        get => _availableStatuses;
        set => SetProperty(ref _availableStatuses, value);
    }

    public string SelectedStatus
    {
        get => _selectedStatus;
        set => SetProperty(ref _selectedStatus, value);
    }

    public ICommand SaveBudgetCommand { get; }
    public ICommand AssignTeamCommand { get; }
    public ICommand UpdateStatusCommand { get; }
    public ICommand CloseCommand { get; }

    private async Task LoadTeams()
    {
        var teams = await _db.GetRepairTeamsAsync();
        RepairTeams.Clear();
        foreach (var t in teams)
            RepairTeams.Add(t);
    }

    private async Task SaveBudget()
    {
        if (decimal.TryParse(EditBudget, out var budget))
        {
            var success = await _db.UpdateRepairBudgetAsync(Repair.Id, budget);
            if (success)
            {
                Repair.Budget = budget;
                var box = MessageBoxManager.GetMessageBoxStandard("Успех", "Бюджет сохранён", ButtonEnum.Ok);
                await box.ShowAsync();
            }
        }
    }

    private async Task AssignTeam()
    {
        if (SelectedTeam == null) return;

        var success = await _db.AssignRepairTeamAsync(Repair.Id, SelectedTeam.Id);
        if (success)
        {
            Repair.TeamName = SelectedTeam.Name;
            var box = MessageBoxManager.GetMessageBoxStandard("Успех", "Бригада назначена", ButtonEnum.Ok);
            await box.ShowAsync();
        }
    }

    private async Task UpdateStatus()
    {
        var success = await _db.UpdateRepairStatusAsync(Repair.Id, SelectedStatus);
        if (success)
        {
            Repair.Status = SelectedStatus;
            OnPropertyChanged(nameof(Repair));
            OnPropertyChanged(nameof(CanAssignTeam));

            var box = MessageBoxManager.GetMessageBoxStandard("Успех", $"Статус изменён на {SelectedStatus}", ButtonEnum.Ok);
            await box.ShowAsync();
        }
    }

    private void Close()
    {
        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.Windows.OfType<Views.RepairDetailsWindow>().FirstOrDefault();
            window?.Close();
        }
    }
}