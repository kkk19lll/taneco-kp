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

public class EquipmentEditViewModel : ViewModelBase
{
    private readonly DatabaseService _databaseService;
    private readonly EquipmentEditWindow _window;
    private readonly User? _currentUser;
    private bool _isCreateMode;
    private int _editingEquipmentId;
    private bool _isPipeline;

    private string _windowTitle = string.Empty;
    private string _modalButtonText = string.Empty;
    private string _selectedEquipmentType = "Датчик";
    private ObservableCollection<string> _equipmentTypes = new() { "Датчик", "Трубопровод" };

    private string _formName = string.Empty;
    private string _formModel = string.Empty;
    private string _formManufacturer = string.Empty;
    private string _formManufacturerCountry = string.Empty;
    private string _formSensorType = string.Empty;
    private string _formMeasurementType = string.Empty;
    private string _formUnit = string.Empty;
    private string _formMinValue = string.Empty;
    private string _formMaxValue = string.Empty;
    private string _formControlPoint = string.Empty;
    private string _formPipelineName = string.Empty;
    private string _formLocation = string.Empty;
    private string _formProductionYear = string.Empty;
    private string _formMaterial = string.Empty;
    private string _formLength = string.Empty;
    private string _formDiameter = string.Empty;
    private DateTime? _formInstallationDate = DateTime.Today;
    private string _formInstallationDateString = string.Empty;

    private ObservableCollection<string> _sensorTypes = new();
    private ObservableCollection<string> _measurementTypes = new();
    private ObservableCollection<string> _units = new();
    private ObservableCollection<string> _pipelinesForDropdown = new();
    private ObservableCollection<string> _materialsForDropdown = new();

    private Equipment? _pendingEquipmentToEdit;

    public EquipmentEditViewModel(EquipmentEditWindow window, bool isCreateMode, Equipment? equipmentToEdit = null, User? currentUser = null)
    {
        _databaseService = new DatabaseService();
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _isCreateMode = isCreateMode;
        _currentUser = currentUser;
        _pendingEquipmentToEdit = equipmentToEdit;

        if (!CanUserManageEquipment())
        {
            Task.Run(async () => await ShowAccessDeniedAndClose());
            return;
        }

        CancelCommand = new RelayCommand(_ => Close(false));
        SaveCommand = new RelayCommand(_ => SaveAsync());

        if (!isCreateMode && equipmentToEdit != null)
        {
            LoadForCreate();
        }
        else
        {
            LoadForCreate();
        }

        Task.Run(async () => await LoadDropdownDataAsync());
    }

    private bool CanUserManageEquipment()
    {
        return _currentUser != null && _currentUser.Role == "Инженер_КИПиА";
    }

