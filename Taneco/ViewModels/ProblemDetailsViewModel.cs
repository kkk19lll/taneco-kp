using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Taneco.Models;
using Taneco.Services;
using Avalonia.Threading;

namespace Taneco.ViewModels;

public class ProblemDetailsViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private readonly User _currentUser;
    private Problem _problem;
    private ObservableCollection<InspectorItem> _inspectors;
    private InspectorItem? _selectedInspector;
    private ObservableCollection<RepairManagerItem> _repairManagers;
    private RepairManagerItem? _selectedRepairManager;
    private string _urgency;
    private string _currentView;
    private string _inspectionResultDescription;
    private Action<bool>? _closeAction; // Изменено: передаем результат (было ли изменение)

    // Возможные состояния View
    private const string ViewStart = "Start";
    private const string ViewInspectionForm = "InspectionForm";
    private const string ViewInspectionResult = "InspectionResult";
    private const string ViewRepairManager = "RepairManager";

    public ProblemDetailsViewModel(Problem problem, DatabaseService db, User currentUser, Action<bool>? closeAction = null)
    {
        _db = db;
        _problem = problem;
        _currentUser = currentUser;
        _closeAction = closeAction;
        _inspectors = new ObservableCollection<InspectorItem>();
        _repairManagers = new ObservableCollection<RepairManagerItem>();
        _urgency = "Средняя";
        _inspectionResultDescription = string.Empty;
        _currentView = ViewStart;

        StartInspectionCommand = new RelayCommand(() => CurrentView = ViewInspectionForm);
        AssignInspectionCommand = new RelayCommand(async () => await AssignInspectionAsync());
        ConfirmFalseReadingCommand = new RelayCommand(async () => await ConfirmFalseReadingAsync());
        ConfirmRepairRequiredCommand = new RelayCommand(() => CurrentView = ViewRepairManager);
        SendToRepairManagerCommand = new RelayCommand(async () => await SendToRepairManagerAsync());
        CancelCommand = new RelayCommand(() => _closeAction?.Invoke(false));

        // Загружаем данные
        Task.Run(async () => await LoadInspectorsAsync());
        Task.Run(async () => await LoadRepairManagersAsync());
    }

    public Problem Problem
    {
        get => _problem;
        set => SetProperty(ref _problem, value);
    }

    public ObservableCollection<InspectorItem> Inspectors
    {
        get => _inspectors;
        set => SetProperty(ref _inspectors, value);
    }

    public InspectorItem? SelectedInspector
    {
        get => _selectedInspector;
        set => SetProperty(ref _selectedInspector, value);
    }

    public ObservableCollection<RepairManagerItem> RepairManagers
    {
        get => _repairManagers;
        set => SetProperty(ref _repairManagers, value);
    }

    public RepairManagerItem? SelectedRepairManager
    {
        get => _selectedRepairManager;
        set => SetProperty(ref _selectedRepairManager, value);
    }

    public string Urgency
    {
        get => _urgency;
        set => SetProperty(ref _urgency, value);
    }

    public string InspectionResultDescription
    {
        get => _inspectionResultDescription;
        set => SetProperty(ref _inspectionResultDescription, value);
    }

    public string CurrentView
    {
        get => _currentView;
        set
        {
            SetProperty(ref _currentView, value);
            // Уведомляем UI об изменении видимости
            OnPropertyChanged(nameof(ShowStartButton));
            OnPropertyChanged(nameof(ShowInspectionForm));
            OnPropertyChanged(nameof(ShowInspectionResult));
            OnPropertyChanged(nameof(ShowRepairManagerSelection));
        }
    }

    // Свойства для видимости UI
    public bool ShowStartButton => CurrentView == ViewStart;
    public bool ShowInspectionForm => CurrentView == ViewInspectionForm;
    public bool ShowInspectionResult => CurrentView == ViewInspectionResult;
    public bool ShowRepairManagerSelection => CurrentView == ViewRepairManager;

    public ICommand StartInspectionCommand { get; }
    public ICommand AssignInspectionCommand { get; }
    public ICommand ConfirmFalseReadingCommand { get; }
    public ICommand ConfirmRepairRequiredCommand { get; }
    public ICommand SendToRepairManagerCommand { get; }
    public ICommand CancelCommand { get; }

    private async Task LoadInspectorsAsync()
    {
        try
        {
            var inspectors = await _db.GetInspectorsAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Inspectors.Clear();
                foreach (var inspector in inspectors)
                    Inspectors.Add(inspector);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadInspectorsAsync error: {ex.Message}");
        }
    }

    private async Task LoadRepairManagersAsync()
    {
        try
        {
            var managers = await _db.GetRepairManagersAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RepairManagers.Clear();
                foreach (var manager in managers)
                    RepairManagers.Add(manager);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadRepairManagersAsync error: {ex.Message}");
        }
    }

    private async Task AssignInspectionAsync()
    {
        if (SelectedInspector == null)
        {
            Console.WriteLine("No inspector selected");
            return;
        }

        try
        {
            Console.WriteLine($"Assigning inspection: ProblemId={Problem.Id}, InspectorId={SelectedInspector.Id}, Urgency={Urgency}");

            bool success = await _db.CreateInspectionWithStatusAsync(
                Problem.Id,
                SelectedInspector.Id,
                $"Проверка проблемы: {Problem.Description}",
                Urgency,
                "Ожидает подтверждения"
            );

            if (success)
            {
                Problem.Status = "Ожидает подтверждения";
                CurrentView = ViewInspectionResult;
                Console.WriteLine("Inspection created successfully");
            }
            else
            {
                Console.WriteLine("Failed to create inspection");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AssignInspectionAsync error: {ex.Message}");
        }
    }

    private async Task ConfirmFalseReadingAsync()
    {
        try
        {
            bool success = await _db.UpdateProblemStatusAsync(Problem.Id, "Ложные показания");
            if (success)
            {
                Problem.Status = "Ложные показания";
                _closeAction?.Invoke(true); // Передаем true - были изменения
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ConfirmFalseReadingAsync error: {ex.Message}");
        }
    }

    private async Task SendToRepairManagerAsync()
    {
        if (SelectedRepairManager == null)
        {
            Console.WriteLine("No repair manager selected");
            return;
        }

        try
        {
            Console.WriteLine($"Sending to repair: ProblemId={Problem.Id}, ManagerId={SelectedRepairManager.Id}");

            // Сначала обновляем статус проверки на "Требует срочного вмешательства" или подобный
            bool success = await _db.UpdateProblemStatusAsync(Problem.Id, "В процессе ремонта");

            if (success)
            {
                Problem.Status = "В процессе ремонта";
                _closeAction?.Invoke(true); // Передаем true - были изменения, обновить список проблем
            }
            else
            {
                Console.WriteLine("Failed to update problem status");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SendToRepairManagerAsync error: {ex.Message}");
        }
    }
}