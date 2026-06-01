using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Taneco.Models;
using Taneco.Services;
using Taneco.Views;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace Taneco.ViewModels;

public class RepairsViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private ObservableCollection<Repair> _allRepairs;
    private ObservableCollection<Repair> _filteredRepairs;
    private bool _isLoading;
    private User? _currentUser;
    private string _selectedStatusFilter = "Все";
    private ObservableCollection<string> _statusFilters;

    public RepairsViewModel()
    {
        _db = new DatabaseService();
        _allRepairs = new ObservableCollection<Repair>();
        _filteredRepairs = new ObservableCollection<Repair>();
        _statusFilters = new ObservableCollection<string> { "Все", "Запланирован", "В процессе ремонта", "Завершен", "Аварийная остановка" };

        LoadCommand = new RelayCommand(() => Task.Run(async () => await LoadRepairsAsync()), () => !IsLoading);
        RepairClickCommand = new RelayCommand(OnRepairClick);
        CreateReportCommand = new RelayCommand(() => Task.Run(async () => await CreateReport()), () => CanCreateReport);

        Task.Run(async () => await LoadRepairsAsync());
    }

    public User? CurrentUser
    {
        get => _currentUser;
        set
        {
            SetProperty(ref _currentUser, value);
            OnPropertyChanged(nameof(CanCreateReport));
            Task.Run(async () => await LoadRepairsAsync());
        }
    }

    public ObservableCollection<Repair> FilteredRepairs
    {
        get => _filteredRepairs;
        set => SetProperty(ref _filteredRepairs, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public ObservableCollection<string> StatusFilters
    {
        get => _statusFilters;
        set => SetProperty(ref _statusFilters, value);
    }

    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
                ApplyFilters();
        }
    }

    public bool CanCreateReport => CurrentUser?.Role == "Администратор";

    public ICommand LoadCommand { get; }
    public ICommand RepairClickCommand { get; }
    public ICommand CreateReportCommand { get; }

    private async Task LoadRepairsAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            ObservableCollection<Repair> repairs;

            if (CurrentUser?.Role == "Администратор")
            {
                repairs = await _db.GetRepairsAsync();
            }
            else if (CurrentUser?.Role == "Начальник_ремонтной_службы")
            {
                repairs = await _db.GetRepairsByManagerAsync(CurrentUser.Id);
            }
            else
            {
                repairs = new ObservableCollection<Repair>();
            }

            _allRepairs.Clear();
            foreach (var r in repairs)
                _allRepairs.Add(r);

            ApplyFilters();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadRepairsAsync error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilters()
    {
        var filtered = _allRepairs.AsEnumerable();

        if (SelectedStatusFilter != "Все")
        {
            filtered = filtered.Where(r => r.Status == SelectedStatusFilter);
        }

        FilteredRepairs.Clear();
        foreach (var r in filtered)
            FilteredRepairs.Add(r);
    }

    private void OnRepairClick(object parameter)
    {
        if (parameter is Repair repair)
        {
            var dialog = new RepairDetailsWindow();
            var viewModel = new RepairDetailsViewModel(repair, _db, CurrentUser);
            dialog.DataContext = viewModel;

            if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                dialog.ShowDialog(desktop.MainWindow);
                Task.Run(async () => await LoadRepairsAsync());
            }
        }
    }

    private async Task CreateReport()
    {
        var box = MessageBoxManager.GetMessageBoxStandard("Отчёт по ремонтам",
            "Выберите формат отчёта: PDF или Excel\n\nФункция в разработке",
            ButtonEnum.Ok);
        await box.ShowAsync();
    }
}