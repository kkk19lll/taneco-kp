using System;
using System.Windows.Input;
using Taneco.Models;
using Taneco.Views;

namespace Taneco.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private object? _currentView;
    private string _currentTitle = "Мониторинг";
    private User? _currentUser;

    public MainWindowViewModel(User user)
    {
        CurrentUser = user;

        // Инициализация всех команд
        ShowMonitoringCommand = new RelayCommand(_ => ShowMonitoring());
        ShowEquipmentsCommand = new RelayCommand(_ => ShowEquipments());
        ShowProblemsCommand = new RelayCommand(_ => ShowProblems());
        ShowInspectionsCommand = new RelayCommand(_ => ShowInspections());
        ShowRepairsCommand = new RelayCommand(_ => ShowRepairs());
        ShowStaffCommand = new RelayCommand(_ => ShowStaff());
        ShowReportsCommand = new RelayCommand(_ => ShowReports());
        ShowEquipmentManagementCommand = new RelayCommand(_ => ShowEquipmentManagement());
        LogoutCommand = new RelayCommand(_ => Environment.Exit(0));

        // Показываем главный экран
        ShowMonitoring();
    }

    public User? CurrentUser
    {
        get => _currentUser;
        set => SetProperty(ref _currentUser, value);
    }

    public object? CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    public string CurrentTitle
    {
        get => _currentTitle;
        set => SetProperty(ref _currentTitle, value);
    }

    // Права доступа для отображения меню
    public bool CanMonitor => CurrentUser?.CanMonitor ?? false;
    public bool CanEditSensors => CurrentUser?.CanEditSensors ?? false;
    public bool CanManageProblems => CurrentUser?.CanManageProblems ?? false;
    public bool CanViewInspections => CurrentUser?.CanViewInspections ?? false;
    public bool CanManageRepairs => CurrentUser?.CanManageRepairs ?? false;
    public bool CanManageStaff => CurrentUser?.CanManageStaff ?? false;
    public bool CanViewReports => CurrentUser?.CanViewReports ?? false;
    public bool IsAdmin => CurrentUser?.IsAdmin ?? false;
    public bool CanManageEquipment => CurrentUser?.CanManageEquipment ?? false;

    public ICommand ShowMonitoringCommand { get; }
    public ICommand ShowEquipmentsCommand { get; }
    public ICommand ShowProblemsCommand { get; }
    public ICommand ShowInspectionsCommand { get; }
    public ICommand ShowRepairsCommand { get; }
    public ICommand ShowStaffCommand { get; }
    public ICommand ShowReportsCommand { get; }
    public ICommand ShowAdminCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand ShowEquipmentManagementCommand { get; }

    private void ShowMonitoring()
    {
        var view = new MonitoringView();
        var viewModel = new MonitoringViewModel();
        viewModel.CurrentUser = CurrentUser;
        view.DataContext = viewModel;
        CurrentView = view;
        CurrentTitle = "Мониторинг";
    }

    private void ShowEquipments()
    {
        var view = new EquipmentsView();
        var viewModel = new EquipmentsViewModel();
        viewModel.CurrentUser = CurrentUser;
        view.DataContext = viewModel;
        CurrentView = view;
        CurrentTitle = "Оборудование";
    }

    private void ShowProblems()
    {
        var view = new ProblemsView();
        var viewModel = new ProblemsViewModel();
        viewModel.CurrentUser = CurrentUser;
        view.DataContext = viewModel;
        CurrentView = view;
        CurrentTitle = "Проблемы";
    }

    private void ShowInspections()
    {
        var view = new InspectionsView();
        var viewModel = new InspectionsViewModel();
        viewModel.CurrentUser = CurrentUser;
        view.DataContext = viewModel;
        CurrentView = view;
        CurrentTitle = "Проверки";
    }

    private void ShowRepairs()
    {
        var view = new RepairsView();
        var viewModel = new RepairsViewModel();
        viewModel.CurrentUser = CurrentUser;
        view.DataContext = viewModel;
        CurrentView = view;
        CurrentTitle = "Ремонты";
    }

    private void ShowStaff()
    {
        var view = new StaffView();
        var viewModel = new StaffViewModel();
        viewModel.CurrentUser = CurrentUser;
        view.DataContext = viewModel;
        CurrentView = view;
        CurrentTitle = "Персонал";
    }

    private void ShowReports()
    {
        var view = new ReportsView();
        var viewModel = new ReportsViewModel();
        viewModel.CurrentUser = CurrentUser;
        view.DataContext = viewModel;
        CurrentView = view;
        CurrentTitle = "Отчеты";
    }

    private void ShowEquipmentManagement()
    {
        var view = new EquipmentManagementView();
        var viewModel = new EquipmentManagementViewModel();
        viewModel.CurrentUser = CurrentUser;
        view.DataContext = viewModel;
        CurrentView = view;
        CurrentTitle = "Оборудование";
    }
}