using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
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
    private DateTime _selectedDate;
    private string _selectedDateString;
    private ObservableCollection<DateTime> _availableDates;
    private bool _isInitialized;

    public MonitoringViewModel()
    {
        _db = new DatabaseService();
        _measurements = new ObservableCollection<Measurement>();
        _activeProblems = new ObservableCollection<Problem>();
        _availableDates = new ObservableCollection<DateTime>();
        _statusMessage = "Готов к работе";

        _selectedDate = DateTime.Today;
        _selectedDateString = DateTime.Today.ToString("dd.MM.yyyy");

        RefreshCommand = new RelayCommand(async () => await RefreshDataAsync());
        MeasurementClickCommand = new RelayCommand<Measurement>(OnMeasurementClick);
        ProblemClickCommand = new RelayCommand<Problem>(OnProblemClick);
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        try
        {
            _availableDates = await _db.GetAvailableDatesAsync();
            await LoadDataForDate(DateTime.Today);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка инициализации: {ex.Message}";
        }
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

    public string SelectedDateString
    {
        get => _selectedDateString;
        set
        {
            if (SetProperty(ref _selectedDateString, value))
            {
                if (DateTime.TryParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                {
                    _selectedDate = parsedDate;
                }
            }
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand MeasurementClickCommand { get; }
    public ICommand ProblemClickCommand { get; }

    private async Task RefreshDataAsync()
    {
        if (DateTime.TryParseExact(SelectedDateString, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
        {
            _selectedDate = parsedDate;
            await LoadDataForDate(_selectedDate);
        }
        else
        {
            StatusMessage = "Ошибка: неверный формат даты. Используйте ДД.ММ.ГГГГ";
        }
    }

    private async Task LoadDataForDate(DateTime date)
    {
        IsLoading = true;
        StatusMessage = $"Загрузка данных за {date:dd.MM.yyyy}...";

        try
        {
            var measurementsForDate = await _db.GetMeasurementsByDateAsync(date);
            Measurements.Clear();
            foreach (var m in measurementsForDate)
                Measurements.Add(m);

            var problemsForDate = await _db.GetActiveProblemsByDateAsync(date);
            ActiveProblems.Clear();
            foreach (var p in problemsForDate)
                ActiveProblems.Add(p);

            if (Measurements.Count == 0 && ActiveProblems.Count == 0)
            {
                StatusMessage = $"За {date:dd.MM.yyyy} данные отсутствуют";
            }
            else
            {
                StatusMessage = $"Данные за {date:dd.MM.yyyy}: {Measurements.Count} показаний, {ActiveProblems.Count} проблем";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
            Measurements.Clear();
            ActiveProblems.Clear();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async void OnMeasurementClick(Measurement? measurement)
    {
        if (measurement == null) return;

        try
        {
            var dialog = new MeasurementDetailsWindow();
            var viewModel = new MeasurementDetailsViewModel(measurement, _db, () => dialog.Close());
            dialog.DataContext = viewModel;

            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.Show();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка открытия окна: {ex.Message}";
        }
    }

    private async void OnProblemClick(Problem? problem)
    {
        if (problem == null) return;

        if (CurrentUser?.Role != "Оператор")
        {
            await ShowProblemInfoOnly(problem);
            return;
        }

        try
        {
            var dialog = new ProblemDetailsWindow();

            // БЛЯТЬ, ПЕРЕДАЙ closeAction!
            var viewModel = new ProblemDetailsViewModel(problem, _db, CurrentUser, (changed) =>
            {
                if (changed)
                {
                    // Обновляем данные, если были изменения
                    Task.Run(async () => await LoadDataForDate(_selectedDate));
                }
                dialog.Close();
            });

            dialog.DataContext = viewModel;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.Show();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка открытия окна: {ex.Message}";
        }
    }

    private async Task ShowProblemInfoOnly(Problem problem)
    {
        try
        {
            var dialog = new Window
            {
                Title = "Информация о проблеме",
                Width = 500,
                Height = 450,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var stackPanel = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 10
            };

            stackPanel.Children.Add(new TextBlock
            {
                Text = $"Тип: {problem.Type}",
                FontSize = 14,
                FontWeight = Avalonia.Media.FontWeight.Bold
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = $"Трубопровод: {problem.PipelineName}"
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = $"Описание: {problem.Description}",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = $"Дата: {problem.NotificationDate:dd.MM.yyyy}"
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = $"Показание: {problem.MeasuredValue} (порог: {problem.ThresholdValue})"
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = $"Категория риска: {problem.RiskCategory}"
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = $"Статус: {problem.Status}",
                Foreground = Avalonia.Media.Brushes.Gray
            });

            var closeButton = new Button
            {
                Content = "Закрыть",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Margin = new Avalonia.Thickness(0, 20, 0, 0)
            };
            closeButton.Click += (s, e) => dialog.Close();
            stackPanel.Children.Add(closeButton);

            dialog.Content = stackPanel;
            dialog.Show();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка открытия окна информации: {ex.Message}";
        }
    }

    public DatabaseService GetDatabaseService() => _db;
}