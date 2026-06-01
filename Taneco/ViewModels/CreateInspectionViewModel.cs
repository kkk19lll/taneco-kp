using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Taneco.Models;
using Taneco.Services;

namespace Taneco.ViewModels;

public class CreateInspectionViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private readonly int _currentUserId;
    private ObservableCollection<Pipeline> _pipelines;
    private Pipeline? _selectedPipeline;
    private string _selectedInspectionType = "Плановая";
    private DateTime _scheduledDate = DateTime.Today.AddDays(7);
    private string _description = string.Empty;
    private ObservableCollection<string> _inspectionTypes;

    public CreateInspectionViewModel(DatabaseService db, int currentUserId)
    {
        _db = db;
        _currentUserId = currentUserId;
        _pipelines = new ObservableCollection<Pipeline>();
        _inspectionTypes = new ObservableCollection<string> { "Плановая", "Внеплановая", "Контрольная" };

        CreateCommand = new RelayCommand(() => Task.Run(async () => await CreateInspection()), () => CanCreate);
        CloseCommand = new RelayCommand(Close);

        Task.Run(async () => await LoadPipelines());
    }

    public ObservableCollection<Pipeline> Pipelines
    {
        get => _pipelines;
        set => SetProperty(ref _pipelines, value);
    }

    public Pipeline? SelectedPipeline
    {
        get => _selectedPipeline;
        set
        {
            SetProperty(ref _selectedPipeline, value);
            ((RelayCommand)CreateCommand).RaiseCanExecuteChanged();
        }
    }

    public ObservableCollection<string> InspectionTypes
    {
        get => _inspectionTypes;
        set => SetProperty(ref _inspectionTypes, value);
    }

    public string SelectedInspectionType
    {
        get => _selectedInspectionType;
        set => SetProperty(ref _selectedInspectionType, value);
    }

    public DateTime ScheduledDate
    {
        get => _scheduledDate;
        set => SetProperty(ref _scheduledDate, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public bool CanCreate => SelectedPipeline != null && !string.IsNullOrWhiteSpace(Description);

    public ICommand CreateCommand { get; }
    public ICommand CloseCommand { get; }

    private async Task LoadPipelines()
    {
        var pipelines = await _db.GetPipelinesAsync();
        Pipelines.Clear();
        foreach (var p in pipelines)
            Pipelines.Add(p);
    }

    private async Task CreateInspection()
    {
        if (SelectedPipeline == null) return;

        var success = await _db.CreateScheduledInspectionAsync(
            _currentUserId,
            SelectedPipeline.Id,
            SelectedInspectionType,
            ScheduledDate,
            Description);

        if (success)
        {
            Close();
        }
    }

    private void Close()
    {
        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.Windows.OfType<Views.CreateInspectionWindow>().FirstOrDefault();
            window?.Close();
        }
    }
}