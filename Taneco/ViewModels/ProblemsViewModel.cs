using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Taneco.Models;
using Taneco.Services;
using MsBox.Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Taneco.Views;
using Avalonia.Threading;

namespace Taneco.ViewModels;

public class ProblemsViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private ObservableCollection<Problem> _allProblems;
    private ObservableCollection<Problem> _filteredProblems;
    private bool _isLoading;
    private User? _currentUser;
    private string _selectedStatusFilter = "Все";
    private string _selectedTypeFilter = "Все";
    private string _searchText = string.Empty;
    private string _refreshButtonText = "Обновить";

    public ObservableCollection<string> StatusFilters { get; } = new();
    public ObservableCollection<string> TypeFilters { get; } = new();

    public ProblemsViewModel()
    {
        _db = new DatabaseService();
        _allProblems = new ObservableCollection<Problem>();
        _filteredProblems = new ObservableCollection<Problem>();

        RefreshCommand = new RelayCommand(async () => await LoadProblemsAsync(), () => !IsLoading);
        CreateProblemCommand = new RelayCommand(async () => await CreateProblem(), () => CanCreateProblem);
        OpenProblemDetailsCommand = new RelayCommand<Problem>(async (problem) => await OpenProblemDetails(problem));

        // Загружаем данные при создании ViewModel
        Task.Run(async () => await LoadProblemsAsync());
    }

    public User? CurrentUser
    {
        get => _currentUser;
        set
        {
            SetProperty(ref _currentUser, value);
            OnPropertyChanged(nameof(CanCreateProblem));
        }
    }

    public ObservableCollection<Problem> FilteredProblems
    {
        get => _filteredProblems;
        set => SetProperty(ref _filteredProblems, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            SetProperty(ref _isLoading, value);
            (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(RefreshButtonText));
        }
    }

    public string RefreshButtonText => IsLoading ? "Загрузка..." : "Обновить";

    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
                ApplyFilters();
        }
    }

    public string SelectedTypeFilter
    {
        get => _selectedTypeFilter;
        set
        {
            if (SetProperty(ref _selectedTypeFilter, value))
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

    public bool CanCreateProblem => CurrentUser?.CanCreateProblem ?? false;

    public ICommand RefreshCommand { get; }
    public ICommand CreateProblemCommand { get; }
    public RelayCommand<Problem> OpenProblemDetailsCommand { get; }

    private async Task LoadProblemsAsync()
    {
        if (IsLoading) return;

        await Dispatcher.UIThread.InvokeAsync(() => IsLoading = true);

        try
        {
            var problems = await _db.GetActiveProblemsAsync();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _allProblems.Clear();
                foreach (var p in problems)
                    _allProblems.Add(p);

                // Обновляем фильтры статусов
                var statuses = _allProblems.Select(p => p.Status).Distinct().OrderBy(s => s).ToList();
                StatusFilters.Clear();
                StatusFilters.Add("Все");
                foreach (var s in statuses)
                    StatusFilters.Add(s);

                // Обновляем фильтры типов
                var types = _allProblems.Select(p => p.Type).Distinct().OrderBy(t => t).ToList();
                TypeFilters.Clear();
                TypeFilters.Add("Все");
                foreach (var t in types)
                    TypeFilters.Add(t);

                ApplyFilters();
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Ошибка", $"Ошибка загрузки проблем: {ex.Message}");
                await box.ShowAsync();
            });
            Console.WriteLine($"LoadProblems error: {ex.Message}");
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
        }
    }

    private void ApplyFilters()
    {
        var filtered = _allProblems.AsEnumerable();

        if (SelectedStatusFilter != "Все")
            filtered = filtered.Where(p => p.Status == SelectedStatusFilter);

        if (SelectedTypeFilter != "Все")
            filtered = filtered.Where(p => p.Type == SelectedTypeFilter);

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(p => p.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                           p.PipelineName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        FilteredProblems.Clear();
        foreach (var p in filtered)
            FilteredProblems.Add(p);
    }

    private async Task CreateProblem()
    {
        var box = MessageBoxManager.GetMessageBoxStandard("Новая проблема", "Форма создания проблемы будет здесь");
        await box.ShowAsync();
    }

    private async Task OpenProblemDetails(Problem problem)
    {
        if (problem == null) return;

        var viewModel = new ProblemDetailsViewModel(problem, _db, CurrentUser);
        var window = new ProblemDetailsWindow
        {
            DataContext = viewModel
        };

        Window? mainWindow = null;

        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            mainWindow = desktop.MainWindow;

            if (mainWindow == null || !mainWindow.IsVisible)
            {
                mainWindow = desktop.Windows.FirstOrDefault(w => w.IsVisible);
            }
        }

        if (mainWindow != null)
        {
            await window.ShowDialog(mainWindow);
            await LoadProblemsAsync();
        }
        else
        {
            window.Show();
            await LoadProblemsAsync();
        }
    }
}

public class RelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = (param) => { execute(param); return Task.CompletedTask; };
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;

    public async void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            await _execute((T?)parameter);
        }
    }

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}