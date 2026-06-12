using System;
using System.Windows.Input;
using Taneco.Models;
using Taneco.Views;
using Avalonia.Controls;

namespace Taneco.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private object? _currentView;
    private string _currentTitle = string.Empty;
    private User? _currentUser;
    private Window? _parentWindow;

    public MainWindowViewModel(User user, Window parentWindow)
    {
        CurrentUser = user;
        _parentWindow = parentWindow;

        ShowMonitoringCommand = new RelayCommand(_ => ShowMonitoring());
        ShowEquipmentsCommand = new RelayCommand(_ => ShowEquipments());
        ShowProblemsCommand = new RelayCommand(_ => ShowProblems());
        ShowInspectionsCommand = new RelayCommand(_ => ShowInspections());
        ShowRepairsCommand = new RelayCommand(_ => ShowRepairs());
        ShowStaffCommand = new RelayCommand(_ => ShowStaff());
        ShowReportsCommand = new RelayCommand(_ => ShowReports());
        ShowEquipmentManagementCommand = new RelayCommand(_ => ShowEquipmentManagement());
        LogoutCommand = new RelayCommand(_ => Environment.Exit(0));

        // Открываем окно по умолчанию в зависимости от роли
        ShowDefaultViewForRole();
    }

    private void ShowDefaultViewForRole()
    {
        if (CurrentUser == null) return;

        // Определяем основную роль пользователя и открываем соответствующее окно
        switch (CurrentUser.Role)
        {
            case "Администратор":
                // Для администратора открываем мониторинг (главная страница)
                ShowMonitoring();
                break;
            case "Оператор":
                ShowMonitoring();
                break;
            case "Инженер_КИПиА":
                ShowEquipmentManagement();
                break;
            case "Инспектор":
                ShowProblems();
                break;
            case "Начальник_ремонтной_службы":
                ShowRepairs();
                break;
            case "HR":
                ShowStaff();
                break;
            case "Аналитик":
                ShowReports();
                break;
            default:
                // Если роль не распознана, показываем мониторинг
                ShowMonitoring();
                break;
        }
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
        if (_parentWindow == null)
        {
            Console.WriteLine("Parent window is null");
            return;
        }

        var view = new EquipmentManagementView();
        var viewModel = new EquipmentManagementViewModel(_parentWindow);
        viewModel.CurrentUser = CurrentUser;
        view.DataContext = viewModel;
        CurrentView = view;
        CurrentTitle = "Управление оборудованием";
    }
}