    private async Task ShowAccessDeniedAndClose()
    {
        var dialog = new Window
        {
            Title = "Доступ запрещен",
            Width = 350,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 15,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Редактирование и добавление оборудования доступно только инженерам КИПиА!",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        MaxWidth = 300
                    },
                    new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Width = 80
                    }
                }
            }
        };

        var button = (Button)((StackPanel)dialog.Content).Children[1];
        button.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(_window);
        Close(false);
    }

    public bool IsCreateMode => _isCreateMode;
    public bool ShowSensorFields => SelectedEquipmentType == "Датчик";
    public bool ShowPipelineFields => SelectedEquipmentType == "Трубопровод";

    public string WindowTitle
    {
        get => _windowTitle;
        set => SetProperty(ref _windowTitle, value);
    }

    public string ModalButtonText
    {
        get => _modalButtonText;
        set => SetProperty(ref _modalButtonText, value);
    }

    public string SelectedEquipmentType
    {
        get => _selectedEquipmentType;
        set
        {
            if (SetProperty(ref _selectedEquipmentType, value))
            {
                OnPropertyChanged(nameof(ShowSensorFields));
                OnPropertyChanged(nameof(ShowPipelineFields));
            }
        }
    }

    public ObservableCollection<string> EquipmentTypes => _equipmentTypes;

    public string FormName
    {
        get => _formName;
        set => SetProperty(ref _formName, value);
    }

    public string FormModel
    {
        get => _formModel;
        set => SetProperty(ref _formModel, value);
    }

    public string FormManufacturer
    {
        get => _formManufacturer;
        set => SetProperty(ref _formManufacturer, value);
    }

    public string FormManufacturerCountry
    {
        get => _formManufacturerCountry;
        set => SetProperty(ref _formManufacturerCountry, value);
    }

    public string FormSensorType
    {
        get => _formSensorType;
        set => SetProperty(ref _formSensorType, value);
    }

    public string FormMeasurementType
    {
        get => _formMeasurementType;
        set => SetProperty(ref _formMeasurementType, value);
    }

    public string FormUnit
    {
        get => _formUnit;
        set => SetProperty(ref _formUnit, value);
    }

    public string FormMinValue
    {
        get => _formMinValue;
        set => SetProperty(ref _formMinValue, value);
    }

    public string FormMaxValue
    {
        get => _formMaxValue;
        set => SetProperty(ref _formMaxValue, value);
    }

    public string FormControlPoint
    {
        get => _formControlPoint;
        set => SetProperty(ref _formControlPoint, value);
    }

    public string FormPipelineName
    {
        get => _formPipelineName;
        set => SetProperty(ref _formPipelineName, value);
    }

    public string FormLocation
    {
        get => _formLocation;
        set => SetProperty(ref _formLocation, value);
    }

    public string FormProductionYear
    {
        get => _formProductionYear;
        set => SetProperty(ref _formProductionYear, value);
    }

    public string FormMaterial
    {
        get => _formMaterial;
        set => SetProperty(ref _formMaterial, value);
    }

    public string FormLength
    {
        get => _formLength;
        set => SetProperty(ref _formLength, value);
    }

    public string FormDiameter
    {
        get => _formDiameter;
        set => SetProperty(ref _formDiameter, value);
    }

    public DateTime? FormInstallationDate
    {
        get => _formInstallationDate;
        set
        {
            if (SetProperty(ref _formInstallationDate, value))
            {
                if (value.HasValue)
                {
                    FormInstallationDateString = value.Value.ToString("dd.MM.yyyy");
                }
                else
                {
                    FormInstallationDateString = string.Empty;
                }
            }
        }
    }

    public string FormInstallationDateString
    {
        get => _formInstallationDateString;
        set
        {
            if (SetProperty(ref _formInstallationDateString, value))
            {
                if (DateTime.TryParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                {
                    _formInstallationDate = parsedDate;
                    OnPropertyChanged(nameof(FormInstallationDate));
                }
                else if (string.IsNullOrWhiteSpace(value))
                {
                    _formInstallationDate = null;
                    OnPropertyChanged(nameof(FormInstallationDate));
                }
            }
        }
    }

    public ObservableCollection<string> SensorTypes
    {
        get => _sensorTypes;
        set => SetProperty(ref _sensorTypes, value);
    }

    public ObservableCollection<string> MeasurementTypes
    {
        get => _measurementTypes;
        set => SetProperty(ref _measurementTypes, value);
    }

    public ObservableCollection<string> Units
    {
        get => _units;
        set => SetProperty(ref _units, value);
    }

    public ObservableCollection<string> PipelinesForDropdown
    {
        get => _pipelinesForDropdown;
        set => SetProperty(ref _pipelinesForDropdown, value);
    }

    public ObservableCollection<string> MaterialsForDropdown
    {
        get => _materialsForDropdown;
        set => SetProperty(ref _materialsForDropdown, value);
    }

    public ICommand CancelCommand { get; }
    public ICommand SaveCommand { get; }

    private void LoadEquipmentForEdit(Equipment equipment)
    {
        _editingEquipmentId = equipment.Id;
        _isPipeline = equipment.IsPipeline;

        Console.WriteLine($"=== LoadEquipmentForEdit ===");
        Console.WriteLine($"IsPipeline: {equipment.IsPipeline}");
        Console.WriteLine($"DisplayName: {equipment.DisplayName}");

        if (equipment.IsPipeline)
        {
            SelectedEquipmentType = "Трубопровод";
            FormName = equipment.DisplayName ?? string.Empty;
            FormMaterial = equipment.Material ?? string.Empty;
            FormLength = equipment.Length.ToString();
            FormDiameter = equipment.Diameter.ToString();

            Console.WriteLine($"InstallationDate из equipment: {equipment.InstallationDate}");

            // Устанавливаем дату
            if (equipment.InstallationDate != DateTime.MinValue)
            {
                _formInstallationDate = equipment.InstallationDate;
                FormInstallationDateString = equipment.InstallationDate.ToString("dd.MM.yyyy");
                Console.WriteLine($"Установлена дата: {FormInstallationDateString}");
            }
            else
            {
                _formInstallationDate = DateTime.Today;
                FormInstallationDateString = DateTime.Today.ToString("dd.MM.yyyy");
                Console.WriteLine($"Дата не найдена, установлена сегодня: {FormInstallationDateString}");
            }

            WindowTitle = "Редактирование трубопровода";
            ModalButtonText = "Сохранить";

            // Принудительно обновляем PropertyChanged
            OnPropertyChanged(nameof(FormInstallationDate));
            OnPropertyChanged(nameof(FormInstallationDateString));
        }
        else
        {
            SelectedEquipmentType = "Датчик";
            FormName = equipment.DisplayName ?? string.Empty;
            FormModel = equipment.Model ?? string.Empty;
            FormManufacturer = equipment.Manufacturer ?? string.Empty;
            FormManufacturerCountry = equipment.ManufacturerCountry ?? string.Empty;
            FormSensorType = equipment.Type ?? string.Empty;
            FormMeasurementType = equipment.MeasurementType ?? string.Empty;
            FormUnit = equipment.Unit ?? string.Empty;
            FormMinValue = equipment.MinValue.ToString();
            FormMaxValue = equipment.MaxValue.ToString();
            FormControlPoint = equipment.ControlPoint ?? string.Empty;
            FormPipelineName = equipment.PipelineName ?? string.Empty;
            FormLocation = equipment.InstallationLocation ?? string.Empty;
            FormProductionYear = equipment.ProductionYear?.ToString() ?? string.Empty;
            WindowTitle = "Редактирование датчика";
            ModalButtonText = "Сохранить";
        }
    }

    private void LoadForCreate()
    {
        if (_isCreateMode)
        {
            WindowTitle = "Создание оборудования";
            ModalButtonText = "Создать";
            FormInstallationDateString = DateTime.Today.ToString("dd.MM.yyyy");
        }
    }

    private async Task LoadDropdownDataAsync()
    {
        try
        {
            var sensorTypes = await _databaseService.GetSensorTypesAsync();
            SensorTypes.Clear();
            foreach (var type in sensorTypes)
                SensorTypes.Add(type);

            var measurementTypes = await _databaseService.GetMeasurementTypesAsync();
            MeasurementTypes.Clear();
            foreach (var type in measurementTypes)
                MeasurementTypes.Add(type);

            var units = await _databaseService.GetUnitsAsync();
            Units.Clear();
            foreach (var unit in units)
                Units.Add(unit);

            var pipelines = await _databaseService.GetPipelinesAsync();
            PipelinesForDropdown.Clear();
            PipelinesForDropdown.Add("(Не установлен)");
            foreach (var pipeline in pipelines)
            {
                PipelinesForDropdown.Add(pipeline.Name);
                Console.WriteLine($"Добавлен трубопровод в выпадающий список: {pipeline.Name}");
            }

            var materials = await _databaseService.GetMaterialsAsync();
            MaterialsForDropdown.Clear();
            foreach (var material in materials)
            {
                MaterialsForDropdown.Add(material);
                Console.WriteLine($"Добавлен материал в выпадающий список: {material}");
            }

            if (!_isCreateMode && _pendingEquipmentToEdit != null)
            {
                await Task.Delay(50);
                LoadEquipmentForEdit(_pendingEquipmentToEdit);
                _pendingEquipmentToEdit = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadDropdownDataAsync error: {ex.Message}");
        }
    }

    private async void SaveAsync()
    {
        try
        {
            Console.WriteLine("SaveAsync вызван!");

            if (string.IsNullOrWhiteSpace(FormName))
            {
                await ShowErrorDialog("Ошибка", "Заполните наименование оборудования!");
                return;
            }

            if (SelectedEquipmentType == "Датчик")
            {
                if (string.IsNullOrWhiteSpace(FormModel))
                {
                    await ShowErrorDialog("Ошибка", "Заполните модель датчика!");
                    return;
                }
                if (string.IsNullOrWhiteSpace(FormManufacturer))
                {
                    await ShowErrorDialog("Ошибка", "Заполните производителя датчика!");
                    return;
                }

                if (string.IsNullOrWhiteSpace(FormMinValue))
                {
                    FormMinValue = "0";
                }

                if (string.IsNullOrWhiteSpace(FormMaxValue))
                {
                    FormMaxValue = "100";
                }

                decimal minValue;
                decimal maxValue;

                string minValueStr = FormMinValue.Trim().Replace(',', '.');
                if (!decimal.TryParse(minValueStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out minValue))
                {
                    await ShowErrorDialog("Ошибка", $"Некорректное минимальное значение: '{FormMinValue}'. Введите число (например: 10 или 10.5)");
                    return;
                }

                string maxValueStr = FormMaxValue.Trim().Replace(',', '.');
                if (!decimal.TryParse(maxValueStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out maxValue))
                {
                    await ShowErrorDialog("Ошибка", $"Некорректное максимальное значение: '{FormMaxValue}'. Введите число (например: 100 или 100.5)");
                    return;
                }

                if (minValue >= maxValue)
                {
                    await ShowErrorDialog("Ошибка", "Минимальное значение должно быть меньше максимального!");
                    return;
                }
            }

            if (SelectedEquipmentType == "Трубопровод")
            {
                if (string.IsNullOrWhiteSpace(FormMaterial))
                {
                    await ShowErrorDialog("Ошибка", "Выберите материал трубопровода!");
                    return;
                }
                if (string.IsNullOrWhiteSpace(FormLength))
                {
                    await ShowErrorDialog("Ошибка", "Заполните протяженность трубопровода!");
                    return;
                }
                if (string.IsNullOrWhiteSpace(FormDiameter))
                {
                    await ShowErrorDialog("Ошибка", "Заполните диаметр трубопровода!");
                    return;
                }

                if (!decimal.TryParse(FormLength.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal lengthValue) || lengthValue <= 0)
                {
                    await ShowErrorDialog("Ошибка", "Введите корректную протяженность (положительное число)!");
                    return;
                }

                if (!decimal.TryParse(FormDiameter.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal diameterValue) || diameterValue <= 0)
                {
                    await ShowErrorDialog("Ошибка", "Введите корректный диаметр (положительное число)!");
                    return;
                }

                if (string.IsNullOrWhiteSpace(FormInstallationDateString) ||
                    !DateTime.TryParseExact(FormInstallationDateString, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    await ShowErrorDialog("Ошибка", "Введите корректную дату установки в формате ДД.ММ.ГГГГ!");
                    return;
                }
            }

            bool success = false;

            if (!_isCreateMode)
            {
                if (_isPipeline)
                {
                    success = await _databaseService.UpdatePipelineAsync(
                        _editingEquipmentId,
                        FormName,
                        FormMaterial ?? "",
                        FormLength ?? "0",
                        FormDiameter ?? "0",
                        FormInstallationDate ?? DateTime.Today);
                }
                else
                {
                    success = await _databaseService.UpdateSensorAsync(
                        _editingEquipmentId,
                        FormControlPoint ?? "",
                        FormModel ?? "",
                        FormManufacturer ?? "",
                        FormManufacturerCountry ?? "",
                        FormSensorType ?? "",
                        FormMeasurementType ?? "",
                        FormUnit ?? "",
                        FormMinValue ?? "0",
                        FormMaxValue ?? "0",
                        FormControlPoint ?? "",
                        FormPipelineName ?? "",
                        FormLocation ?? "",
                        FormProductionYear ?? "");
                }
            }
            else
            {
                if (SelectedEquipmentType == "Трубопровод")
                {
                    success = await _databaseService.CreatePipelineAsync(
                        FormName,
                        FormMaterial ?? "",
                        FormLength ?? "0",
                        FormDiameter ?? "0",
                        FormInstallationDate ?? DateTime.Today);
                }
                else
                {
                    int pipelineId = 0;
                    if (!string.IsNullOrEmpty(FormPipelineName) && FormPipelineName != "(Не установлен)")
                    {
                        var pipelines = await _databaseService.GetPipelinesAsync();
                        var pipeline = pipelines.FirstOrDefault(p => p.Name == FormPipelineName);
                        if (pipeline != null)
                            pipelineId = pipeline.Id;
                    }

                    string location = string.IsNullOrEmpty(FormLocation) ? "Не указано" : FormLocation;

                    success = await _databaseService.CreateSensorAsync(
                        FormControlPoint ?? "",
                        FormModel ?? "",
                        FormManufacturer ?? "",
                        FormManufacturerCountry ?? "",
                        FormSensorType ?? "",
                        FormMeasurementType ?? "",
                        FormUnit ?? "",
                        FormMinValue ?? "0",
                        FormMaxValue ?? "0",
                        pipelineId,
                        location,
                        FormProductionYear ?? "");
                }
            }

            if (success)
            {
                Console.WriteLine("Операция успешна, закрываем окно");
                Close(true);
            }
            else
            {
                await ShowErrorDialog("Ошибка", "Не удалось сохранить оборудование! Проверьте правильность заполнения полей.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SaveAsync error: {ex.Message}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
            await ShowErrorDialog("Ошибка", $"Ошибка: {ex.Message}");
        }
    }

    private async Task ShowErrorDialog(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 350,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 15,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        MaxWidth = 300
                    },
                    new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Width = 80
                    }
                }
            }
        };

        var button = (Button)((StackPanel)dialog.Content).Children[1];
        button.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(_window);
    }

    private void Close(bool result)
    {
        try
        {
            if (_window != null && !_window.IsClosed)
            {
                _window.Close(result);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Close error: {ex.Message}");
        }
    }
}