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
using Avalonia.Controls;
using Taneco.Views;

namespace Taneco.ViewModels;

public class ProblemDetailsViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private Problem _problem;
    private User? _currentUser;
    private ObservableCollection<ProblemHistoryEvent> _historyEvents;
    private ObservableCollection<TimelineTick> _timelineTicks;
    private ObservableCollection<YearLabel> _yearLabels;
    private ProblemHistoryEvent? _selectedHistoryEvent;
    private bool _isLoadingHistory;

    public ProblemDetailsViewModel(Problem problem, DatabaseService db, User? currentUser)
    {
        _db = db;
        _problem = problem;
        _currentUser = currentUser;
        _historyEvents = new ObservableCollection<ProblemHistoryEvent>();
        _timelineTicks = new ObservableCollection<TimelineTick>();
        _yearLabels = new ObservableCollection<YearLabel>();

        AssignInspectionCommand = new RelayCommand(() => Task.Run(async () => await AssignInspection()));
        MarkAsFalseCommand = new RelayCommand(() => Task.Run(async () => await MarkAsFalse()));
        RequestRepairCommand = new RelayCommand(() => Task.Run(async () => await RequestRepair()));
        CloseCommand = new RelayCommand(Close);
        RefreshHistoryCommand = new RelayCommand(() => Task.Run(async () => await LoadHistoryAsync()));
        SelectEventCommand = new RelayCommand<ProblemHistoryEvent>(SelectEvent);

        Task.Run(async () => await LoadHistoryAsync());
    }

    public Problem Problem
    {
        get => _problem;
        set => SetProperty(ref _problem, value);
    }

    public ObservableCollection<ProblemHistoryEvent> HistoryEvents
    {
        get => _historyEvents;
        set => SetProperty(ref _historyEvents, value);
    }

    public ObservableCollection<TimelineTick> TimelineTicks
    {
        get => _timelineTicks;
        set => SetProperty(ref _timelineTicks, value);
    }

    public ObservableCollection<YearLabel> YearLabels
    {
        get => _yearLabels;
        set => SetProperty(ref _yearLabels, value);
    }

    public ProblemHistoryEvent? SelectedHistoryEvent
    {
        get => _selectedHistoryEvent;
        set
        {
            if (SetProperty(ref _selectedHistoryEvent, value))
            {
                OnPropertyChanged(nameof(SelectedEventDetails));
                OnPropertyChanged(nameof(HasSelectedEvent));
            }
        }
    }

    public string SelectedEventDetails => SelectedHistoryEvent?.Details ?? string.Empty;

    public bool HasSelectedEvent => SelectedHistoryEvent != null;

    public bool IsLoadingHistory
    {
        get => _isLoadingHistory;
        set => SetProperty(ref _isLoadingHistory, value);
    }

    public bool CanTakeAction => _currentUser?.Role == "Оператор";

    public ICommand AssignInspectionCommand { get; }
    public ICommand MarkAsFalseCommand { get; }
    public ICommand RequestRepairCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand RefreshHistoryCommand { get; }
    public ICommand SelectEventCommand { get; }

    private async Task LoadHistoryAsync()
    {
        if (IsLoadingHistory) return;
        IsLoadingHistory = true;

        try
        {
            var history = await _db.GetProblemHistoryAsync(Problem.Id);

            HistoryEvents.Clear();
            foreach (var evt in history)
            {
                HistoryEvents.Add(evt);
            }

            // Создаем временную шкалу
            GenerateTimeline();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadHistoryAsync error: {ex.Message}");
        }
        finally
        {
            IsLoadingHistory = false;
        }
    }

    private void GenerateTimeline()
    {
        if (!HistoryEvents.Any()) return;

        TimelineTicks.Clear();
        YearLabels.Clear();

        var minDate = HistoryEvents.Min(e => e.EventDate);
        var maxDate = HistoryEvents.Max(e => e.EventDate);
        var today = DateTime.Now;
        if (today > maxDate) maxDate = today;

        var totalDays = (maxDate - minDate).TotalDays;
        if (totalDays <= 0) totalDays = 1;

        // ГОДА от первой насечки до текущего
        for (int year = minDate.Year; year <= maxDate.Year; year++)
        {
            var yearDate = new DateTime(year, 1, 1);
            if (yearDate < minDate) yearDate = minDate;
            if (yearDate > maxDate) continue;

            var position = CalculatePosition(yearDate, minDate, totalDays);

            YearLabels.Add(new YearLabel
            {
                Year = year.ToString(),
                Position = position
            });

            TimelineTicks.Add(new TimelineTick
            {
                TickType = TickType.YearTick,
                Position = position,
                YearLabel = year.ToString(),
                TooltipText = $"Год: {year}"
            });
        }

        // Группируем по дням чтоб не слипалось
        var groupedByDay = HistoryEvents
            .GroupBy(e => e.EventDate.Date)
            .Select(g => new { Date = g.Key, Events = g.ToList() })
            .OrderBy(g => g.Date)
            .ToList();

        double lastPosition = -20;
        double minDistance = 8;

        foreach (var dayGroup in groupedByDay)
        {
            double position = CalculatePosition(dayGroup.Date, minDate, totalDays);

            // Раздвигаем если слипаются
            if (position - lastPosition < minDistance && lastPosition > -20)
            {
                position = lastPosition + minDistance;
                if (position > 95) position = 95;
            }

            string formattedDate = dayGroup.Date.ToString("dd.MM");

            // Белая насечка для даты
            TimelineTicks.Add(new TimelineTick
            {
                TickType = TickType.DateTick,
                Position = position,
                DateLabel = formattedDate,
                TooltipText = dayGroup.Date.ToString("dd.MM.yyyy")
            });

            // Точки событий
            for (int i = 0; i < dayGroup.Events.Count; i++)
            {
                var evt = dayGroup.Events[i];
                double eventPos = position;

                if (dayGroup.Events.Count > 1)
                {
                    eventPos = position + (i - (dayGroup.Events.Count - 1) / 2.0) * 2;
                    if (eventPos < 2) eventPos = 2;
                    if (eventPos > 98) eventPos = 98;
                }

                TimelineTicks.Add(new TimelineTick
                {
                    TickType = TickType.EventPoint,
                    Position = eventPos,
                    EventColor = GetEventColor(evt.EventType),
                    DateLabel = formattedDate,
                    TooltipText = $"{evt.EventDate:dd.MM.yyyy HH:mm}\n{evt.Details}",
                    RelatedEvent = evt,
                    EventType = evt.EventType
                });
            }

            lastPosition = position;
        }
    }

    private double CalculatePosition(DateTime date, DateTime minDate, double totalDays)
    {
        if (totalDays <= 0) return 50;
        var daysFromStart = (date - minDate).TotalDays;
        return 5 + (daysFromStart / totalDays) * 90;
    }

    private string GetEventColor(string eventType)
    {
        return eventType switch
        {
            "Проблема" => "#FF6B6B",      // Красный
            "Проверка" => "#4ECDC4",      // Бирюзовый
            "Ремонт" => "#FFE66D",        // Желтый
            "Ложные показания" => "#95E77E", // Зеленый
            "Запланирована проверка" => "#4ECDC4",
            "В процессе ремонта" => "#FFE66D",
            _ => "#A0A0A0"               // Серый
        };
    }

    private void SelectEvent(ProblemHistoryEvent? historyEvent)
    {
        SelectedHistoryEvent = historyEvent;
    }

    private async Task AssignInspection()
    {
        var dialog = new AssignInspectionWindow();
        var viewModel = new AssignInspectionViewModel(Problem.Id, _db);
        dialog.DataContext = viewModel;

        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            await dialog.ShowDialog(desktop.MainWindow);

            if (viewModel.IsAssigned)
            {
                Problem.Status = "Запланирована проверка";
                OnPropertyChanged(nameof(Problem));
                await _db.UpdateProblemStatusAsync(Problem.Id, "Запланирована проверка");
                await LoadHistoryAsync();
            }
        }
    }

    private async Task MarkAsFalse()
    {
        await _db.UpdateProblemStatusAsync(Problem.Id, "Ложные показания");
        Problem.Status = "Ложные показания";
        OnPropertyChanged(nameof(Problem));
        await LoadHistoryAsync();

        var box = MessageBoxManager.GetMessageBoxStandard("Информация", "Проблема закрыта как ложные показания", ButtonEnum.Ok);
        await box.ShowAsync();
    }

    private async Task RequestRepair()
    {
        var dialog = new AssignRepairWindow();
        var viewModel = new AssignRepairViewModel(Problem.Id, _db);
        dialog.DataContext = viewModel;

        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            await dialog.ShowDialog(desktop.MainWindow);

            if (viewModel.IsAssigned)
            {
                Problem.Status = "В процессе ремонта";
                OnPropertyChanged(nameof(Problem));
                await _db.UpdateProblemStatusAsync(Problem.Id, "В процессе ремонта");
                await LoadHistoryAsync();
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

// Модели для временной шкалы
public class TimelineTick
{
    public TickType TickType { get; set; }
    public double Position { get; set; } // Процентная позиция (0-100)
    public string? YearLabel { get; set; }
    public string? DateLabel { get; set; }
    public string? TooltipText { get; set; }
    public string? EventColor { get; set; }
    public ProblemHistoryEvent? RelatedEvent { get; set; }
    public string? EventType { get; set; }
}

public class YearLabel
{
    public string? Year { get; set; }
    public double Position { get; set; }
}

public enum TickType
{
    YearTick,    // Серая насечка года
    DateTick,    // Белая насечка даты
    EventPoint   // Точка события
}