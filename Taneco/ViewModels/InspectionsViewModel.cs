using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Taneco.Models;
using Taneco.Services;

namespace Taneco.ViewModels;

public class InspectionsViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private ObservableCollection<Inspection> _allInspections;
    private ObservableCollection<Inspection> _filteredInspections;
    private bool _isLoading;
    private User? _currentUser;
    private string _searchText = string.Empty;
    private string _selectedStatusFilter = "Все";
    private string _filterStartDateText = string.Empty;
    private string _filterEndDateText = string.Empty;
    private DateTime? _filterStartDate;
    private DateTime? _filterEndDate;
    private ObservableCollection<string> _statusFilters;
    private Window? _parentWindow;
    private Window? _tempWindow;

    public InspectionsViewModel()
    {
        _db = new DatabaseService();
        _allInspections = new ObservableCollection<Inspection>();
        _filteredInspections = new ObservableCollection<Inspection>();

        _statusFilters = new ObservableCollection<string>
        {
            "Все",
            "Запланирована",
            "В процессе выполнения",
            "Требует дополнительной диагностики",
            "Ожидает подтверждения",
            "На анализе данных",
            "Завершена",
            "Отложена",
            "Отменена",
            "Требует срочного вмешательства",
            "На согласовании"
        };

        RefreshCommand = new RelayCommand(async () => await LoadInspectionsAsync(), () => !IsLoading);
        SearchCommand = new RelayCommand(ApplyFilters);
        GenerateReportCommand = new RelayCommand(async () => await GenerateReportPdfOnly(), () => IsAdmin && !IsLoading);
        InspectionClickCommand = new RelayCommand(async (param) => await ShowInspectionDetails(param));

        Task.Run(async () => await LoadInspectionsAsync());
    }

    public void SetParentWindow(Window window)
    {
        _parentWindow = window;
    }

    public User? CurrentUser
    {
        get => _currentUser;
        set
        {
            if (SetProperty(ref _currentUser, value))
            {
                OnPropertyChanged(nameof(IsAdmin));
                Task.Run(async () => await LoadInspectionsAsync());
            }
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
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (GenerateReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                ApplyFilters();
        }
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

    public string FilterStartDateText
    {
        get => _filterStartDateText;
        set
        {
            if (SetProperty(ref _filterStartDateText, value))
            {
                _filterStartDate = ParseDate(value);
                ApplyFilters();
            }
        }
    }

    public string FilterEndDateText
    {
        get => _filterEndDateText;
        set
        {
            if (SetProperty(ref _filterEndDateText, value))
            {
                _filterEndDate = ParseDate(value);
                ApplyFilters();
            }
        }
    }

    public DateTime? FilterStartDate
    {
        get => _filterStartDate;
        set
        {
            if (SetProperty(ref _filterStartDate, value))
            {
                _filterStartDateText = value?.ToString("dd.MM.yyyy") ?? string.Empty;
                OnPropertyChanged(nameof(FilterStartDateText));
                ApplyFilters();
            }
        }
    }

    public DateTime? FilterEndDate
    {
        get => _filterEndDate;
        set
        {
            if (SetProperty(ref _filterEndDate, value))
            {
                _filterEndDateText = value?.ToString("dd.MM.yyyy") ?? string.Empty;
                OnPropertyChanged(nameof(FilterEndDateText));
                ApplyFilters();
            }
        }
    }

    public bool IsAdmin => CurrentUser?.Role == "Администратор";

    public ICommand RefreshCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand GenerateReportCommand { get; }
    public ICommand InspectionClickCommand { get; }

    private DateTime? ParseDate(string dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return null;

        string cleaned = dateString.Trim();
        cleaned = Regex.Replace(cleaned, @"[\.\-/]", ".");

        if (DateTime.TryParseExact(cleaned, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime result))
        {
            return result;
        }
        return null;
    }

    private async Task LoadInspectionsAsync()
    {
        if (IsLoading) return;

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => IsLoading = true);

        try
        {
            ObservableCollection<Inspection> inspections;

            if (CurrentUser?.Role == "Администратор")
            {
                inspections = await _db.GetAllInspectionsForAdminAsync();
            }
            else if (CurrentUser?.Role == "Инспектор")
            {
                inspections = await _db.GetInspectionsForInspectorAsync(CurrentUser.Id);
            }
            else
            {
                inspections = await _db.GetAllInspectionsAsync();
            }

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                _allInspections.Clear();
                foreach (var i in inspections)
                    _allInspections.Add(i);

                ApplyFilters();
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadInspectionsAsync error: {ex.Message}");
            await ShowError("Ошибка загрузки", $"Не удалось загрузить проверки: {ex.Message}");
        }
        finally
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
        }
    }

    private void ApplyFilters()
    {
        var filtered = _allInspections.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(i =>
                (i.EmployeeName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.ProblemType?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.ProblemDescription?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (SelectedStatusFilter != "Все")
        {
            filtered = filtered.Where(i => i.Status == SelectedStatusFilter);
        }

        if (_filterStartDate.HasValue)
        {
            filtered = filtered.Where(i => i.StartDate.Date >= _filterStartDate.Value.Date);
        }
        if (_filterEndDate.HasValue)
        {
            filtered = filtered.Where(i => i.StartDate.Date <= _filterEndDate.Value.Date);
        }

        FilteredInspections.Clear();
        foreach (var i in filtered)
            FilteredInspections.Add(i);
    }

    private async Task ShowInspectionDetails(object? parameter)
    {
        if (parameter is Inspection inspection)
        {
            var history = await _db.GetInspectionHistoryAsync(inspection.Id);

            var details = $"ИНФОРМАЦИЯ О ПРОБЛЕМЕ:\n" +
                $"Тип проблемы: {inspection.ProblemType}\n" +
                $"Описание: {inspection.ProblemDescription}\n\n" +
                $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                $"ИНФОРМАЦИЯ О ПРОВЕРКЕ:\n" +
                $"Инспектор: {inspection.EmployeeName}\n" +
                $"Дата начала: {inspection.StartDate:dd.MM.yyyy}\n" +
                $"Дата завершения: {(inspection.EndDate.HasValue ? inspection.EndDate.Value.ToString("dd.MM.yyyy") : "Не завершена")}\n" +
                $"Статус: {inspection.Status}\n" +
                $"Результат: {inspection.Result}\n" +
                $"Комментарий: {(string.IsNullOrEmpty(inspection.Description) ? "Нет комментария" : inspection.Description)}\n\n" +
                $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                $"ИСТОРИЯ ИЗМЕНЕНИЙ СТАТУСОВ:\n" +
                $"{history}";

            var box = MessageBoxManager.GetMessageBoxStandard(
                $"Детали проверки #{inspection.Id}",
                details,
                ButtonEnum.Ok);
            await box.ShowAsync();
        }
    }

    private async Task GenerateReportPdfOnly()
    {
        try
        {
            if (!IsAdmin)
            {
                await ShowError("Ошибка", "У вас нет прав для создания отчета");
                return;
            }

            if (!_filterStartDate.HasValue || !_filterEndDate.HasValue)
            {
                await ShowWarning("Нет периода", "Пожалуйста, укажите даты начала и окончания в фильтрах выше");
                return;
            }

            // Убеждаемся что мы в UI потоке для открытия диалога
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await OpenSaveFileDialogAndGenerateReport();
            });
        }
        catch (Exception ex)
        {
            await ShowError("Ошибка при сохранении отчета", ex.Message);
        }
    }

    private async Task OpenSaveFileDialogAndGenerateReport()
    {
        try
        {
            // Добавим отладочный вывод
            Console.WriteLine($"OpenSaveFileDialogAndGenerateReport: FilterStartDate={_filterStartDate?.ToString("yyyy-MM-dd")}, FilterEndDate={_filterEndDate?.ToString("yyyy-MM-dd")}");

            if (!_filterStartDate.HasValue || !_filterEndDate.HasValue)
            {
                await ShowWarning("Нет периода", "Пожалуйста, укажите даты начала и окончания в фильтрах выше");
                return;
            }

            IStorageProvider? storageProvider = null;

            // Пытаемся получить StorageProvider из родительского окна
            if (_parentWindow != null && _parentWindow.IsVisible)
            {
                storageProvider = _parentWindow.StorageProvider;
            }

            // Если нет - ищем через Application
            if (storageProvider == null && Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow?.IsVisible == true)
                {
                    storageProvider = desktop.MainWindow.StorageProvider;
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

            // Последняя попытка - создаем временное окно
            if (storageProvider == null)
            {
                _tempWindow = new Window
                {
                    Width = 400,
                    Height = 300,
                    Title = "Сохранение отчета",
                    Opacity = 1,
                    ShowInTaskbar = false,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                var panel = new StackPanel();
                var text = new TextBlock { Text = "Пожалуйста, выберите место для сохранения отчета...", Margin = new Avalonia.Thickness(10) };
                var progress = new Avalonia.Controls.ProgressBar { IsIndeterminate = true, Height = 20, Margin = new Avalonia.Thickness(10) };
                panel.Children.Add(text);
                panel.Children.Add(progress);
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

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => IsLoading = true);

            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Сохранить PDF отчет",
                SuggestedFileName = $"Отчет_по_проверкам_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                DefaultExtension = "pdf",
                FileTypeChoices = new[]
                {
                new FilePickerFileType("PDF файлы") { Patterns = new[] { "*.pdf" } },
                new FilePickerFileType("Все файлы") { Patterns = new[] { "*.*" } }
            }
            });

            if (_tempWindow != null)
            {
                _tempWindow.Close();
                _tempWindow = null;
            }

            if (file == null)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
                return;
            }

            string reportPath = file.Path.LocalPath;

            Console.WriteLine($"Выбран путь для сохранения: {reportPath}");

            // Используем даты из фильтров
            DateTime startDate = _filterStartDate.Value;
            DateTime endDate = _filterEndDate.Value;

            Console.WriteLine($"Даты для отчета: start={startDate:yyyy-MM-dd}, end={endDate:yyyy-MM-dd}");

            bool reportSuccess = await _db.GenerateReportAsync(startDate, endDate, reportPath);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);

            if (reportSuccess)
            {
                var successBox = MessageBoxManager.GetMessageBoxStandard(
                    "Успешно",
                    $"PDF отчет успешно сохранен!\n\nПуть: {reportPath}",
                    ButtonEnum.Ok,
                    Icon.Success);
                await successBox.ShowAsync();
            }
            else
            {
                await ShowWarning("Нет данных", $"За период {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy} нет проверок для формирования отчета\n\nПроверьте, что в выбранном диапазоне есть проверки.");
            }
        }
        catch (Exception ex)
        {
            if (_tempWindow != null)
            {
                _tempWindow.Close();
                _tempWindow = null;
            }
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
            Console.WriteLine($"OpenSaveFileDialogAndGenerateReport error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            await ShowError("Ошибка при сохранении отчета", ex.Message);
        }
    }

    private async Task ShowError(string title, string message)
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var box = MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum.Ok, Icon.Error);
            await box.ShowAsync();
        });
    }

    private async Task ShowWarning(string title, string message)
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var box = MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum.Ok, Icon.Warning);
            await box.ShowAsync();
        });
    }
}