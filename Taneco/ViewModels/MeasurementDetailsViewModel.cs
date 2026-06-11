using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Taneco.Models;
using Taneco.Services;

namespace Taneco.ViewModels;

public class MeasurementDetailsViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private Measurement _measurement;
    private ObservableCollection<Measurement> _historyMeasurements;
    private bool _isLoading;
    private Action? _closeAction;
    private bool _isLoadingHistory; // Флаг для предотвращения повторной загрузки
    private int _currentDays; // Текущий выбранный период

    public MeasurementDetailsViewModel(Measurement measurement, DatabaseService db, Action? closeAction = null)
    {
        _db = db;
        _measurement = measurement;
        _historyMeasurements = new ObservableCollection<Measurement>();
        _closeAction = closeAction;
        _currentDays = 7; // По умолчанию неделя

        ShowWeekCommand = new RelayCommand(async () => await LoadHistory(7));
        ShowMonthCommand = new RelayCommand(async () => await LoadHistory(30));
        ShowYearCommand = new RelayCommand(async () => await LoadHistory(365));
        CloseCommand = new RelayCommand(() => _closeAction?.Invoke());

        // Используем асинхронный метод без Task.Run
        _ = LoadHistoryAsync(7);
    }

    public Measurement Measurement
    {
        get => _measurement;
        set => SetProperty(ref _measurement, value);
    }

    public ObservableCollection<Measurement> HistoryMeasurements
    {
        get => _historyMeasurements;
        set => SetProperty(ref _historyMeasurements, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public ICommand ShowWeekCommand { get; }
    public ICommand ShowMonthCommand { get; }
    public ICommand ShowYearCommand { get; }
    public ICommand CloseCommand { get; }

    private async Task LoadHistory(int days)
    {
        // Если уже загружаем тот же период или идет загрузка - пропускаем
        if (_isLoadingHistory && _currentDays == days) return;

        _currentDays = days;
        await LoadHistoryAsync(days);
    }

    private async Task LoadHistoryAsync(int days)
    {
        // Блокируем повторные вызовы
        if (_isLoadingHistory) return;

        _isLoadingHistory = true;
        IsLoading = true;

        try
        {
            var startDate = Measurement.Date.Date.AddDays(-days);
            var endDate = Measurement.Date.Date;

            var allMeasurements = await _db.GetAllMeasurementsAsync();

            // Фильтруем и сортируем
            var filtered = allMeasurements
                .Where(m => m.SensorId == Measurement.SensorId && m.Date.Date >= startDate && m.Date.Date <= endDate)
                .OrderByDescending(m => m.Date)
                .ToList();

            // Очищаем и добавляем новые данные (все в одном потоке UI)
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                HistoryMeasurements.Clear();
                foreach (var m in filtered)
                {
                    HistoryMeasurements.Add(m);
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadHistory error: {ex.Message}");
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                HistoryMeasurements.Clear();
            });
        }
        finally
        {
            IsLoading = false;
            _isLoadingHistory = false;
        }
    }
}