using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Taneco.Models;
using Taneco.Services;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace Taneco.ViewModels;

public class StaffViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private ObservableCollection<Employee> _allEmployees;
    private ObservableCollection<Employee> _filteredEmployees;
    private Employee? _selectedEmployee;
    private bool _isLoading;
    private string _searchText = string.Empty;
    private User? _currentUser;
    private bool _isEditMode;
    
    private string _editLastName = string.Empty;
    private string _editFirstName = string.Empty;
    private string _editPatronymic = string.Empty;
    private string _editPhone = string.Empty;
    private string _editDepartment = string.Empty;
    private string _editPosition = string.Empty;
    private string _editRole = string.Empty;
    private string _editLogin = string.Empty;
    private string _editPassword = string.Empty;
    
    public ObservableCollection<string> Departments { get; } = new();
    public ObservableCollection<string> Positions { get; } = new();
    public ObservableCollection<string> AvailableRoles { get; } = new();

    public StaffViewModel()
    {
        _db = new DatabaseService();
        _allEmployees = new ObservableCollection<Employee>();
        _filteredEmployees = new ObservableCollection<Employee>();
        
        RefreshCommand = new RelayCommand(() => Task.Run(async () => await LoadEmployeesAsync()), () => !IsLoading);
        AddEmployeeCommand = new RelayCommand(() => StartAdd(), () => CanManageStaff);
        EditCommand = new RelayCommand(() => StartEdit(), () => SelectedEmployee != null && CanManageStaff);
        SaveEmployeeCommand = new RelayCommand(() => Task.Run(async () => await SaveEmployee()), () => IsEditMode && CanManageStaff);
        CancelEditCommand = new RelayCommand(() => CancelEdit());
        DeleteEmployeeCommand = new RelayCommand(() => Task.Run(async () => await DeleteEmployee()), () => SelectedEmployee != null && CanManageStaff);
        ResetPasswordCommand = new RelayCommand(() => Task.Run(async () => ResetPassword()), () => SelectedEmployee != null && CanManageStaff);
        
        AvailableRoles.Add("Администратор");
        AvailableRoles.Add("HR");
        AvailableRoles.Add("Оператор");
        AvailableRoles.Add("Инженер_КИПиА");
        AvailableRoles.Add("Инспектор");
        AvailableRoles.Add("Начальник_ремонтной_службы");
        AvailableRoles.Add("Аналитик");
        
        Task.Run(async () => await LoadEmployeesAsync());
    }
    
    public User? CurrentUser
    {
        get => _currentUser;
        set 
        { 
            SetProperty(ref _currentUser, value);
            OnPropertyChanged(nameof(CanManageStaff));
            ((RelayCommand)RefreshCommand).RaiseCanExecuteChanged();
            ((RelayCommand)AddEmployeeCommand).RaiseCanExecuteChanged();
            ((RelayCommand)EditCommand).RaiseCanExecuteChanged();
            ((RelayCommand)DeleteEmployeeCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ResetPasswordCommand).RaiseCanExecuteChanged();
        }
    }
    
    public ObservableCollection<Employee> Employees
    {
        get => _filteredEmployees;
        set => SetProperty(ref _filteredEmployees, value);
    }
    
    public Employee? SelectedEmployee
    {
        get => _selectedEmployee;
        set 
        { 
            if (SetProperty(ref _selectedEmployee, value))
            {
                OnPropertyChanged(nameof(IsEmployeeSelected));
                OnPropertyChanged(nameof(IsEmployeeNotSelected));
                ((RelayCommand)EditCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DeleteEmployeeCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ResetPasswordCommand).RaiseCanExecuteChanged();
                
                if (value != null && !IsEditMode)
                {
                    EditLastName = value.LastName;
                    EditFirstName = value.FirstName;
                    EditPatronymic = value.Patronymic;
                    EditPhone = value.Phone;
                    EditDepartment = value.Department;
                    EditPosition = value.Position;
                    EditRole = value.Role;
                    EditLogin = value.Login;
                    EditPassword = "";
                }
            }
        }
    }
    
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }
    
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                FilterEmployees();
        }
    }
    
    public bool IsEditMode
    {
        get => _isEditMode;
        set => SetProperty(ref _isEditMode, value);
    }
    
    public bool CanManageStaff => CurrentUser?.CanManageStaff ?? false;
    public bool IsEmployeeSelected => SelectedEmployee != null;
    public bool IsEmployeeNotSelected => SelectedEmployee == null;
    
    public string EditLastName { get => _editLastName; set => SetProperty(ref _editLastName, value); }
    public string EditFirstName { get => _editFirstName; set => SetProperty(ref _editFirstName, value); }
    public string EditPatronymic { get => _editPatronymic; set => SetProperty(ref _editPatronymic, value); }
    public string EditPhone { get => _editPhone; set => SetProperty(ref _editPhone, value); }
    public string EditDepartment { get => _editDepartment; set => SetProperty(ref _editDepartment, value); }
    public string EditPosition { get => _editPosition; set => SetProperty(ref _editPosition, value); }
    public string EditRole { get => _editRole; set => SetProperty(ref _editRole, value); }
    public string EditLogin { get => _editLogin; set => SetProperty(ref _editLogin, value); }
    public string EditPassword { get => _editPassword; set => SetProperty(ref _editPassword, value); }
    
    public ICommand RefreshCommand { get; }
    public ICommand AddEmployeeCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand SaveEmployeeCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand DeleteEmployeeCommand { get; }
    public ICommand ResetPasswordCommand { get; }
    
    private async Task LoadEmployeesAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var employees = await _db.GetEmployeesAsync();
            _allEmployees.Clear();
            foreach (var e in employees)
                _allEmployees.Add(e);
            
            var depts = _allEmployees.Select(e => e.Department).Distinct().ToList();
            Departments.Clear();
            foreach (var d in depts)
                Departments.Add(d);
            
            var pos = _allEmployees.Select(e => e.Position).Distinct().ToList();
            Positions.Clear();
            foreach (var p in pos)
                Positions.Add(p);
            
            FilterEmployees();
            Console.WriteLine($"LoadEmployeesAsync: загружено {_allEmployees.Count} сотрудников");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadEmployees error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    private void FilterEmployees()
    {
        Employees.Clear();
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allEmployees
            : _allEmployees.Where(e => e.LastName.ToLower().Contains(SearchText.ToLower()) ||
                                       e.FirstName.ToLower().Contains(SearchText.ToLower()) ||
                                       e.Position.ToLower().Contains(SearchText.ToLower()));
        foreach (var e in filtered)
            Employees.Add(e);
    }
    
    private void StartAdd()
    {
        SelectedEmployee = null;
        EditLastName = "";
        EditFirstName = "";
        EditPatronymic = "";
        EditPhone = "";
        EditDepartment = Departments.FirstOrDefault() ?? "";
        EditPosition = Positions.FirstOrDefault() ?? "";
        EditRole = "Оператор";
        EditLogin = "";
        EditPassword = "";
        IsEditMode = true;
        Console.WriteLine("StartAdd: режим добавления включен");
    }
    
    private void StartEdit()
    {
        if (SelectedEmployee != null)
        {
            IsEditMode = true;
            Console.WriteLine($"StartEdit: редактирование сотрудника id={SelectedEmployee.Id}");
        }
    }
    
    private void CancelEdit()
    {
        IsEditMode = false;
        if (SelectedEmployee != null)
        {
            EditLastName = SelectedEmployee.LastName;
            EditFirstName = SelectedEmployee.FirstName;
            EditPatronymic = SelectedEmployee.Patronymic;
            EditPhone = SelectedEmployee.Phone;
            EditDepartment = SelectedEmployee.Department;
            EditPosition = SelectedEmployee.Position;
            EditRole = SelectedEmployee.Role;
            EditLogin = SelectedEmployee.Login;
        }
        Console.WriteLine("CancelEdit: режим редактирования выключен");
    }
    
    private async Task SaveEmployee()
    {
        Console.WriteLine("SaveEmployee: начало");
        
        if (EditRole == "Администратор")
        {
            var currentAdminCount = _allEmployees.Count(e => e.Role == "Администратор" && e.Id != (SelectedEmployee?.Id ?? 0));
            if (currentAdminCount >= 2)
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Ошибка", "Не может быть больше 2 администраторов");
                await box.ShowAsync();
                return;
            }
        }
        
        if (EditRole == "HR")
        {
            var currentHrCount = _allEmployees.Count(e => e.Role == "HR" && e.Id != (SelectedEmployee?.Id ?? 0));
            if (currentHrCount >= 1)
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Ошибка", "HR может быть только один");
                await box.ShowAsync();
                return;
            }
        }
        
        if (string.IsNullOrWhiteSpace(EditLogin))
        {
            var box = MessageBoxManager.GetMessageBoxStandard("Ошибка", "Логин обязателен");
            await box.ShowAsync();
            return;
        }
        
        if (SelectedEmployee == null && string.IsNullOrWhiteSpace(EditPassword))
        {
            var box = MessageBoxManager.GetMessageBoxStandard("Ошибка", "Пароль обязателен для нового сотрудника");
            await box.ShowAsync();
            return;
        }
        
        IsLoading = true;
        try
        {
            bool success;
            
            if (SelectedEmployee == null)
            {
                Console.WriteLine($"SaveEmployee: добавление нового сотрудника {EditLastName} {EditFirstName}");
                success = await _db.AddEmployeeAsync(
                    EditLastName, EditFirstName, EditPatronymic, EditPhone,
                    EditDepartment, EditPosition, EditRole, EditLogin, EditPassword);
                Console.WriteLine($"SaveEmployee: результат AddEmployeeAsync = {success}");
            }
            else
            {
                Console.WriteLine($"SaveEmployee: обновление сотрудника id={SelectedEmployee.Id}");
                success = await _db.UpdateEmployeeAsync(
                    SelectedEmployee.Id, EditLastName, EditFirstName, EditPatronymic, EditPhone,
                    EditDepartment, EditPosition, EditRole, EditLogin, EditPassword);
                Console.WriteLine($"SaveEmployee: результат UpdateEmployeeAsync = {success}");
            }
            
            if (success)
            {
                await LoadEmployeesAsync();
                IsEditMode = false;
                Console.WriteLine("SaveEmployee: успешно сохранено");
            }
            else
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Ошибка", "Не удалось сохранить сотрудника");
                await box.ShowAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SaveEmployee error: {ex.Message}");
            var box = MessageBoxManager.GetMessageBoxStandard("Ошибка", $"Ошибка: {ex.Message}");
            await box.ShowAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    private async Task DeleteEmployee()
    {
        if (SelectedEmployee == null) return;
        
        Console.WriteLine($"DeleteEmployee: попытка удалить сотрудника {SelectedEmployee.FullName} id={SelectedEmployee.Id}");
        
        var box = MessageBoxManager.GetMessageBoxStandard("Подтверждение", $"Удалить сотрудника {SelectedEmployee.FullName}?", ButtonEnum.YesNo);
        var result = await box.ShowAsync();
        
        if (result == ButtonResult.Yes)
        {
            IsLoading = true;
            try
            {
                var success = await _db.DeleteEmployeeAsync(SelectedEmployee.Id);
                Console.WriteLine($"DeleteEmployee: результат DeleteEmployeeAsync = {success}");
                
                if (success)
                {
                    await LoadEmployeesAsync();
                    SelectedEmployee = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteEmployee error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
    
    private async Task ResetPassword()
    {
        if (SelectedEmployee == null) return;
        
        Console.WriteLine($"ResetPassword: сброс пароля для сотрудника id={SelectedEmployee.Id}");
        
        var newPassword = GenerateRandomPassword();
        var success = await _db.ResetPasswordAsync(SelectedEmployee.Id, newPassword);
        
        Console.WriteLine($"ResetPassword: результат ResetPasswordAsync = {success}, новый пароль = {newPassword}");
        
        if (success)
        {
            var box = MessageBoxManager.GetMessageBoxStandard("Новый пароль", $"Временный пароль: {newPassword}");
            await box.ShowAsync();
        }
        else
        {
            var box = MessageBoxManager.GetMessageBoxStandard("Ошибка", "Не удалось сбросить пароль");
            await box.ShowAsync();
        }
    }
    
    private string GenerateRandomPassword()
    {
        var random = new Random();
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        return new string(Enumerable.Repeat(chars, 8).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}