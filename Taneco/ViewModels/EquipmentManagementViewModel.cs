// ViewModels/EquipmentManagementViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Taneco.Models;
using Taneco.Services;

namespace Taneco.ViewModels;

public class EquipmentManagementViewModel : ViewModelBase
{
    private readonly DatabaseService _databaseService;
    private User? _currentUser;
    private ObservableCollection<Equipment> _allEquipmentList = new();
    private ObservableCollection<Equipment> _filteredEquipmentList = new();
    private Equipment? _selectedEquipment;
    private Equipment? _selectedEquipmentDetails;
    private string _searchText = string.Empty;
    private bool _isLoading;
    private bool _showDetailsPanel;
    private int _selectedTabIndex;
    private string _equipmentCountText = "Всего: 0";
    private string _detailsTitle = "Информация";
    private string _emptySelectionText = "Выберите оборудование из списка слева для просмотра детальной информации";
    private string _lastActionType = string.Empty;
    private string _lastActionValue = string.Empty;
    private DateTime? _lastActionDate;
    private string _detailLabel1 = string.Empty;
    private string _detailValue1 = string.Empty;
    private string _detailLabel2 = string.Empty;
    private string _detailValue2 = string.Empty;
    private bool _isSensorSelected;
    private bool _isPipelineSelected;

    public EquipmentManagementViewModel()
    {
        _databaseService = new DatabaseService();
        RefreshDataCommand = new RelayCommand(_ => RefreshData());
    }

    public User? CurrentUser
    {
        get => _currentUser;
        set => SetProperty(ref _currentUser, value);
    }

    public ObservableCollection<Equipment> FilteredEquipmentList
    {
        get => _filteredEquipmentList;
        set => SetProperty(ref _filteredEquipmentList, value);
    }

    public Equipment? SelectedEquipment
    {
        get => _selectedEquipment;
        set
        {
            SetProperty(ref _selectedEquipment, value);
            if (value != null)
            {
                ShowDetailsPanel = true;
                SelectedEquipmentDetails = value;

                // Заполняем основные поля для отображения
                if (value.IsPipeline)
                {
                    IsPipelineSelected = true;
                    IsSensorSelected = false;
                    DetailLabel1 = "Название";
                    DetailValue1 = value.DisplayName;
                    DetailLabel2 = "Протяженность";
                    DetailValue2 = value.DisplayDetail;
                }
                else
                {
                    IsPipelineSelected = false;
                    IsSensorSelected = true;
                    DetailLabel1 = "Модель";
                    DetailValue1 = value.DisplayName;
                    DetailLabel2 = "Точка контроля";
                    DetailValue2 = value.DisplayDetail;
                }

                Task.Run(async () => await LoadLastActionAsync(value.Id, value.IsPipeline));
            }
            else
            {
                ShowDetailsPanel = false;
                SelectedEquipmentDetails = null;
                LastActionType = string.Empty;
                LastActionValue = string.Empty;
                LastActionDate = null;
            }
        }
    }

    public Equipment? SelectedEquipmentDetails
    {
        get => _selectedEquipmentDetails;
        set => SetProperty(ref _selectedEquipmentDetails, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            SetProperty(ref _searchText, value);
            ApplyFilter();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool ShowDetailsPanel
    {
        get => _showDetailsPanel;
        set => SetProperty(ref _showDetailsPanel, value);
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            SetProperty(ref _selectedTabIndex, value);
            Task.Run(async () => await LoadEquipmentAsync());
        }
    }

    public string EquipmentCountText
    {
        get => _equipmentCountText;
        set => SetProperty(ref _equipmentCountText, value);
    }

    public string DetailsTitle
    {
        get => _detailsTitle;
        set => SetProperty(ref _detailsTitle, value);
    }

    public string EmptySelectionText
    {
        get => _emptySelectionText;
        set => SetProperty(ref _emptySelectionText, value);
    }

    public string LastActionType
    {
        get => _lastActionType;
        set => SetProperty(ref _lastActionType, value);
    }

    public string LastActionValue
    {
        get => _lastActionValue;
        set => SetProperty(ref _lastActionValue, value);
    }

    public DateTime? LastActionDate
    {
        get => _lastActionDate;
        set => SetProperty(ref _lastActionDate, value);
    }

    public string DetailLabel1
    {
        get => _detailLabel1;
        set => SetProperty(ref _detailLabel1, value);
    }

    public string DetailValue1
    {
        get => _detailValue1;
        set => SetProperty(ref _detailValue1, value);
    }

    public string DetailLabel2
    {
        get => _detailLabel2;
        set => SetProperty(ref _detailLabel2, value);
    }

    public string DetailValue2
    {
        get => _detailValue2;
        set => SetProperty(ref _detailValue2, value);
    }

    public bool IsSensorSelected
    {
        get => _isSensorSelected;
        set => SetProperty(ref _isSensorSelected, value);
    }

    public bool IsPipelineSelected
    {
        get => _isPipelineSelected;
        set => SetProperty(ref _isPipelineSelected, value);
    }

    public ICommand RefreshDataCommand { get; }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredEquipmentList.Clear();
            foreach (var item in _allEquipmentList)
            {
                FilteredEquipmentList.Add(item);
            }
        }
        else
        {
            var filtered = _allEquipmentList.Where(e =>
                e.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                e.DisplayDetail.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();

            FilteredEquipmentList.Clear();
            foreach (var item in filtered)
            {
                FilteredEquipmentList.Add(item);
            }
        }

        EquipmentCountText = $"Всего: {FilteredEquipmentList.Count}";
    }

