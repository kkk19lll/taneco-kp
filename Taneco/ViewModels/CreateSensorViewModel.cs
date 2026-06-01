using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Taneco.Models;
using Taneco.Services;

namespace Taneco.ViewModels;

public class CreateSensorViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    
    private string _controlPoint = string.Empty;
    private string _model = string.Empty;
    private string _manufacturer = string.Empty;
    private string _country = string.Empty;
    private string _selectedSensorType = string.Empty;
    private string _selectedMeasurementType = string.Empty;
    private string _selectedUnit = string.Empty;
    private string _minValue = string.Empty;
    private string _maxValue = string.Empty;
    private Pipeline? _selectedPipeline;
    private string _location = string.Empty;
    private string _productionYear = string.Empty;
    
    private bool _controlPointError;
    private bool _modelError;
    private bool _manufacturerError;

    public ObservableCollection<string> SensorTypes { get; } = new();
    public ObservableCollection<string> MeasurementTypes { get; } = new();
    public ObservableCollection<string> Units { get; } = new();
    public ObservableCollection<Pipeline> Pipelines { get; } = new();

    public CreateSensorViewModel(DatabaseService db)
    {
        _db = db;
        
        CreateCommand = new RelayCommand(() => Task.Run(async () => await CreateSensor()), () => CanCreate);
        CloseCommand = new RelayCommand(Close);
        
        Task.Run(async () => await LoadData());
    }

    public string ControlPoint
    {
        get => _controlPoint;
        set 
        { 
            if (SetProperty(ref _controlPoint, value))
            {
                ControlPointError = string.IsNullOrWhiteSpace(value);
                ((RelayCommand)CreateCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string Model
    {
        get => _model;
        set 
        { 
            if (SetProperty(ref _model, value))
            {
                ModelError = string.IsNullOrWhiteSpace(value);
                ((RelayCommand)CreateCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string Manufacturer
    {
        get => _manufacturer;
        set 
        { 
            if (SetProperty(ref _manufacturer, value))
            {
                ManufacturerError = string.IsNullOrWhiteSpace(value);
                ((RelayCommand)CreateCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string Country
    {
        get => _country;
        set => SetProperty(ref _country, value);
    }

    public string SelectedSensorType
    {
        get => _selectedSensorType;
        set => SetProperty(ref _selectedSensorType, value);
    }

    public string SelectedMeasurementType
    {
        get => _selectedMeasurementType;
        set => SetProperty(ref _selectedMeasurementType, value);
    }

    public string SelectedUnit
    {
        get => _selectedUnit;
        set => SetProperty(ref _selectedUnit, value);
    }

    public string MinValue
    {
        get => _minValue;
        set => SetProperty(ref _minValue, value);
    }

    public string MaxValue
    {
        get => _maxValue;
        set => SetProperty(ref _maxValue, value);
    }

    public Pipeline? SelectedPipeline
    {
        get => _selectedPipeline;
        set => SetProperty(ref _selectedPipeline, value);
    }

    public string Location
    {
        get => _location;
        set => SetProperty(ref _location, value);
    }

    public string ProductionYear
    {
        get => _productionYear;
        set => SetProperty(ref _productionYear, value);
    }

    public bool ControlPointError
    {
        get => _controlPointError;
        set => SetProperty(ref _controlPointError, value);
    }

    public bool ModelError
    {
        get => _modelError;
        set => SetProperty(ref _modelError, value);
    }

    public bool ManufacturerError
    {
        get => _manufacturerError;
        set => SetProperty(ref _manufacturerError, value);
    }

    public bool CanCreate => !string.IsNullOrWhiteSpace(ControlPoint) && 
                             !string.IsNullOrWhiteSpace(Model) && 
                             !string.IsNullOrWhiteSpace(Manufacturer);

    public ICommand CreateCommand { get; }
    public ICommand CloseCommand { get; }

    private async Task LoadData()
    {
        // Загружаем типы датчиков
        var sensorTypes = await _db.GetSensorTypesAsync();
        SensorTypes.Clear();
        SensorTypes.Add("Интеллектуальный");
        SensorTypes.Add("Пассивный");
        SensorTypes.Add("Бесконтактный");
        SensorTypes.Add("Механический");
        
        // Загружаем типы измерений
        var measurementTypes = await _db.GetMeasurementTypesAsync();
        MeasurementTypes.Clear();
        foreach (var m in measurementTypes)
            MeasurementTypes.Add(m);
        
        // Загружаем единицы измерения
        var units = await _db.GetUnitsAsync();
        Units.Clear();
        foreach (var u in units)
            Units.Add(u);
        
        // Загружаем трубопроводы
        var pipelines = await _db.GetPipelinesAsync();
        Pipelines.Clear();
        foreach (var p in pipelines)
            Pipelines.Add(p);
    }

    private async Task CreateSensor()
    {
        var success = await _db.CreateSensorAsync(
            ControlPoint, Model, Manufacturer, Country,
            SelectedSensorType, SelectedMeasurementType, SelectedUnit,
            MinValue, MaxValue, SelectedPipeline?.Id ?? 0, Location, ProductionYear);
        
        if (success)
        {
            Close();
        }
    }

    private void Close()
    {
        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.Windows.OfType<Views.CreateSensorWindow>().FirstOrDefault();
            window?.Close();
        }
    }
}