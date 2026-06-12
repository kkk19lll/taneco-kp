using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Taneco.Models;
using Taneco.Services;
using Taneco.Views;

namespace Taneco.ViewModels;

public class RepairDetailsViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private RepairFullDetails? _repairDetails;
    private int _repairId;
    private User? _currentUser;
    private string _editBudget;
    private RepairTeam? _selectedTeam;
    private ObservableCollection<RepairTeam> _repairTeams;
    private string _selectedStatus;
    private ObservableCollection<string> _availableStatuses;
    private bool _isGeneratingReport;
    private string _reportProgress = string.Empty;
    private Window? _tempWindow;
    private Window? _parentWindow;

    public RepairDetailsViewModel(int repairId, DatabaseService db, User? currentUser)
    {
        _db = db;
        _repairId = repairId;
        _currentUser = currentUser;
        _editBudget = "0";
        _repairTeams = new ObservableCollection<RepairTeam>();
        _availableStatuses = new ObservableCollection<string>();
        _selectedStatus = string.Empty;
        _reportProgress = string.Empty;

        SaveBudgetCommand = new RelayCommand(() => _ = SaveBudget(), () => CanManage);
        AssignTeamCommand = new RelayCommand(() => _ = AssignTeam(), () => CanManage && SelectedTeam != null && Repair?.Status == "Запланирован");
        DiagnoseTeamCommand = new RelayCommand(() => _ = DiagnoseTeam(), () => SelectedTeam != null);
        UpdateStatusCommand = new RelayCommand(() => _ = UpdateStatus(), () => CanUpdateStatus);
        GenerateReportCommand = new RelayCommand(() => _ = GenerateReport());
        CloseCommand = new RelayCommand(Close);

        Task.Run(async () => await LoadAllData());
    }

    public void SetParentWindow(Window window)
    {
        _parentWindow = window;
    }

    private async Task LoadAllData()
    {
        await LoadRepairDetails();
        await LoadTeams();
        await LoadAvailableStatuses();
    }

    public RepairFullDetails? Repair
    {
        get => _repairDetails;
        set
        {
            SetProperty(ref _repairDetails, value);
            if (value != null)
            {
                _editBudget = value.Budget.ToString();
                _selectedStatus = value.Status;
                Dispatcher.UIThread.Post(() =>
                {
                    OnPropertyChanged(nameof(EditBudget));
                    OnPropertyChanged(nameof(SelectedStatus));
                    OnPropertyChanged(nameof(CanAssignTeam));
                    OnPropertyChanged(nameof(CanUpdateStatus));
                    OnPropertyChanged(nameof(IsRepairCompleted));
                    ((RelayCommand)GenerateReportCommand).RaiseCanExecuteChanged();
                });
                Console.WriteLine($"Repair loaded with status: '{value.Status}'");
            }
        }
    }

    public bool CanManage => _currentUser?.Role == "Начальник_ремонтной_службы" || _currentUser?.Role == "Администратор";
    public bool CanAssignTeam => CanManage && (Repair?.Status == "Запланирован");
    public bool CanUpdateStatus => CanManage && Repair != null && Repair.Status.ToLower().Replace("ё", "е") != "завершен";

    public bool IsRepairCompleted
    {
        get
        {
            if (Repair == null) return false;
            string status = Repair.Status.ToLower().Replace("ё", "е");
            return status == "завершен";
        }
    }

    public string EditBudget { get => _editBudget; set => SetProperty(ref _editBudget, value); }
    public ObservableCollection<RepairTeam> RepairTeams { get => _repairTeams; set => SetProperty(ref _repairTeams, value); }
    public RepairTeam? SelectedTeam { get => _selectedTeam; set { SetProperty(ref _selectedTeam, value); ((RelayCommand)AssignTeamCommand).RaiseCanExecuteChanged(); } }
    public ObservableCollection<string> AvailableStatuses { get => _availableStatuses; set => SetProperty(ref _availableStatuses, value); }
    public string SelectedStatus { get => _selectedStatus; set { if (SetProperty(ref _selectedStatus, value)) ((RelayCommand)UpdateStatusCommand).RaiseCanExecuteChanged(); } }
    public bool IsGeneratingReport { get => _isGeneratingReport; set { SetProperty(ref _isGeneratingReport, value); ((RelayCommand)GenerateReportCommand).RaiseCanExecuteChanged(); } }
    public string ReportProgress { get => _reportProgress; set => SetProperty(ref _reportProgress, value); }

    public ICommand SaveBudgetCommand { get; }
    public ICommand AssignTeamCommand { get; }
    public ICommand DiagnoseTeamCommand { get; }
    public ICommand UpdateStatusCommand { get; }
    public ICommand GenerateReportCommand { get; }
    public ICommand CloseCommand { get; }

    private async Task LoadRepairDetails()
    {
        try
        {
            var details = await _db.GetRepairFullDetailsAsync(_repairId);
            if (details != null)
                await Dispatcher.UIThread.InvokeAsync(() => Repair = details);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadRepairDetails error: {ex.Message}");
        }
    }

    private async Task LoadTeams()
    {
        try
        {
            var teams = await _db.GetAvailableRepairTeamsAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RepairTeams.Clear();
                foreach (var t in teams) RepairTeams.Add(t);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadTeams error: {ex.Message}");
        }
    }

    private async Task LoadAvailableStatuses()
    {
        try
        {
            if (Repair != null)
            {
                var statuses = await _db.GetAvailableStatusesForRepairAsync(Repair.Status);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AvailableStatuses.Clear();
                    foreach (var s in statuses) AvailableStatuses.Add(s);
                    if (AvailableStatuses.Count > 0 && !AvailableStatuses.Contains(SelectedStatus))
                        SelectedStatus = AvailableStatuses[0];
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadAvailableStatuses error: {ex.Message}");
        }
    }

    private async Task SaveBudget()
    {
        if (Repair == null) return;
        if (decimal.TryParse(EditBudget, out var budget))
        {
            var success = await _db.UpdateRepairBudgetAsync(Repair.Id, budget);
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (success)
                {
                    Repair!.Budget = budget;
                    var box = MessageBoxManager.GetMessageBoxStandard("Успех", "Бюджет сохранён", ButtonEnum.Ok);
                    await box.ShowAsync();
                }
            });
        }
    }

    private async Task DiagnoseTeam()
    {
        if (SelectedTeam == null) return;
        var diagnosis = await _db.DiagnoseTeamAssignmentAsync(SelectedTeam.Id);
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var box = MessageBoxManager.GetMessageBoxStandard($"Диагностика бригады: {SelectedTeam!.Name}", diagnosis, ButtonEnum.Ok);
            await box.ShowAsync();
        });
    }

    private async Task AssignTeam()
    {
        if (Repair == null || SelectedTeam == null) return;
        try
        {
            var diagnosis = await _db.DiagnoseTeamAssignmentAsync(SelectedTeam.Id);
            var confirmResult = ButtonResult.No;
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Диагностика", diagnosis + "\n\nПродолжить?", ButtonEnum.YesNo);
                confirmResult = await box.ShowAsync();
            });
            if (confirmResult != ButtonResult.Yes) return;

            var success = await _db.AssignRepairTeamAsync(Repair.Id, SelectedTeam.Id);
            if (success) await LoadRepairDetails();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AssignTeam error: {ex.Message}");
        }
    }

    private async Task UpdateStatus()
    {
        if (Repair == null || string.IsNullOrEmpty(SelectedStatus) || SelectedStatus == Repair.Status) return;
        var success = await _db.UpdateRepairStatusAsync(Repair.Id, SelectedStatus);
        if (success)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                Repair!.Status = SelectedStatus;
                OnPropertyChanged(nameof(IsRepairCompleted));
                await LoadAvailableStatuses();
                var box = MessageBoxManager.GetMessageBoxStandard("Успех", $"Статус изменён на \"{SelectedStatus}\"", ButtonEnum.Ok);
                await box.ShowAsync();
            });
        }
    }

    private async Task GenerateReport()
    {
        if (Repair == null)
        {
            await ShowError("Ошибка", "Данные ремонта не загружены");
            return;
        }

        string status = Repair.Status.ToLower().Replace("ё", "е");
        if (status != "завершен")
        {
            await ShowError("Ошибка", $"Отчет можно сформировать только для завершенного ремонта.\nТекущий статус: {Repair.Status}");
            return;
        }

        try
        {
            IsGeneratingReport = true;
            await OpenSaveFileDialogAndGenerateReport();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GenerateReport error: {ex.Message}");
            await ShowError("Ошибка", ex.Message);
        }
        finally
        {
            IsGeneratingReport = false;
        }
    }

    private async Task OpenSaveFileDialogAndGenerateReport()
    {
        try
        {
            IStorageProvider? storageProvider = null;

            if (_parentWindow != null && _parentWindow.IsVisible)
            {
                storageProvider = _parentWindow.StorageProvider;
            }

            if (storageProvider == null && App.Current?.ApplicationLifetime is ClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow?.IsVisible == true)
                {
                    storageProvider = desktop.MainWindow.StorageProvider;
                    _parentWindow = desktop.MainWindow;
                }
                else if (desktop.Windows.Count > 0)
                {
                    var activeWindow = desktop.Windows.FirstOrDefault(w => w.IsVisible);
                    if (activeWindow != null)
                    {
                        storageProvider = activeWindow.StorageProvider;
                        _parentWindow = activeWindow;
                    }
                }
            }

            if (storageProvider == null)
            {
                _tempWindow = new Window
                {
                    Width = 400,
                    Height = 300,
                    Title = "Сохранение отчета",
                    ShowInTaskbar = false,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                var panel = new StackPanel();
                panel.Children.Add(new TextBlock { Text = "Пожалуйста, выберите место для сохранения отчета...", Margin = new Avalonia.Thickness(10) });
                panel.Children.Add(new Avalonia.Controls.ProgressBar { IsIndeterminate = true, Height = 20, Margin = new Avalonia.Thickness(10) });
                _tempWindow.Content = panel;

                _tempWindow.Show();
                storageProvider = _tempWindow.StorageProvider;
                await Task.Delay(200);
            }

            if (storageProvider == null)
            {
                await ShowError("Ошибка", "Не удалось получить доступ к файловой системе");
                return;
            }

            string defaultFileName = $"Отчёт_по_ремонту_{Repair!.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Сохранить PDF отчет",
                SuggestedFileName = defaultFileName,
                DefaultExtension = "pdf",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PDF файлы") { Patterns = new[] { "*.pdf" } }
                }
            });

            if (_tempWindow != null)
            {
                _tempWindow.Close();
                _tempWindow = null;
            }

            if (file == null) return;

            string filePath = file.Path.LocalPath;
            var success = await _db.GenerateRepairReportAsync(Repair.Id, filePath);

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (success)
                {
                    var box = MessageBoxManager.GetMessageBoxStandard("Успех", $"Отчёт сохранён:\n{filePath}", ButtonEnum.Ok);
                    await box.ShowAsync();
                }
                else
                {
                    await ShowError("Ошибка", "Не удалось сгенерировать отчёт");
                }
            });
        }
        catch (Exception ex)
        {
            if (_tempWindow != null)
            {
                _tempWindow.Close();
                _tempWindow = null;
            }
            await ShowError("Ошибка", ex.Message);
        }
    }

    private async Task ShowError(string title, string message)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var box = MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum.Ok, Icon.Error);
            await box.ShowAsync();
        });
    }

    private void Close()
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var window = desktop.Windows.OfType<RepairDetailsWindow>().FirstOrDefault(w => w.DataContext == this);
                    window?.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing window: {ex.Message}");
            }
        });
    }
}