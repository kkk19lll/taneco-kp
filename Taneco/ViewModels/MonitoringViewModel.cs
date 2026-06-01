using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Taneco.Models;
using Taneco.Services;
using Taneco.Views;

namespace Taneco.ViewModels;

public class MonitoringViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private ObservableCollection<Measurement> _measurements;
    private ObservableCollection<Problem> _activeProblems;
    private bool _isLoading;
    private string _statusMessage;
    private User? _currentUser;
    private ObservableCollection<DateTime> _availableDates;
    private DateTime _selectedDate;

    public MonitoringViewModel()
    {
        _db = new DatabaseService();
        _measurements = new ObservableCollection<Measurement>();
        _activeProblems = new ObservableCollection<Problem>();
        _availableDates = new ObservableCollection<DateTime>();
        _statusMessage = "Готов к работе";
        _selectedDate = DateTime.Today;

        RefreshCommand = new RelayCommand(() => Task.Run(async () => await LoadDataAsync()), () => !IsLoading);
        MeasurementClickCommand = new RelayCommand(OnMeasurementClick);
        ProblemClickCommand = new RelayCommand(OnProblemClick);

        Task.Run(async () => await LoadDataAsync());
    }

    public User? CurrentUser
    {
        get => _currentUser;
        set => SetProperty(ref _currentUser, value);
    }

    public ObservableCollection<Measurement> Measurements
    {
        get => _measurements;
        set => SetProperty(ref _measurements, value);
    }

    public ObservableCollection<DateTime> AvailableDates
    {
        get => _availableDates;
        set => SetProperty(ref _availableDates, value);
    }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value))
            {
                Task.Run(async () => await SelectDate(value));
            }
        }
    }

    public ObservableCollection<Problem> ActiveProblems
    {
        get => _activeProblems;
        set => SetProperty(ref _activeProblems, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool CanSelectDate => CurrentUser?.CanSelectDate ?? false;
    public bool CanClick => CurrentUser?.Role == "Оператор";

    public ICommand RefreshCommand { get; }
    public ICommand MeasurementClickCommand { get; }
    public ICommand ProblemClickCommand { get; }

    public async Task SelectDate(DateTime date)
    {
        SelectedDate = date;
        await LoadMeasurementsForDate(date);
        await LoadActiveProblemsForDateAsync(date);
    }

    public async Task LoadActiveProblemsForDateAsync(DateTime date)
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var allProblems = await _db.GetActiveProblemsAsync();
            var filteredProblems = allProblems.Where(p => p.NotificationDate.Date == date.Date).ToList();

            ActiveProblems.Clear();
            foreach (var p in filteredProblems)
                ActiveProblems.Add(p);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки проблем: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadDataAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        StatusMessage = "Загрузка данных...";

        try
        {
            var allMeasurements = await _db.GetAllMeasurementsAsync();
            var uniqueDates = allMeasurements
                .Select(m => m.Date.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            AvailableDates.Clear();
            foreach (var date in uniqueDates)
                AvailableDates.Add(date);

            if (AvailableDates.Any())
            {
                SelectedDate = AvailableDates.First();
                await LoadMeasurementsForDate(SelectedDate);
                await LoadActiveProblemsForDateAsync(SelectedDate);
            }
            else
            {
                var allProblems = await _db.GetActiveProblemsAsync();
                ActiveProblems.Clear();
                foreach (var p in allProblems)
                    ActiveProblems.Add(p);
            }

            StatusMessage = $"Данные загружено: {Measurements.Count} показаний, {ActiveProblems.Count} проблем";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadMeasurementsForDate(DateTime date)
    {
        try
        {
            var measurementsForDate = await _db.GetMeasurementsByDateAsync(date);
            Measurements.Clear();
            foreach (var m in measurementsForDate)
                Measurements.Add(m);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки показаний: {ex.Message}";
        }
    }

    private void OnMeasurementClick(object parameter)
    {
        if (parameter is Measurement measurement && CanClick)
        {
            var dialog = new MeasurementDetailsWindow();
            var viewModel = new MeasurementDetailsViewModel(measurement, _db);
            dialog.DataContext = viewModel;

            if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                dialog.ShowDialog(desktop.MainWindow);
            }
        }
    }

    private void OnProblemClick(object parameter)
    {
        if (parameter is Problem problem && CanClick)
        {
            var dialog = new ProblemDetailsWindow();
            var viewModel = new ProblemDetailsViewModel(problem, _db, CurrentUser);
            dialog.DataContext = viewModel;

            if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                dialog.ShowDialog(desktop.MainWindow);
                Task.Run(async () => await LoadActiveProblemsForDateAsync(SelectedDate));
            }
        }
    }

    public DatabaseService GetDatabaseService()
    {
        return _db;
    }
}