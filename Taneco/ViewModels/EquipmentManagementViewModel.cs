// ViewModels/EquipmentManagementViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Taneco.Models;
using Taneco.Services;

namespace Taneco.ViewModels;

public class EquipmentManagementViewModel : ViewModelBase
{
    private readonly DatabaseService _databaseService;
    private User? _currentUser;
    private ObservableCollection<Equipment> _equipmentList = new();
    private Equipment? _selectedEquipment;
    private string _searchText = string.Empty;
    private bool _isLoading;
    private bool _showDetailsPanel;
    private Equipment? _selectedEquipmentDetails;

    public EquipmentManagementViewModel()
    {
        _databaseService = new DatabaseService();
        RefreshEquipmentListCommand = new RelayCommand(_ => RefreshEquipmentList());
    }

    public User? CurrentUser
    {
        get => _currentUser;
        set => SetProperty(ref _currentUser, value);
    }

    public ObservableCollection<Equipment> EquipmentList
    {
        get => _equipmentList;
        set => SetProperty(ref _equipmentList, value);
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
                Task.Run(async () => await LoadEquipmentDetailsAsync(value.Id));
            }
            else
            {
                ShowDetailsPanel = false;
                SelectedEquipmentDetails = null;
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            SetProperty(ref _searchText, value);
            if (string.IsNullOrWhiteSpace(value))
            {
                Task.Run(async () => await LoadEquipmentAsync());
            }
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

    public Equipment? SelectedEquipmentDetails
    {
        get => _selectedEquipmentDetails;
        set => SetProperty(ref _selectedEquipmentDetails, value);
    }

    public ICommand RefreshEquipmentListCommand { get; }

    public async Task LoadEquipmentAsync()
    {
        IsLoading = true;
        try
        {
            var equipment = await _databaseService.GetAllEquipmentAsync();
            EquipmentList.Clear();
            foreach (var item in equipment)
            {
                EquipmentList.Add(item);
            }
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

    public async Task SearchEquipmentAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            await LoadEquipmentAsync();
            return;
        }

        IsLoading = true;
        try
        {
            var results = await _databaseService.SearchEquipmentByNameAsync(SearchText);
            EquipmentList.Clear();
            foreach (var item in results)
            {
                EquipmentList.Add(item);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SearchEquipmentAsync error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadEquipmentDetailsAsync(int equipmentId)
    {
        IsLoading = true;
        try
        {
            var details = await _databaseService.GetEquipmentDetailsAsync(equipmentId);
            SelectedEquipmentDetails = details;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadEquipmentDetailsAsync error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async void RefreshEquipmentList()
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