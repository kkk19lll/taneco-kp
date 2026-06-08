using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Taneco.Models;
using Taneco.Services;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Taneco.ViewModels;

public class ReportsViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private bool _isLoading;
    private User? _currentUser;
    private string _reportResult = string.Empty;
    private DateTime _startDate = DateTime.Today.AddDays(-30);
    private DateTime _endDate = DateTime.Today;
    private string _startDateString = string.Empty;
    private string _endDateString = string.Empty;
    private ObservableCollection<SensorForDropdown> _sensors;
    private SensorForDropdown? _selectedSensor;
    private bool _hasResult;
    private string _currentReportText = string.Empty;
    private string _currentReportTitle = string.Empty;
    private Window? _parentWindow;
    private Window? _tempWindow;

    // Для отчета по персоналу
    private ObservableCollection<EmployeeForReport> _allEmployees;
    private ObservableCollection<EmployeeForReport> _selectedEmployees;
    private ObservableCollection<string> _availableColumns;
    private ObservableCollection<string> _selectedColumns;
    private DateTime _personnelStartDate = DateTime.Today.AddYears(-5);
    private DateTime _personnelEndDate = DateTime.Today;
    private string _personnelStartDateString = string.Empty;
    private string _personnelEndDateString = string.Empty;

    public ReportsViewModel()
    {
        _db = new DatabaseService();
        _sensors = new ObservableCollection<SensorForDropdown>();
        _allEmployees = new ObservableCollection<EmployeeForReport>();
        _selectedEmployees = new ObservableCollection<EmployeeForReport>();
        _availableColumns = new ObservableCollection<string>();
        _selectedColumns = new ObservableCollection<string>();

        QuestPDF.Settings.License = LicenseType.Community;

        GenerateEquipmentReportCommand = new RelayCommand(async () => await GenerateEquipmentReportAsync(), () => !IsLoading && SelectedSensor != null);
        GenerateStatisticsCommand = new RelayCommand(async () => await GenerateStatisticsAsync(), () => !IsLoading && SelectedSensor != null);
        ExportReportCommand = new RelayCommand(async () => await ExportReportAsync(), () => CanExport && HasResult);
        GenerateComprehensiveEquipmentReportCommand = new RelayCommand(async () => await GenerateComprehensiveEquipmentReportAsync(), () => !IsLoading);
        GenerateComprehensiveProblemsReportCommand = new RelayCommand(async () => await GenerateComprehensiveProblemsReportAsync(), () => !IsLoading);

        // Команды для отчета по персоналу
        GeneratePersonnelReportCommand = new RelayCommand(async () => await GeneratePersonnelReportAsync(), () => !IsLoading && SelectedEmployees.Count > 0 && SelectedColumns.Count > 0);
        AddEmployeeToReportCommand = new RelayCommand(AddEmployeeToReport, () => SelectedEmployeeFromList != null && !_selectedEmployees.Contains(SelectedEmployeeFromList));
        RemoveEmployeeFromReportCommand = new RelayCommand(RemoveEmployeeFromReport, () => SelectedEmployeeFromSelected != null);
        AddColumnToReportCommand = new RelayCommand(AddColumnToReport, () => SelectedAvailableColumn != null && !_selectedColumns.Contains(SelectedAvailableColumn));
        RemoveColumnFromReportCommand = new RelayCommand(RemoveColumnFromReport, () => SelectedSelectedColumn != null);
        SelectAllEmployeesCommand = new RelayCommand(SelectAllEmployees);
        ClearEmployeesCommand = new RelayCommand(ClearEmployees);
        SelectAllColumnsCommand = new RelayCommand(SelectAllColumns);
        ClearColumnsCommand = new RelayCommand(ClearColumns);

        UpdateDateStrings();
        UpdatePersonnelDateStrings();
        Task.Run(async () => await LoadSensors());
        Task.Run(async () => await LoadEmployees());
        InitializeColumns();
    }

    private void InitializeColumns()
    {
        _availableColumns.Add("ФИО");
        _availableColumns.Add("Должность");
        _availableColumns.Add("Отдел");
        _availableColumns.Add("Стаж");
        _availableColumns.Add("Дата приема");
        _availableColumns.Add("Контактная информация");
        _availableColumns.Add("Роль в системе");

        // По умолчанию выбраны основные колонки
        _selectedColumns.Add("ФИО");
        _selectedColumns.Add("Должность");
        _selectedColumns.Add("Стаж");
    }

    public void SetParentWindow(Window window)
    {
        _parentWindow = window;
    }

    // Свойство для проверки прав на просмотр отчетов (Администратор или Аналитик)
    public bool CanViewReports => CurrentUser != null && (CurrentUser.Role == "Администратор" || CurrentUser.Role == "Аналитик");

    // Свойство для проверки прав на просмотр отчета по персоналу (только Аналитик)
    public bool CanViewPersonnelReports => CurrentUser != null && CurrentUser.Role == "Аналитик";

    // Свойство для проверки прав на экспорт (Администратор или Аналитик)
    public bool CanExport => CurrentUser != null && (CurrentUser.Role == "Администратор" || CurrentUser.Role == "Аналитик");

    public User? CurrentUser
    {
        get => _currentUser;
        set
        {
            SetProperty(ref _currentUser, value);
            OnPropertyChanged(nameof(CanExport));
            OnPropertyChanged(nameof(CanViewReports));
            OnPropertyChanged(nameof(CanViewPersonnelReports));
            (ExportReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (GenerateEquipmentReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (GenerateStatisticsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (GenerateComprehensiveEquipmentReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (GenerateComprehensiveProblemsReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (GeneratePersonnelReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string ReportResult
    {
        get => _reportResult;
        set
        {
            SetProperty(ref _reportResult, value);
            HasResult = !string.IsNullOrEmpty(value) &&
                       value != "Генерация отчёта..." &&
                       value != "Расчёт статистики..." &&
                       value != "Генерация сводного отчёта по оборудованию..." &&
                       value != "Генерация сводного отчёта по проблемам..." &&
                       value != "Генерация отчета по персоналу...";
            (ExportReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
        set
        {
            SetProperty(ref _startDate, value);
            UpdateDateStrings();
        }
    }

    public DateTime EndDate
    {
        get => _endDate;
        set
        {
            SetProperty(ref _endDate, value);
            UpdateDateStrings();
        }
    }

    public string StartDateString
    {
        get => _startDateString;
        set
        {
            SetProperty(ref _startDateString, value);
            if (DateTime.TryParseExact(value, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
            {
                _startDate = parsedDate;
            }
        }
    }

    public string EndDateString
    {
        get => _endDateString;
        set
        {
            SetProperty(ref _endDateString, value);
            if (DateTime.TryParseExact(value, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
            {
                _endDate = parsedDate;
            }
        }
    }

    // Свойства для отчета по персоналу
    public DateTime PersonnelStartDate
    {
        get => _personnelStartDate;
        set
        {
            SetProperty(ref _personnelStartDate, value);
            UpdatePersonnelDateStrings();
        }
    }

    public DateTime PersonnelEndDate
    {
        get => _personnelEndDate;
        set
        {
            SetProperty(ref _personnelEndDate, value);
            UpdatePersonnelDateStrings();
        }
    }

    public string PersonnelStartDateString
    {
        get => _personnelStartDateString;
        set
        {
            SetProperty(ref _personnelStartDateString, value);
            if (DateTime.TryParseExact(value, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
            {
                _personnelStartDate = parsedDate;
            }
        }
    }

    public string PersonnelEndDateString
    {
        get => _personnelEndDateString;
        set
        {
            SetProperty(ref _personnelEndDateString, value);
            if (DateTime.TryParseExact(value, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
            {
                _personnelEndDate = parsedDate;
            }
        }
    }

    public ObservableCollection<EmployeeForReport> AllEmployees
    {
        get => _allEmployees;
        set => SetProperty(ref _allEmployees, value);
    }

    public ObservableCollection<EmployeeForReport> SelectedEmployees
    {
        get => _selectedEmployees;
        set => SetProperty(ref _selectedEmployees, value);
    }

    public ObservableCollection<string> AvailableColumns
    {
        get => _availableColumns;
        set => SetProperty(ref _availableColumns, value);
    }

    public ObservableCollection<string> SelectedColumns
    {
        get => _selectedColumns;
        set => SetProperty(ref _selectedColumns, value);
    }

    private EmployeeForReport? _selectedEmployeeFromList;
    public EmployeeForReport? SelectedEmployeeFromList
    {
        get => _selectedEmployeeFromList;
        set
        {
            SetProperty(ref _selectedEmployeeFromList, value);
            (AddEmployeeToReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private EmployeeForReport? _selectedEmployeeFromSelected;
    public EmployeeForReport? SelectedEmployeeFromSelected
    {
        get => _selectedEmployeeFromSelected;
        set
        {
            SetProperty(ref _selectedEmployeeFromSelected, value);
            (RemoveEmployeeFromReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private string? _selectedAvailableColumn;
    public string? SelectedAvailableColumn
    {
        get => _selectedAvailableColumn;
        set
        {
            SetProperty(ref _selectedAvailableColumn, value);
            (AddColumnToReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private string? _selectedSelectedColumn;
    public string? SelectedSelectedColumn
    {
        get => _selectedSelectedColumn;
        set
        {
            SetProperty(ref _selectedSelectedColumn, value);
            (RemoveColumnFromReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public ObservableCollection<SensorForDropdown> Sensors
    {
        get => _sensors;
        set => SetProperty(ref _sensors, value);
    }

    public SensorForDropdown? SelectedSensor
    {
        get => _selectedSensor;
        set
        {
            SetProperty(ref _selectedSensor, value);
            (GenerateEquipmentReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (GenerateStatisticsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public ICommand GenerateEquipmentReportCommand { get; }
    public ICommand GenerateStatisticsCommand { get; }
    public ICommand ExportReportCommand { get; }
    public ICommand GenerateComprehensiveEquipmentReportCommand { get; }
    public ICommand GenerateComprehensiveProblemsReportCommand { get; }
    public ICommand GeneratePersonnelReportCommand { get; }
    public ICommand AddEmployeeToReportCommand { get; }
    public ICommand RemoveEmployeeFromReportCommand { get; }
    public ICommand AddColumnToReportCommand { get; }
    public ICommand RemoveColumnFromReportCommand { get; }
    public ICommand SelectAllEmployeesCommand { get; }
    public ICommand ClearEmployeesCommand { get; }
    public ICommand SelectAllColumnsCommand { get; }
    public ICommand ClearColumnsCommand { get; }

    private void UpdateDateStrings()
    {
        _startDateString = _startDate.ToString("dd.MM.yyyy");
        _endDateString = _endDate.ToString("dd.MM.yyyy");
        OnPropertyChanged(nameof(StartDateString));
        OnPropertyChanged(nameof(EndDateString));
    }

    private void UpdatePersonnelDateStrings()
    {
        _personnelStartDateString = _personnelStartDate.ToString("dd.MM.yyyy");
        _personnelEndDateString = _personnelEndDate.ToString("dd.MM.yyyy");
        OnPropertyChanged(nameof(PersonnelStartDateString));
        OnPropertyChanged(nameof(PersonnelEndDateString));
    }

    private async Task LoadSensors()
    {
        var sensors = await _db.GetSensorsForDropdownAsync();
        Sensors.Clear();
        foreach (var s in sensors)
            Sensors.Add(s);
    }

    private async Task LoadEmployees()
    {
        var employees = await _db.GetEmployeesAsync();
        AllEmployees.Clear();
        foreach (var emp in employees.Where(e => e.IsActive))
        {
            AllEmployees.Add(new EmployeeForReport
            {
                Id = emp.Id,
                FullName = emp.FullName,
                Position = emp.Position,
                Department = emp.Department,
                HireDate = emp.HireDate,
                Phone = emp.Phone,
                Role = emp.Role,
                Experience = CalculateExperience(emp.HireDate)
            });
        }
    }

    private string CalculateExperience(DateTime hireDate)
    {
        var today = DateTime.Today;
        int years = today.Year - hireDate.Year;
        if (hireDate.Date > today.AddYears(-years)) years--;

        int months = today.Month - hireDate.Month;
        if (months < 0)
        {
            months += 12;
            years--;
        }

        if (years > 0)
            return $"{years} г. {months} мес.";
        else
            return $"{months} мес.";
    }

    private void AddEmployeeToReport()
    {
        if (SelectedEmployeeFromList != null && !_selectedEmployees.Contains(SelectedEmployeeFromList))
        {
            _selectedEmployees.Add(SelectedEmployeeFromList);
            OnPropertyChanged(nameof(SelectedEmployees));
            (GeneratePersonnelReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private void RemoveEmployeeFromReport()
    {
        if (SelectedEmployeeFromSelected != null)
        {
            _selectedEmployees.Remove(SelectedEmployeeFromSelected);
            OnPropertyChanged(nameof(SelectedEmployees));
            (GeneratePersonnelReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private void AddColumnToReport()
    {
        if (SelectedAvailableColumn != null && !_selectedColumns.Contains(SelectedAvailableColumn))
        {
            _selectedColumns.Add(SelectedAvailableColumn);
            OnPropertyChanged(nameof(SelectedColumns));
            (GeneratePersonnelReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private void RemoveColumnFromReport()
    {
        if (SelectedSelectedColumn != null)
        {
            _selectedColumns.Remove(SelectedSelectedColumn);
            OnPropertyChanged(nameof(SelectedColumns));
            (GeneratePersonnelReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private void SelectAllEmployees()
    {
        _selectedEmployees.Clear();
        foreach (var emp in _allEmployees)
            _selectedEmployees.Add(emp);
        OnPropertyChanged(nameof(SelectedEmployees));
        (GeneratePersonnelReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void ClearEmployees()
    {
        _selectedEmployees.Clear();
        OnPropertyChanged(nameof(SelectedEmployees));
        (GeneratePersonnelReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void SelectAllColumns()
    {
        _selectedColumns.Clear();
        foreach (var col in _availableColumns)
            _selectedColumns.Add(col);
        OnPropertyChanged(nameof(SelectedColumns));
        (GeneratePersonnelReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void ClearColumns()
    {
        _selectedColumns.Clear();
        OnPropertyChanged(nameof(SelectedColumns));
        (GeneratePersonnelReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private async Task GenerateEquipmentReportAsync()
    {
        if (SelectedSensor == null) return;

        IsLoading = true;
        ReportResult = "Генерация отчёта...";
        _currentReportTitle = $"Отчёт_по_оборудованию_{SelectedSensor.Name}";

        try
        {
            var equipment = await _db.GetEquipmentReportByIdAsync(SelectedSensor.Id);

            if (equipment == null)
            {
                ReportResult = "Оборудование не найдено";
                _currentReportText = "";
                return;
            }

            var report = $"Модель: {equipment.Model}\n";
            report += $"Производитель: {equipment.Manufacturer}\n";
            if (!string.IsNullOrEmpty(equipment.ManufacturerCountry))
                report += $"Страна производитель: {equipment.ManufacturerCountry}\n";
            report += $"Тип: {equipment.Type}\n";
            report += $"Точка контроля: {equipment.ControlPoint ?? "Не указана"}\n";
            report += $"Место установки: {equipment.InstallationLocation}\n";
            report += $"Трубопровод: {equipment.PipelineName}\n";
            if (equipment.ProductionYear.HasValue && equipment.ProductionYear.Value > 0)
                report += $"Год выпуска: {equipment.ProductionYear.Value}\n";
            if (equipment.LastCalibration.HasValue)
                report += $"Дата последней поверки: {equipment.LastCalibration.Value:dd.MM.yyyy}\n";
            report += $"\nДиапазон измерений:\n";
            report += $"  Минимальное значение: {equipment.MinThreshold} {equipment.Unit}\n";
            report += $"  Максимальное значение: {equipment.MaxThreshold} {equipment.Unit}\n";
            report += $"\nПоследние показания:\n";
            if (equipment.LastMeasurementValue.HasValue && equipment.LastMeasurementDate.HasValue)
            {
                report += $"  Значение: {equipment.LastMeasurementValue.Value:F2} {equipment.Unit}\n";
                report += $"  Дата: {equipment.LastMeasurementDate.Value:dd.MM.yyyy HH:mm}\n";
                if (equipment.LastMeasurementValue.Value > equipment.MaxThreshold)
                    report += $"  Статус: ПРЕВЫШЕНИЕ ДОПУСТИМОГО ЗНАЧЕНИЯ\n";
                else if (equipment.LastMeasurementValue.Value < equipment.MinThreshold)
                    report += $"  Статус: НИЖЕ ДОПУСТИМОГО ЗНАЧЕНИЯ\n";
                else
                    report += $"  Статус: В НОРМЕ\n";
            }
            else
            {
                report += $"  Нет данных о замерах\n";
            }
            report += $"\nПоследние ремонты:\n";
            report += $"  {equipment.LastRepairDescription}\n";
            report += $"\nОтчёт сформирован: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";

            _currentReportText = report;
            ReportResult = report;
        }
        catch (Exception ex)
        {
            ReportResult = $"Ошибка: {ex.Message}";
            _currentReportText = "";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task GenerateStatisticsAsync()
    {
        if (SelectedSensor == null)
        {
            var box = MessageBoxManager.GetMessageBoxStandard("Внимание",
                "Для расчёта статистики необходимо выбрать оборудование",
                ButtonEnum.Ok);
            await box.ShowAsync();
            return;
        }

        IsLoading = true;
        ReportResult = "Расчёт статистики...";
        _currentReportTitle = $"Статистика_по_оборудованию_{SelectedSensor.Name}_{StartDate:dd.MM.yyyy}_{EndDate:dd.MM.yyyy}";

        try
        {
            var stats = await _db.GetSensorStatisticsByIdAsync(SelectedSensor.Id, StartDate, EndDate);

            var result = $"Оборудование: {SelectedSensor.Name}\n";
            result += $"Период: {StartDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy}\n\n";
            result += $"Количество замеров: {stats.TotalMeasurements}\n";
            result += $"Количество проблем: {stats.ProblemCount}\n";
            result += $"Среднее значение показаний: {stats.AverageValue:F2}\n\n";

            if (stats.DailyStats.Any())
            {
                result += $"Дневная динамика:\n";
                foreach (var day in stats.DailyStats.Take(14))
                {
                    result += $"{day.Date:dd.MM.yyyy}: ср={day.AverageValue:F2}, замеров={day.MeasurementCount}, проблем={day.ProblemCount}\n";
                }
                if (stats.DailyStats.Count > 14)
                    result += $"... и ещё {stats.DailyStats.Count - 14} дней\n";
            }
            result += $"\nОтчёт сформирован: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";

            _currentReportText = result;
            ReportResult = result;
        }
        catch (Exception ex)
        {
            ReportResult = $"Ошибка: {ex.Message}";
            _currentReportText = "";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task GenerateComprehensiveEquipmentReportAsync()
    {
        IsLoading = true;
        ReportResult = "Генерация сводного отчёта по оборудованию...";
        _currentReportTitle = $"Сводный_отчёт_по_оборудованию_{StartDate:dd.MM.yyyy}_{EndDate:dd.MM.yyyy}";

        try
        {
            var report = await _db.GetComprehensiveReportAsync(StartDate, EndDate);

            var result = $"Период: {StartDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy}\n\n";
            result += $"Общая статистика:\n";
            result += $"  Всего единиц оборудования: {report.TotalSensors}\n";
            result += $"  Всего замеров: {report.TotalMeasurements}\n";
            result += $"  Всего проблем: {report.TotalProblems}\n";
            result += $"  Всего проверок: {report.TotalInspections}\n";
            result += $"  Всего ремонтов: {report.TotalRepairs}\n";
            result += $"  Среднее значение показаний: {report.AverageMeasurementValue:F2}\n\n";

            result += $"Список оборудования:\n";
            int counter = 1;
            foreach (var eq in report.EquipmentList.Take(50))
            {
                result += $"{counter}. {eq.Model} ({eq.Type})\n";
                result += $"     Производитель: {eq.Manufacturer}\n";
                result += $"     Место установки: {eq.InstallationLocation}\n";
                result += $"     Трубопровод: {eq.PipelineName}\n";
                result += $"     Диапазон: {eq.MinThreshold} - {eq.MaxThreshold} {eq.Unit}\n\n";
                counter++;
            }

            if (report.EquipmentList.Count > 50)
            {
                result += $"... и ещё {report.EquipmentList.Count - 50} единиц оборудования\n";
            }
            result += $"\nОтчёт сформирован: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";

            _currentReportText = result;
            ReportResult = result;
        }
        catch (Exception ex)
        {
            ReportResult = $"Ошибка: {ex.Message}";
            _currentReportText = "";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task GenerateComprehensiveProblemsReportAsync()
    {
        IsLoading = true;
        ReportResult = "Генерация сводного отчёта по проблемам...";
        _currentReportTitle = $"Сводный_отчёт_по_проблемам_{StartDate:dd.MM.yyyy}_{EndDate:dd.MM.yyyy}";

        try
        {
            var report = await _db.GetComprehensiveReportAsync(StartDate, EndDate);

            var result = $"Период: {StartDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy}\n\n";
            result += $"Общая статистика по проблемам:\n";
            result += $"  Всего проблем: {report.TotalProblems}\n";
            result += $"  Всего проверок: {report.TotalInspections}\n";
            result += $"  Всего ремонтов: {report.TotalRepairs}\n\n";

            result += $"Распределение по типам проблем:\n";
            foreach (var problem in report.ProblemSummary)
            {
                result += $"  {problem.ProblemType}:\n";
                result += $"     Количество: {problem.Count}\n";
                result += $"     Категория риска: {problem.RiskCategory}\n\n";
            }

            if (report.ProblemSummary.Count == 0)
            {
                result += $"  За указанный период проблем не зарегистрировано\n";
            }
            result += $"\nОтчёт сформирован: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";

            _currentReportText = result;
            ReportResult = result;
        }
        catch (Exception ex)
        {
            ReportResult = $"Ошибка: {ex.Message}";
            _currentReportText = "";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task GeneratePersonnelReportAsync()
    {
        if (_selectedEmployees.Count == 0)
        {
            await ShowWarning("Внимание", "Выберите хотя бы одного сотрудника для отчета");
            return;
        }

        if (_selectedColumns.Count == 0)
        {
            await ShowWarning("Внимание", "Выберите хотя бы одну колонку для отображения");
            return;
        }

        IsLoading = true;
        ReportResult = "Генерация отчета по персоналу...";
        _currentReportTitle = $"Отчет_по_персоналу_{PersonnelStartDate:dd.MM.yyyy}_{PersonnelEndDate:dd.MM.yyyy}";

        try
        {
            var result = $"Период: {PersonnelStartDate:dd.MM.yyyy} - {PersonnelEndDate:dd.MM.yyyy}\n\n";

            result += $"Выбрано сотрудников: {_selectedEmployees.Count}\n";
            

            // Формирование данных по сотрудникам
            foreach (var emp in _selectedEmployees)
            {
                result += $"Сотрудник:\n";
                foreach (var col in _selectedColumns)
                {
                    switch (col)
                    {
                        case "ФИО":
                            result += $"  ФИО: {emp.FullName}\n";
                            break;
                        case "Должность":
                            result += $"  Должность: {emp.Position}\n";
                            break;
                        case "Отдел":
                            result += $"  Отдел: {emp.Department}\n";
                            break;
                        case "Стаж":
                            result += $"  Стаж: {emp.Experience}\n";
                            break;
                        case "Дата приема":
                            result += $"  Дата приема: {emp.HireDate:dd.MM.yyyy}\n";
                            break;
                        case "Контактная информация":
                            result += $"  Контактная информация: {emp.Phone}\n";
                            break;
                        case "Роль в системе":
                            result += $"  Роль в системе: {emp.Role}\n";
                            break;
                    }
                }
                result += "\n";
            }

            result += $"Отчёт сформирован: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";

            _currentReportText = result;
            ReportResult = result;
        }
        catch (Exception ex)
        {
            ReportResult = $"Ошибка: {ex.Message}";
            _currentReportText = "";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExportReportAsync()
    {
        if (string.IsNullOrEmpty(_currentReportText))
        {
            var box = MessageBoxManager.GetMessageBoxStandard("Экспорт отчёта",
                "Нет данных для экспорта. Сначала сформируйте отчёт.",
                ButtonEnum.Ok);
            await box.ShowAsync();
            return;
        }

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await OpenSaveFileDialogAndGenerateReport();
        });
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

            if (storageProvider == null && Avalonia.Application.Current?.ApplicationLifetime is ClassicDesktopStyleApplicationLifetime desktop)
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

            string cleanTitle = _currentReportTitle
                .Replace(" ", "_")
                .Replace("/", "_")
                .Replace("\\", "_")
                .Replace("(", "")
                .Replace(")", "")
                .Replace(":", "")
                .Replace("?", "")
                .Replace("*", "")
                .Replace("\"", "");

            string defaultFileName = $"{cleanTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Сохранить PDF отчет",
                SuggestedFileName = defaultFileName,
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

            string filePath = file.Path.LocalPath;

            try
            {
                await Task.Run(() => GeneratePdfReport(_currentReportText, filePath));

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);

                var successBox = MessageBoxManager.GetMessageBoxStandard(
                    "Успешно",
                    $"PDF отчет успешно сохранен!\n\nПуть: {filePath}",
                    ButtonEnum.Ok,
                    Icon.Success);
                await successBox.ShowAsync();
            }
            catch (Exception ex)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
                await ShowError("Ошибка при сохранении отчета", ex.Message);
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
            await ShowError("Ошибка", ex.Message);
        }
    }

    private void GeneratePdfReport(string reportText, string filePath)
    {
        try
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header()
                        .AlignCenter()
                        .Column(col =>
                        {
                            col.Spacing(5);
                            col.Item().Text(_currentReportTitle.Replace("_", " ")).Bold().FontSize(14);
                            col.Item().PaddingTop(5).LineHorizontal(1);
                        });

                    page.Content()
                        .PaddingTop(10)
                        .Text(text =>
                        {
                            text.Span(reportText).FontSize(9);
                        });

                    page.Footer()
                        .AlignCenter()
                        .Column(col =>
                        {
                            col.Spacing(3);
                            col.Item().PaddingTop(5).LineHorizontal(1);
                            col.Item().Text("Сформировано системой мониторинга Taneco").FontSize(8);
                            col.Item().Text(page);
                        });
                });
            });

            document.GeneratePdf(filePath);
        }
        catch (Exception ex)
        {
            throw;
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

// Вспомогательный класс для отчета по персоналу
public class EmployeeForReport
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Experience { get; set; } = string.Empty;
    public DateTime HireDate { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}