using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Linq;
using Taneco.Models;
using Taneco.Services;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Avalonia.Controls.ApplicationLifetimes;
using Taneco.Views;

namespace Taneco.ViewModels;

public class ProblemDetailsViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private Problem _problem;
    private User? _currentUser;
    private ObservableCollection<StatusHistoryItem> _statusHistory;
    private bool _showAssignInspectionButton = true;
    private bool _showInspectionResponse;
    private string _inspectionResponseText = string.Empty;

    public ProblemDetailsViewModel(Problem problem, DatabaseService db, User? currentUser)
    {
        _db = db;
        _problem = problem;
        _currentUser = currentUser;
        _statusHistory = new ObservableCollection<StatusHistoryItem>();

        AssignInspectionCommand = new RelayCommand(() => Task.Run(async () => await AssignInspection()));
        MarkAsFalseCommand = new RelayCommand(() => Task.Run(async () => await MarkAsFalse()));
        RequestRepairCommand = new RelayCommand(() => Task.Run(async () => await RequestRepair()));
        CloseCommand = new RelayCommand(Close);

        LoadStatusHistory();
    }

    public Problem Problem
    {
        get => _problem;
        set => SetProperty(ref _problem, value);
    }

    public ObservableCollection<StatusHistoryItem> StatusHistory
    {
        get => _statusHistory;
        set => SetProperty(ref _statusHistory, value);
    }

    public bool CanTakeAction => _currentUser?.Role == "Оператор";

    public bool ShowAssignInspectionButton
    {
        get => _showAssignInspectionButton;
        set => SetProperty(ref _showAssignInspectionButton, value);
    }

    public bool ShowInspectionResponse
    {
        get => _showInspectionResponse;
        set => SetProperty(ref _showInspectionResponse, value);
    }

    public string InspectionResponseText
    {
        get => _inspectionResponseText;
        set => SetProperty(ref _inspectionResponseText, value);
    }

    public ICommand AssignInspectionCommand { get; }
    public ICommand MarkAsFalseCommand { get; }
    public ICommand RequestRepairCommand { get; }
    public ICommand CloseCommand { get; }

    private void LoadStatusHistory()
    {
        StatusHistory.Clear();
        StatusHistory.Add(new StatusHistoryItem { Date = Problem.NotificationDate, Status = "Создана" });
        StatusHistory.Add(new StatusHistoryItem { Date = DateTime.Now, Status = Problem.Status });
    }

    private async Task AssignInspection()
    {
        var dialog = new AssignInspectionWindow();
        var viewModel = new AssignInspectionViewModel(Problem.Id, _db);
        dialog.DataContext = viewModel;

        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            await dialog.ShowDialog(desktop.MainWindow);

            if (viewModel.IsAssigned)
            {
                Problem.Status = "Запланирована проверка";
                ShowAssignInspectionButton = false;
                StatusHistory.Add(new StatusHistoryItem { Date = DateTime.Now, Status = "Запланирована проверка" });
                OnPropertyChanged(nameof(Problem));

                await _db.UpdateProblemStatusAsync(Problem.Id, "Запланирована проверка");
            }
        }
    }

    private async Task MarkAsFalse()
    {
        await _db.UpdateProblemStatusAsync(Problem.Id, "Ложные показания");
        Problem.Status = "Ложные показания";
        ShowInspectionResponse = false;
        StatusHistory.Add(new StatusHistoryItem { Date = DateTime.Now, Status = "Ложные показания - проблема закрыта" });
        OnPropertyChanged(nameof(Problem));

        var box = MessageBoxManager.GetMessageBoxStandard("Информация", "Проблема закрыта как ложные показания", ButtonEnum.Ok);
        await box.ShowAsync();
    }

    private async Task RequestRepair()
    {
        var dialog = new AssignRepairWindow();
        var viewModel = new AssignRepairViewModel(Problem.Id, _db);
        dialog.DataContext = viewModel;

        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            await dialog.ShowDialog(desktop.MainWindow);

            if (viewModel.IsAssigned)
            {
                Problem.Status = "В процессе ремонта";
                ShowInspectionResponse = false;
                StatusHistory.Add(new StatusHistoryItem { Date = DateTime.Now, Status = "В процессе ремонта" });
                OnPropertyChanged(nameof(Problem));

                await _db.UpdateProblemStatusAsync(Problem.Id, "В процессе ремонта");
            }
        }
    }

    private void Close()
    {
        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.Windows.OfType<Views.ProblemDetailsWindow>().FirstOrDefault();
            window?.Close();
        }
    }
}

public class StatusHistoryItem
{
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty;
}