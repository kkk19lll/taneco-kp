using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Taneco.Models;
using Taneco.Services;

namespace Taneco.ViewModels;

public class MeasurementDetailsViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private Measurement _measurement;
    private ObservableCollection<Measurement> _historyMeasurements;
    private bool _isLoading;

    public MeasurementDetailsViewModel(Measurement measurement, DatabaseService db)
    {
        _db = db;
        _measurement = measurement;
        _historyMeasurements = new ObservableCollection<Measurement>();

        ShowWeekCommand = new RelayCommand(() => Task.Run(async () => await LoadHistory(7)));
        ShowMonthCommand = new RelayCommand(() => Task.Run(async () => await LoadHistory(30)));
        ShowYearCommand = new RelayCommand(() => Task.Run(async () => await LoadHistory(365)));
        CloseCommand = new RelayCommand(Close);

        Task.Run(async () => await LoadHistory(7));
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
        IsLoading = true;
        try
        {
            var startDate = Measurement.Date.Date.AddDays(-days);
            var endDate = Measurement.Date.Date;
            
            var allMeasurements = await _db.GetAllMeasurementsAsync();
            var filtered = allMeasurements
                .Where(m => m.SensorId == Measurement.SensorId && m.Date >= startDate && m.Date <= endDate)
                .OrderByDescending(m => m.Date)
                .ToList();
            
            HistoryMeasurements.Clear();
            foreach (var m in filtered)
                HistoryMeasurements.Add(m);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadHistory error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Close()
    {
        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.Windows.OfType<Views.MeasurementDetailsWindow>().FirstOrDefault();
            window?.Close();
        }
    }
}