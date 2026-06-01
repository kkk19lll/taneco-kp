using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Taneco.Models;
using Taneco.Services;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace Taneco.ViewModels;

public class ReportsViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private bool _isLoading;
    private User? _currentUser;
    private string _reportResult = string.Empty;
    private DateTime _startDate = DateTime.Today.AddDays(-30);
    private DateTime _endDate = DateTime.Today;
    private ObservableCollection<Pipeline> _pipelines;
    private Pipeline? _selectedPipeline;
    private bool _hasResult;

    public ReportsViewModel()
    {
        _db = new DatabaseService();
        _pipelines = new ObservableCollection<Pipeline>();

        GeneratePipelineReportCommand = new RelayCommand(async () => await GeneratePipelineReportAsync(), () => !IsLoading);
        GenerateSensorStatisticsCommand = new RelayCommand(async () => await GenerateSensorStatisticsAsync(), () => !IsLoading);
        ExportReportCommand = new RelayCommand(async () => await ExportReport(), () => CanExport && HasResult);

        Task.Run(async () => await LoadPipelines());
    }

    public User? CurrentUser
    {
        get => _currentUser;
        set
        {
            SetProperty(ref _currentUser, value);
            OnPropertyChanged(nameof(CanExport));
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool CanExport => CurrentUser?.CanExportReport ?? false;

    public string ReportResult
    {
        get => _reportResult;
        set
        {
            SetProperty(ref _reportResult, value);
            HasResult = !string.IsNullOrEmpty(value) && value != "Генерация отчёта..." && value != "Расчёт статистики...";
        }
    }

    public bool HasResult
    {
        get => _hasResult;
        set => SetProperty(ref _hasResult, value);
    }

    public DateTime StartDate
    {
        get => _startDate;
        set => SetProperty(ref _startDate, value);
    }

    public DateTime EndDate
    {
        get => _endDate;
        set => SetProperty(ref _endDate, value);
    }

    public ObservableCollection<Pipeline> Pipelines
    {
        get => _pipelines;
        set => SetProperty(ref _pipelines, value);
    }

    public Pipeline? SelectedPipeline
    {
        get => _selectedPipeline;
        set => SetProperty(ref _selectedPipeline, value);
    }

    public ICommand GeneratePipelineReportCommand { get; }
    public ICommand GenerateSensorStatisticsCommand { get; }
    public ICommand ExportReportCommand { get; }

    private async Task LoadPipelines()
    {
        var pipelines = await _db.GetPipelinesAsync();
        Pipelines.Clear();
        foreach (var p in pipelines)
            Pipelines.Add(p);
    }

    private async Task GeneratePipelineReportAsync()
    {
        IsLoading = true;
        ReportResult = "Генерация отчёта...";

        try
        {
            var result = await _db.GeneratePipelineReportAsync(SelectedPipeline?.Id);
            ReportResult = $"Отчёт по трубопроводам сгенерирован.\n\n{result}";
        }
        catch (Exception ex)
        {
            ReportResult = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task GenerateSensorStatisticsAsync()
    {
        IsLoading = true;
        ReportResult = "Расчёт статистики...";

        try
        {
            var (total, problems, avg) = await _db.GetSensorStatisticsAsync(StartDate, EndDate);

            ReportResult = $"Статистика за {StartDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy}\n\n" +
                          $"Всего замеров: {total}\n" +
                          $"Проблем: {problems}\n" +
                          $"Среднее значение: {avg:F2}";
        }
        catch (Exception ex)
        {
            ReportResult = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExportReport()
    {
        var box = MessageBoxManager.GetMessageBoxStandard("Экспорт отчёта",
            "Выберите формат: PDF или Excel\n\nФункция в разработке",
            ButtonEnum.Ok);
        await box.ShowAsync();
    }
}