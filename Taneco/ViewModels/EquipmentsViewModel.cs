using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Taneco.Models;
using Taneco.Services;

namespace Taneco.ViewModels;

public class EquipmentsViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    
    // Датчики
    private ObservableCollection<Sensor> _allSensors;
    private ObservableCollection<Sensor> _filteredSensors;
    private Sensor? _selectedSensor;
    private string _sensorSearchText = string.Empty;
    
    // Трубопроводы
    private ObservableCollection<Pipeline> _allPipelines;
    private ObservableCollection<Pipeline> _filteredPipelines;
    private Pipeline? _selectedPipeline;
    private string _pipelineSearchText = string.Empty;
    
    private bool _isLoading;
    private User? _currentUser;
    private bool _isEditMode;
    private decimal _editMinValue;
    private decimal _editMaxValue;

    public EquipmentsViewModel()
    {
        _db = new DatabaseService();
        
        _allSensors = new ObservableCollection<Sensor>();
        _filteredSensors = new ObservableCollection<Sensor>();
        _allPipelines = new ObservableCollection<Pipeline>();
        _filteredPipelines = new ObservableCollection<Pipeline>();

        LoadSensorsCommand = new RelayCommand(() => Task.Run(async () => await LoadSensorsAsync()), () => !IsLoading);
        LoadPipelinesCommand = new RelayCommand(() => Task.Run(async () => await LoadPipelinesAsync()), () => !IsLoading);
        SaveThresholdsCommand = new RelayCommand(() => Task.Run(async () => SaveThresholds()), () => SelectedSensor != null && IsEditMode);
        EditCommand = new RelayCommand(() => StartEdit());
        CancelEditCommand = new RelayCommand(() => CancelEdit());
        
        CreateSensorCommand = new RelayCommand(() => OpenCreateSensorDialog(), () => CanCreateEquipment);
        CreatePipelineCommand = new RelayCommand(() => OpenCreatePipelineDialog(), () => CanCreateEquipment);

        Task.Run(async () =>
        {
            await LoadSensorsAsync();
            await LoadPipelinesAsync();
        });
    }

    public User? CurrentUser
    {
        get => _currentUser;
        set => SetProperty(ref _currentUser, value);
    }

    // Датчики
    public ObservableCollection<Sensor> Sensors
    {
        get => _filteredSensors;
        set => SetProperty(ref _filteredSensors, value);
    }

    public Sensor? SelectedSensor
    {
        get => _selectedSensor;
        set 
        { 
            if (SetProperty(ref _selectedSensor, value))
            {
                OnPropertyChanged(nameof(IsSensorSelected));
                if (value != null)
                {
                    EditMinValue = value.MinValue;
                    EditMaxValue = value.MaxValue;
                    IsEditMode = false;
                    LoadLastMeasurementForSensor(value.Id);
                }
            }
        }
    }

    public string SensorSearchText
    {
        get => _sensorSearchText;
        set
        {
            if (SetProperty(ref _sensorSearchText, value))
                FilterSensors();
        }
    }
    
    public bool IsSensorSelected => SelectedSensor != null;

    // Трубопроводы
    public ObservableCollection<Pipeline> Pipelines
    {
        get => _filteredPipelines;
        set => SetProperty(ref _filteredPipelines, value);
    }

    public Pipeline? SelectedPipeline
    {
        get => _selectedPipeline;
        set 
        { 
            if (SetProperty(ref _selectedPipeline, value))
            {
                OnPropertyChanged(nameof(IsPipelineSelected));
                if (value != null)
                {
                    LoadLastRepairForPipeline(value.Id);
                }
            }
        }
    }

    public string PipelineSearchText
    {
        get => _pipelineSearchText;
        set
        {
            if (SetProperty(ref _pipelineSearchText, value))
                FilterPipelines();
        }
    }
    
    public bool IsPipelineSelected => SelectedPipeline != null;

    // Общие
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set => SetProperty(ref _isEditMode, value);
    }

    public decimal EditMinValue
    {
        get => _editMinValue;
        set => SetProperty(ref _editMinValue, value);
    }

    public decimal EditMaxValue
    {
        get => _editMaxValue;
        set => SetProperty(ref _editMaxValue, value);
    }

    public bool CanEdit => CurrentUser?.Role == "Инженер_КИПиА";
    public bool IsEditButtonVisible => !IsEditMode && CanEdit;
    public bool CanCreateEquipment => CurrentUser?.Role == "Инженер_КИПиА";

    public ICommand LoadSensorsCommand { get; }
    public ICommand LoadPipelinesCommand { get; }
    public ICommand SaveThresholdsCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand CreateSensorCommand { get; }
    public ICommand CreatePipelineCommand { get; }

    private async Task LoadSensorsAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var sensors = await _db.GetSensorsAsync();
            _allSensors.Clear();
            foreach (var s in sensors)
                _allSensors.Add(s);
            FilterSensors();
        }
        catch
        {
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadPipelinesAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var pipelines = await _db.GetPipelinesAsync();
            _allPipelines.Clear();
            foreach (var p in pipelines)
                _allPipelines.Add(p);
            FilterPipelines();
        }
        catch
        {
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void FilterSensors()
    {
        Sensors.Clear();
        var filtered = string.IsNullOrWhiteSpace(SensorSearchText)
            ? _allSensors
            : _allSensors.Where(s => s.ControlPoint.ToLower().Contains(SensorSearchText.ToLower()));
        foreach (var sensor in filtered)
            Sensors.Add(sensor);
    }

    private void FilterPipelines()
    {
        Pipelines.Clear();
        var filtered = string.IsNullOrWhiteSpace(PipelineSearchText)
            ? _allPipelines
            : _allPipelines.Where(p => p.Name.ToLower().Contains(PipelineSearchText.ToLower()));
        foreach (var pipeline in filtered)
            Pipelines.Add(pipeline);
    }

    private void StartEdit()
    {
        if (SelectedSensor != null && CanEdit)
            IsEditMode = true;
    }

    private void CancelEdit()
    {
        IsEditMode = false;
        if (SelectedSensor != null)
        {
            EditMinValue = SelectedSensor.MinValue;
            EditMaxValue = SelectedSensor.MaxValue;
        }
    }

    private async Task SaveThresholds()
    {
        if (SelectedSensor == null) return;
        IsLoading = true;
        try
        {
            var result = await _db.UpdateSensorThresholdsAsync(SelectedSensor.Id, EditMinValue, EditMaxValue);
            if (result)
            {
                SelectedSensor.MinValue = EditMinValue;
                SelectedSensor.MaxValue = EditMaxValue;
                IsEditMode = false;
                await LoadSensorsAsync();
            }
        }
        catch
        {
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async void LoadLastMeasurementForSensor(int sensorId)
    {
        var measurements = await _db.GetAllMeasurementsAsync();
        var lastMeasurement = measurements.Where(m => m.SensorId == sensorId).OrderByDescending(m => m.Date).FirstOrDefault();
        
        if (SelectedSensor != null)
        {
            SelectedSensor.LastMeasurementValue = lastMeasurement?.Value.ToString() ?? "Нет данных";
            SelectedSensor.LastMeasurementDate = lastMeasurement?.Date.ToString("dd.MM.yyyy HH:mm") ?? "Нет данных";
            OnPropertyChanged(nameof(SelectedSensor));
        }
    }

    private async void LoadLastRepairForPipeline(int pipelineId)
    {
        var repairs = await _db.GetRepairsAsync();
        var lastRepair = repairs.Where(r => r.ProblemId == pipelineId).OrderByDescending(r => r.EndDate).FirstOrDefault();
        
        if (SelectedPipeline != null)
        {
            SelectedPipeline.LastRepairDate = lastRepair?.EndDate?.ToString("dd.MM.yyyy") ?? "Нет ремонтов";
            SelectedPipeline.LastRepairStatus = lastRepair?.Status ?? "Нет ремонтов";
            OnPropertyChanged(nameof(SelectedPipeline));
        }
    }

    private async void OpenCreateSensorDialog()
    {
        var dialog = new Views.CreateSensorWindow();
        var viewModel = new CreateSensorViewModel(_db);
        dialog.DataContext = viewModel;
        var mainWindow = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
        await dialog.ShowDialog(mainWindow);
        await LoadSensorsAsync();
    }

    private async void OpenCreatePipelineDialog()
    {
        var dialog = new Views.CreatePipelineWindow();
        var viewModel = new CreatePipelineViewModel(_db);
        dialog.DataContext = viewModel;
        var mainWindow = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
        await dialog.ShowDialog(mainWindow);
        await LoadPipelinesAsync();
    }
}