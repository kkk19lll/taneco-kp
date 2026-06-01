using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Taneco.Models;
using Taneco.Services;

namespace Taneco.ViewModels;

public class CreatePipelineViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    
    private string _name = string.Empty;
    private string _selectedMaterial = string.Empty;
    private string _length = string.Empty;
    private string _diameter = string.Empty;
    private DateTime _installationDate = DateTime.Today;
    
    private bool _nameError;
    private bool _lengthError;
    private bool _diameterError;

    public ObservableCollection<string> Materials { get; } = new();

    public CreatePipelineViewModel(DatabaseService db)
    {
        _db = db;
        
        CreateCommand = new RelayCommand(() => Task.Run(async () => await CreatePipeline()), () => CanCreate);
        CloseCommand = new RelayCommand(Close);
        
        Task.Run(async () => await LoadMaterials());
    }

    public string Name
    {
        get => _name;
        set 
        { 
            if (SetProperty(ref _name, value))
            {
                NameError = string.IsNullOrWhiteSpace(value);
                ((RelayCommand)CreateCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedMaterial
    {
        get => _selectedMaterial;
        set => SetProperty(ref _selectedMaterial, value);
    }

    public string Length
    {
        get => _length;
        set 
        { 
            if (SetProperty(ref _length, value))
            {
                LengthError = string.IsNullOrWhiteSpace(value);
                ((RelayCommand)CreateCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string Diameter
    {
        get => _diameter;
        set 
        { 
            if (SetProperty(ref _diameter, value))
            {
                DiameterError = string.IsNullOrWhiteSpace(value);
                ((RelayCommand)CreateCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public DateTime InstallationDate
    {
        get => _installationDate;
        set => SetProperty(ref _installationDate, value);
    }

    public bool NameError
    {
        get => _nameError;
        set => SetProperty(ref _nameError, value);
    }

    public bool LengthError
    {
        get => _lengthError;
        set => SetProperty(ref _lengthError, value);
    }

    public bool DiameterError
    {
        get => _diameterError;
        set => SetProperty(ref _diameterError, value);
    }

    public bool CanCreate => !string.IsNullOrWhiteSpace(Name) && 
                             !string.IsNullOrWhiteSpace(Length) && 
                             !string.IsNullOrWhiteSpace(Diameter);

    public ICommand CreateCommand { get; }
    public ICommand CloseCommand { get; }

    private async Task LoadMaterials()
    {
        var materials = await _db.GetMaterialsAsync();
        Materials.Clear();
        foreach (var m in materials)
            Materials.Add(m);
    }

    private async Task CreatePipeline()
    {
        var success = await _db.CreatePipelineAsync(
            Name, SelectedMaterial, Length, Diameter, InstallationDate);
        
        if (success)
        {
            Close();
        }
    }

    private void Close()
    {
        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.Windows.OfType<Views.CreatePipelineWindow>().FirstOrDefault();
            window?.Close();
        }
    }
}