    public async Task LoadEquipmentAsync()
    {
        IsLoading = true;
        try
        {
            if (SelectedTabIndex == 1) // Датчики
            {
                DetailsTitle = "Информация о датчике";
                EmptySelectionText = "Выберите датчик из списка слева для просмотра детальной информации";

                var sensors = await _databaseService.GetSensorsAsync();
                _allEquipmentList.Clear();
                foreach (var sensor in sensors)
                {
                    _allEquipmentList.Add(new Equipment
                    {
                        Id = sensor.Id,
                        IsPipeline = false,
                        // Только модель и точка контроля
                        DisplayName = sensor.Model,
                        DisplayDetail = sensor.ControlPoint,
                        // Остальные поля для деталей
                        Model = sensor.Model,
                        ControlPoint = sensor.ControlPoint,
                        Type = sensor.Type,
                        Manufacturer = sensor.Manufacturer,
                        ManufacturerCountry = sensor.ManufacturerCountry,
                        PipelineName = sensor.PipelineName,
                        InstallationLocation = sensor.Location,
                        ProductionYear = sensor.ProductionYear,
                        LastCalibration = sensor.LastCalibration,
                        MinValue = sensor.MinValue,
                        MaxValue = sensor.MaxValue,
                        Unit = sensor.Unit,
                        MeasurementType = sensor.MeasurementType
                    });
                }
            }
            else // Трубопроводы (только название и протяженность)
            {
                DetailsTitle = "Информация о трубопроводе";
                EmptySelectionText = "Выберите трубопровод из списка слева для просмотра детальной информации";

                var pipelines = await _databaseService.GetPipelinesAsync();
                _allEquipmentList.Clear();
                foreach (var pipeline in pipelines)
                {
                    _allEquipmentList.Add(new Equipment
                    {
                        Id = pipeline.Id,
                        IsPipeline = true,
                        // Только название и протяженность
                        DisplayName = pipeline.Name,
                        DisplayDetail = $"{pipeline.Length} м",
                        // Остальные поля для деталей
                        Length = pipeline.Length,
                        Diameter = pipeline.Diameter,
                        InstallationDate = pipeline.InstallationDate,
                        Material = pipeline.Material,
                        PipelineName = pipeline.Name
                    });
                }
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadEquipmentAsync error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadLastActionAsync(int equipmentId, bool isPipeline)
    {
        try
        {
            if (!isPipeline) // Датчики - последний замер
            {
                var measurements = await _databaseService.GetAllMeasurementsAsync();
                var lastMeasurement = measurements
                    .Where(m => m.SensorId == equipmentId)
                    .OrderByDescending(m => m.Date)
                    .ThenByDescending(m => m.Time)
                    .FirstOrDefault();

                if (lastMeasurement != null)
                {
                    LastActionType = "Последний замер";
                    LastActionValue = $"{lastMeasurement.Value} {lastMeasurement.Unit} (Статус: {lastMeasurement.Status})";
                    LastActionDate = lastMeasurement.Date + lastMeasurement.Time;
                }
                else
                {
                    LastActionType = "Последний замер";
                    LastActionValue = "Нет данных";
                    LastActionDate = null;
                }
            }
            else // Трубопроводы - последний ремонт или проверка
            {
                var repairs = await _databaseService.GetRepairsAsync();
                var lastRepair = repairs
                    .Where(r => r.ProblemId == equipmentId)
                    .OrderByDescending(r => r.StartDate)
                    .FirstOrDefault();

                if (lastRepair != null)
                {
                    LastActionType = "Последний ремонт";
                    LastActionValue = $"Статус: {lastRepair.Status}, Бюджет: {lastRepair.Budget:C}";
                    LastActionDate = lastRepair.StartDate;
                }
                else
                {
                    var inspections = await _databaseService.GetAllInspectionsAsync();
                    var lastInspection = inspections
                        .Where(i => i.ProblemId == equipmentId)
                        .OrderByDescending(i => i.StartDate)
                        .FirstOrDefault();

                    if (lastInspection != null)
                    {
                        LastActionType = "Последняя проверка";
                        LastActionValue = $"Статус: {lastInspection.Status}";
                        LastActionDate = lastInspection.StartDate;
                    }
                    else
                    {
                        LastActionType = "Последнее действие";
                        LastActionValue = "Нет данных";
                        LastActionDate = null;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadLastActionAsync error: {ex.Message}");
            LastActionType = "Последнее действие";
            LastActionValue = "Ошибка загрузки данных";
            LastActionDate = null;
        }
    }

    public async void RefreshData()
    {
        await LoadEquipmentAsync();
        SelectedEquipment = null;
    }

    public async Task InitializeAsync(User user)
    {
        CurrentUser = user;
        await LoadEquipmentAsync();
    }
}