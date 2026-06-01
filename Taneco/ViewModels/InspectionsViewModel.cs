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

public class InspectionsViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private ObservableCollection<Inspection> _allInspections;
    private ObservableCollection<Inspection> _filteredInspections;
    private bool _isLoading;
    private User? _currentUser;
    private string _selectedStatusFilter = "Все";
    private ObservableCollection<string> _statusFilters;

    public InspectionsViewModel()
    {
        _db = new DatabaseService();
        _allInspections = new ObservableCollection<Inspection>();
        _filteredInspections = new ObservableCollection<Inspection>();
        _statusFilters = new ObservableCollection<string> { "Все", "Запланирована", "В процессе выполнения", "Завершена", "Отложена", "Отменена" };

        RefreshCommand = new RelayCommand(() => Task.Run(async () => await LoadInspectionsAsync()), () => !IsLoading);
        InspectionClickCommand = new RelayCommand(OnInspectionClick);
        CreateScheduledInspectionCommand = new RelayCommand(() => Task.Run(async () => await CreateScheduledInspection()), () => CanCreateScheduled);

        Task.Run(async () => await LoadInspectionsAsync());
    }

    public User? CurrentUser
    {
        get => _currentUser;
        set
        {
            SetProperty(ref _currentUser, value);
            OnPropertyChanged(nameof(CanCreateScheduled));
            Task.Run(async () => await LoadInspectionsAsync());
        }
    }

    public ObservableCollection<Inspection> FilteredInspections
    {
        get => _filteredInspections;
        set => SetProperty(ref _filteredInspections, value);
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

    public bool CanCreateScheduled => CurrentUser?.CanManageInspections ?? false;

    public ICommand RefreshCommand { get; }
    public ICommand InspectionClickCommand { get; }
    public ICommand CreateScheduledInspectionCommand { get; }

    private async Task LoadInspectionsAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            if (CurrentUser?.Role == "Администратор")
            {
                var inspections = await _db.GetAllInspectionsAsync();
                _allInspections.Clear();
                foreach (var i in inspections)
                    _allInspections.Add(i);
            }
            else if (CurrentUser?.Role == "Инспектор")
            {
                var inspections = await _db.GetInspectionsForInspectorAsync(CurrentUser.Id);
                _allInspections.Clear();
                foreach (var i in inspections)
                    _allInspections.Add(i);
            }

            ApplyFilters();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadInspectionsAsync error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilters()
    {
        var filtered = _allInspections.AsEnumerable();

        if (SelectedStatusFilter != "Все")
        {
            filtered = filtered.Where(i => i.Status == SelectedStatusFilter);
        }

        FilteredInspections.Clear();
        foreach (var i in filtered)
            FilteredInspections.Add(i);
    }

    private async void OnInspectionClick(object parameter)
{
    if (parameter is Inspection inspection)
    {
        var box = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(
            "Детали проверки",
            $"Проверка #{inspection.Id}\n\n" +
            $"Тип проблемы: {inspection.ProblemType}\n" +
            $"Описание: {inspection.ProblemDescription}\n" +
            $"Инспектор: {inspection.EmployeeName}\n" +
            $"Статус: {inspection.Status}\n" +
            $"Начало: {inspection.StartDate:dd.MM.yyyy}",
            MsBox.Avalonia.Enums.ButtonEnum.Ok);
        await box.ShowAsync();
    }
}

    private async Task CreateScheduledInspection()
    {
        var pipelines = await _db.GetPipelinesAsync();
        if (!pipelines.Any())
        {
            var box = MessageBoxManager.GetMessageBoxStandard("Ошибка", "Нет доступных трубопроводов");
            await box.ShowAsync();
            return;
        }

        var dialog = new CreateInspectionWindow();
        var viewModel = new CreateInspectionViewModel(_db, CurrentUser?.Id ?? 0);
        dialog.DataContext = viewModel;

        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            await dialog.ShowDialog(desktop.MainWindow);
            await LoadInspectionsAsync();
        }
    }
}