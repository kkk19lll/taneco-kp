using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using System.Linq;
using Taneco.Models;
using Taneco.Services;
using Taneco.Views;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace Taneco.ViewModels;

public class InspectionDetailsViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private Inspection _inspection;
    private User? _currentUser;
    private bool _showRejectReason;
    private string _rejectReason = string.Empty;
    private bool _showAssignRepair;

    public InspectionDetailsViewModel(Inspection inspection, DatabaseService db, User? currentUser)
    {
        _db = db;
        _inspection = inspection;
        _currentUser = currentUser;

        StartInspectionCommand = new RelayCommand(() => Task.Run(async () => await StartInspection()));
        ConfirmInspectionCommand = new RelayCommand(() => Task.Run(async () => await ConfirmInspection()));
        RejectInspectionCommand = new RelayCommand(() => ShowRejectReason = true);
        DelayInspectionCommand = new RelayCommand(() => Task.Run(async () => await DelayInspection()));
        SubmitRejectCommand = new RelayCommand(() => Task.Run(async () => await SubmitReject()));
        CancelRejectCommand = new RelayCommand(() => { ShowRejectReason = false; RejectReason = string.Empty; });
        AssignRepairCommand = new RelayCommand(() => Task.Run(async () => await AssignRepair()));
        CloseCommand = new RelayCommand(Close);
    }

    public Inspection Inspection
    {
        get => _inspection;
        set => SetProperty(ref _inspection, value);
    }

    public bool CanTakeAction => _currentUser?.Role == "Инспектор" && Inspection.Status != "Завершена" && Inspection.Status != "Отменена";

    public bool CanStartInspection => Inspection.Status == "Запланирована";

    public bool CanConfirmInspection => Inspection.Status == "В процессе выполнения";

    public bool ShowRejectReason
    {
        get => _showRejectReason;
        set => SetProperty(ref _showRejectReason, value);
    }

    public string RejectReason
    {
        get => _rejectReason;
        set => SetProperty(ref _rejectReason, value);
    }

    public bool ShowAssignRepair
    {
        get => _showAssignRepair;
        set => SetProperty(ref _showAssignRepair, value);
    }

    public ICommand StartInspectionCommand { get; }
    public ICommand ConfirmInspectionCommand { get; }
    public ICommand RejectInspectionCommand { get; }
    public ICommand DelayInspectionCommand { get; }
    public ICommand SubmitRejectCommand { get; }
    public ICommand CancelRejectCommand { get; }
    public ICommand AssignRepairCommand { get; }
    public ICommand CloseCommand { get; }

    private async Task StartInspection()
    {
        var success = await _db.UpdateInspectionStatusAsync(Inspection.Id, "В процессе выполнения");
        if (success)
        {
            Inspection.Status = "В процессе выполнения";
            OnPropertyChanged(nameof(Inspection));
            OnPropertyChanged(nameof(CanStartInspection));
            OnPropertyChanged(nameof(CanConfirmInspection));
            OnPropertyChanged(nameof(CanTakeAction));
        }
    }

    private async Task ConfirmInspection()
    {
        ShowAssignRepair = true;
    }

    private async Task DelayInspection()
    {
        var success = await _db.UpdateInspectionStatusAsync(Inspection.Id, "Отложена", "Отложено инспектором");
        if (success)
        {
            Inspection.Status = "Отложена";
            OnPropertyChanged(nameof(Inspection));
            OnPropertyChanged(nameof(CanTakeAction));
            await _db.UpdateProblemStatusAsync(Inspection.ProblemId, "Отложена");
            Close();
        }
    }

    private async Task SubmitReject()
    {
        if (string.IsNullOrWhiteSpace(RejectReason))
        {
            var box = MessageBoxManager.GetMessageBoxStandard("Ошибка", "Укажите причину отклонения");
            await box.ShowAsync();
            return;
        }

        var success = await _db.UpdateInspectionStatusAsync(Inspection.Id, "Отменена", RejectReason);
        if (success)
        {
            Inspection.Status = "Отменена";
            await _db.UpdateProblemStatusAsync(Inspection.ProblemId, "Отменена");
            ShowRejectReason = false;
            OnPropertyChanged(nameof(Inspection));
            OnPropertyChanged(nameof(CanTakeAction));
            Close();
        }
    }

    private async Task AssignRepair()
    {
        var dialog = new AssignRepairWindow();
        var viewModel = new AssignRepairViewModel(Inspection.ProblemId, _db);
        dialog.DataContext = viewModel;

        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            await dialog.ShowDialog(desktop.MainWindow);
            if (viewModel.IsAssigned)
            {
                await _db.UpdateInspectionStatusAsync(Inspection.Id, "Завершена", "Назначен ремонт");
                ShowAssignRepair = false;
                Close();
            }
        }
    }

    private void Close()
    {
        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.Windows.OfType<Views.InspectionDetailsWindow>().FirstOrDefault();
            window?.Close();
        }
    }
}