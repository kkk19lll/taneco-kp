using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Taneco.Models;
using Taneco.Services;
using Taneco.Views;

namespace Taneco.ViewModels;

public class RepairsViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private ObservableCollection<Repair> _allRepairs;
    private ObservableCollection<Repair> _filteredRepairs;
    private bool _isLoading;
    private User? _currentUser;
    private string _selectedStatusFilter = "Все";
    private ObservableCollection<string> _statusFilters;
    private string _searchText = "";
    private DateTime? _selectedDate;
    private ObservableCollection<DateTime> _availableDates;

    public RepairsViewModel()
    {
        _db = new DatabaseService();
        _allRepairs = new ObservableCollection<Repair>();
        _filteredRepairs = new ObservableCollection<Repair>();

        _statusFilters = new ObservableCollection<string>
        {
            "Все",
            "Аварийная остановка",
            "Подготовка к ремонту",
            "В процессе ремонта",
            "Испытания после ремонта",
            "Готов к запуску",
            "Завершен",
            "Ожидает поставки материалов",
            "Требуется проектная документация",
            "На согласовании метода ремонта",
            "Консервация оборудования"
        };

        _availableDates = new ObservableCollection<DateTime>();

        LoadCommand = new RelayCommand(() => Task.Run(async () => await LoadRepairsAsync()), () => !IsLoading);
        RepairClickCommand = new RelayCommand(OnRepairClick);
        SearchCommand = new RelayCommand(() => ApplyFilters());
        ClearSearchCommand = new RelayCommand(() => { SearchText = ""; ApplyFilters(); });
        ClearDateCommand = new RelayCommand(() => { SelectedDateString = ""; ApplyFilters(); });

        Task.Run(async () => await LoadRepairsAsync());
        Task.Run(async () => await LoadAvailableDatesAsync());
    }

    public User? CurrentUser
    {
        get => _currentUser;
        set
        {
            SetProperty(ref _currentUser, value);
            Task.Run(async () => await LoadRepairsAsync());
        }
    }

    public ObservableCollection<Repair> FilteredRepairs
    {
        get => _filteredRepairs;
        set => SetProperty(ref _filteredRepairs, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
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

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                ApplyFilters();
        }
    }

    public DateTime? SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value))
                ApplyFilters();
        }
    }

    public ObservableCollection<DateTime> AvailableDates
    {
        get => _availableDates;
        set => SetProperty(ref _availableDates, value);
    }

    private string _selectedDateString = "";

    public string SelectedDateString
    {
        get => _selectedDateString;
        set
        {
            if (SetProperty(ref _selectedDateString, value))
            {
                if (DateTime.TryParseExact(value, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                {
                    SelectedDate = parsedDate;
                }
                else if (string.IsNullOrWhiteSpace(value))
                {
                    SelectedDate = null;
                }
            }
        }
    }

    public bool HasSelectedDate => !string.IsNullOrWhiteSpace(SelectedDateString);

    public ICommand LoadCommand { get; }
    public ICommand RepairClickCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand ClearDateCommand { get; }

    public void OnRepairClickFromView(Repair? repair, Window? ownerWindow = null)
    {
        if (repair != null)
            OnRepairClickWithOwner(repair, ownerWindow);
    }

    private async Task LoadAvailableDatesAsync()
    {
        try
        {
            var dates = await _db.GetAvailableDatesAsync();
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                AvailableDates.Clear();
                AvailableDates.Add(DateTime.MinValue);
                foreach (var date in dates)
                    AvailableDates.Add(date);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadAvailableDatesAsync error: {ex.Message}");
        }
    }

    private async Task LoadRepairsAsync()
    {
        if (IsLoading) return;

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => IsLoading = true);

        try
        {
            ObservableCollection<Repair> repairs;

            if (CurrentUser?.Role == "Администратор" || CurrentUser?.Role == "Начальник_ремонтной_службы")
            {
                var repairsWithDetails = await _db.GetRepairsWithDetailsAsync();
                repairs = repairsWithDetails;
            }
            else
            {
                repairs = new ObservableCollection<Repair>();
            }

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                _allRepairs.Clear();
                foreach (var r in repairs)
                    _allRepairs.Add(r);

                ApplyFilters();
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadRepairsAsync error: {ex.Message}");
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                FilteredRepairs.Clear();
            });
        }
        finally
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
        }
    }

    private void ApplyFilters()
    {
        try
        {
            var sourceList = _allRepairs.ToList();

            var filtered = sourceList.AsEnumerable();

            if (SelectedStatusFilter != "Все")
            {
                filtered = filtered.Where(r => r.Status == SelectedStatusFilter);
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchLower = SearchText.ToLower();
                filtered = filtered.Where(r =>
                    (r.ProblemDescription?.ToLower().Contains(searchLower) ?? false) ||
                    (r.TeamName?.ToLower().Contains(searchLower) ?? false) ||
                    (r.EquipmentName?.ToLower().Contains(searchLower) ?? false));
            }

            if (SelectedDate.HasValue && SelectedDate.Value != DateTime.MinValue)
            {
                filtered = filtered.Where(r => r.StartDate.Date == SelectedDate.Value.Date);
            }

            var resultList = filtered.ToList();

            FilteredRepairs.Clear();
            foreach (var r in resultList)
            {
                FilteredRepairs.Add(r);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ApplyFilters error: {ex.Message}");
        }
    }

    private async void OnRepairClick(object? parameter)
    {
        if (parameter is Repair repair)
        {
            await ShowRepairDetailsDialog(repair, null);
        }
    }

    private async void OnRepairClickWithOwner(Repair repair, Window? ownerWindow)
    {
        await ShowRepairDetailsDialog(repair, ownerWindow);
    }

    private async Task ShowRepairDetailsDialog(Repair repair, Window? ownerWindow)
    {
        try
        {
            var dialog = new RepairDetailsWindow();
            var viewModel = new RepairDetailsViewModel(repair.Id, _db, CurrentUser);
            dialog.DataContext = viewModel;

            // Try to get a valid owner window
            Window? targetOwner = ownerWindow;

            if (targetOwner == null || !targetOwner.IsVisible)
            {
                targetOwner = GetMainWindow();
            }

            if (targetOwner != null && targetOwner.IsVisible)
            {
                await dialog.ShowDialog(targetOwner);
            }
            else
            {
                // Fallback: show as regular window
                dialog.Show();
            }

            await LoadRepairsAsync();
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Error showing dialog: {ex.Message}");
            try
            {
                // Second fallback: try to show without owner
                var dialog = new RepairDetailsWindow();
                var viewModel = new RepairDetailsViewModel(repair.Id, _db, CurrentUser);
                dialog.DataContext = viewModel;
                dialog.Show();
                await LoadRepairsAsync();
            }
            catch (Exception innerEx)
            {
                Console.WriteLine($"Critical error showing repair details: {innerEx.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }

    private Window? GetMainWindow()
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting main window: {ex.Message}");
        }
        return null;
    }
}