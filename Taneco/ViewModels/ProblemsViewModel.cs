using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Taneco.Models;
using Taneco.Services;
using MsBox.Avalonia;

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
    
    public ObservableCollection<string> StatusFilters { get; } = new();
    public ObservableCollection<string> TypeFilters { get; } = new();

    public ProblemsViewModel()
    {
        _db = new DatabaseService();
        _allProblems = new ObservableCollection<Problem>();
        _filteredProblems = new ObservableCollection<Problem>();
        
        RefreshCommand = new RelayCommand(() => Task.Run(async () => await LoadProblemsAsync()), () => !IsLoading);
        CreateProblemCommand = new RelayCommand(() => Task.Run(async () => await CreateProblem()), () => CanCreateProblem);
        
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
        set => SetProperty(ref _isLoading, value);
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
    
    public string SelectedTypeFilter
    {
        get => _selectedTypeFilter;
        set
        {
            if (SetProperty(ref _selectedTypeFilter, value))
                ApplyFilters();
        }
    }
    
    public bool CanCreateProblem => CurrentUser?.CanCreateProblem ?? false;
    
    public ICommand RefreshCommand { get; }
    public ICommand CreateProblemCommand { get; }
    
    private async Task LoadProblemsAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            // Используем GetActiveProblemsAsync для показа только активных (незавершённых) проблем
            var problems = await _db.GetActiveProblemsAsync();
            _allProblems.Clear();
            foreach (var p in problems)
                _allProblems.Add(p);
            
            // Заполняем фильтр статусов
            var statuses = _allProblems.Select(p => p.Status).Distinct().ToList();
            StatusFilters.Clear();
            StatusFilters.Add("Все");
            foreach (var s in statuses)
                StatusFilters.Add(s);
            
            // Заполняем фильтр типов
            var types = _allProblems.Select(p => p.Type).Distinct().ToList();
            TypeFilters.Clear();
            TypeFilters.Add("Все");
            foreach (var t in types)
                TypeFilters.Add(t);
            
            ApplyFilters();
            
            Console.WriteLine($"Загружено проблем: {_allProblems.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadProblems error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    private void ApplyFilters()
    {
        var filtered = _allProblems.AsEnumerable();
        
        if (SelectedStatusFilter != "Все")
            filtered = filtered.Where(p => p.Status == SelectedStatusFilter);
        
        if (SelectedTypeFilter != "Все")
            filtered = filtered.Where(p => p.Type == SelectedTypeFilter);
        
        FilteredProblems.Clear();
        foreach (var p in filtered)
            FilteredProblems.Add(p);
        
        Console.WriteLine($"После фильтрации: {FilteredProblems.Count} проблем");
    }
    
    private async Task CreateProblem()
    {
        var box = MessageBoxManager.GetMessageBoxStandard("Новая проблема", "Форма создания проблемы будет здесь");
        await box.ShowAsync();
    }
}