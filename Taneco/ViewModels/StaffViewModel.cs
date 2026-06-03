using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Taneco.Models;
using Taneco.Services;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Avalonia.Threading;

namespace Taneco.ViewModels;

public class StaffViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private ObservableCollection<Employee> _allActiveEmployees;
    private ObservableCollection<Employee> _allArchivedEmployees;
    private ObservableCollection<Employee> _filteredActiveEmployees;
    private ObservableCollection<Employee> _filteredArchivedEmployees;
    private Employee? _selectedActiveEmployee;
    private Employee? _selectedArchivedEmployee;
    private bool _isLoading;
    private string _searchTextActive = string.Empty;
    private string _searchTextArchive = string.Empty;
    private User? _currentUser;
    private bool _isEditMode;

    private string _editLastName = string.Empty;
    private string _editFirstName = string.Empty;
    private string _editPatronymic = string.Empty;
    private string _editPhoneRaw = string.Empty;
    private string _editDepartment = string.Empty;
    private string _editPosition = string.Empty;
    private string _editRole = string.Empty;
    private string _editLogin = string.Empty;
    private string _editPassword = string.Empty;
    private string _editHireDateRaw = string.Empty;

    private bool _isUpdatingPhone;
    private bool _isUpdatingHireDate;

    public ObservableCollection<string> Departments { get; } = new();
    public ObservableCollection<string> Positions { get; } = new();
    public ObservableCollection<string> AvailableRoles { get; } = new();

    public StaffViewModel()
    {
        _db = new DatabaseService();
        _allActiveEmployees = new ObservableCollection<Employee>();
        _allArchivedEmployees = new ObservableCollection<Employee>();
        _filteredActiveEmployees = new ObservableCollection<Employee>();
        _filteredArchivedEmployees = new ObservableCollection<Employee>();

        RefreshActiveCommand = new RelayCommand(async () => await LoadActiveEmployeesAsync(), () => !IsLoading);
        RefreshArchiveCommand = new RelayCommand(async () => await LoadArchivedEmployeesAsync(), () => !IsLoading);
        AddEmployeeCommand = new RelayCommand(() => StartAdd(), () => CanManageStaff);
        EditCommand = new RelayCommand(() => StartEdit(), () => SelectedActiveEmployee != null && CanManageStaff && !IsEditMode);
        SaveEmployeeCommand = new RelayCommand(async () => await SaveEmployee(), () => IsEditMode);
        CancelEditCommand = new RelayCommand(() => CancelEdit());
        ArchiveEmployeeCommand = new RelayCommand(async () => await ArchiveEmployee(), () => SelectedActiveEmployee != null && CanManageStaff && !IsEditMode);
        RestoreEmployeeCommand = new RelayCommand(async () => await RestoreEmployee(), () => SelectedArchivedEmployee != null && CanManageStaff);
        ResetPasswordCommand = new RelayCommand(async () => await ResetPassword(), () => SelectedActiveEmployee != null && CanManageStaff && !IsEditMode);

        AvailableRoles.Add("Администратор");
        AvailableRoles.Add("HR");
        AvailableRoles.Add("Оператор");
        AvailableRoles.Add("Инженер_КИПиА");
        AvailableRoles.Add("Инспектор");
        AvailableRoles.Add("Начальник_ремонтной_службы");
        AvailableRoles.Add("Аналитик");

        Task.Run(async () => await LoadDepartmentsAsync());
        Task.Run(async () => await LoadActiveEmployeesAsync());
        Task.Run(async () => await LoadArchivedEmployeesAsync());
    }

    public User? CurrentUser
    {
        get => _currentUser;
        set
        {
            SetProperty(ref _currentUser, value);
            OnPropertyChanged(nameof(CanManageStaff));
            ((RelayCommand)RefreshActiveCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RefreshArchiveCommand).RaiseCanExecuteChanged();
            ((RelayCommand)AddEmployeeCommand).RaiseCanExecuteChanged();
            ((RelayCommand)EditCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ArchiveEmployeeCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RestoreEmployeeCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ResetPasswordCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SaveEmployeeCommand).RaiseCanExecuteChanged();
        }
    }

    public ObservableCollection<Employee> ActiveEmployees
    {
        get => _filteredActiveEmployees;
        set => SetProperty(ref _filteredActiveEmployees, value);
    }

    public ObservableCollection<Employee> ArchivedEmployees
    {
        get => _filteredArchivedEmployees;
        set => SetProperty(ref _filteredArchivedEmployees, value);
    }

    public Employee? SelectedActiveEmployee
    {
        get => _selectedActiveEmployee;
        set
        {
            if (SetProperty(ref _selectedActiveEmployee, value))
            {
                OnPropertyChanged(nameof(IsActiveEmployeeSelected));
                OnPropertyChanged(nameof(IsActiveEmployeeNotSelected));
                ((RelayCommand)EditCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ArchiveEmployeeCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ResetPasswordCommand).RaiseCanExecuteChanged();

                if (value != null && !IsEditMode)
                {
                    EditLastName = value.LastName;
                    EditFirstName = value.FirstName;
                    EditPatronymic = value.Patronymic;
                    EditPhoneRaw = CleanPhoneNumber(value.Phone);
                    EditDepartment = value.Department;
                    EditPosition = value.Position;
                    EditRole = value.Role;
                    EditLogin = value.Login;
                    EditPassword = "";
                    EditHireDateRaw = value.HireDate.ToString("yyyy-MM-dd").Replace("-", "");
                }
            }
        }
    }

    public Employee? SelectedArchivedEmployee
    {
        get => _selectedArchivedEmployee;
        set
        {
            if (SetProperty(ref _selectedArchivedEmployee, value))
            {
                OnPropertyChanged(nameof(IsArchivedEmployeeSelected));
                OnPropertyChanged(nameof(IsArchivedEmployeeNotSelected));
                ((RelayCommand)RestoreEmployeeCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string SearchTextActive
    {
        get => _searchTextActive;
        set
        {
            if (SetProperty(ref _searchTextActive, value))
                FilterActiveEmployees();
        }
    }

    public string SearchTextArchive
    {
        get => _searchTextArchive;
        set
        {
            if (SetProperty(ref _searchTextArchive, value))
                FilterArchivedEmployees();
        }
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (SetProperty(ref _isEditMode, value))
            {
                ((RelayCommand)EditCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ArchiveEmployeeCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ResetPasswordCommand).RaiseCanExecuteChanged();
                ((RelayCommand)SaveEmployeeCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanManageStaff => CurrentUser?.CanManageStaff ?? false;
    public bool IsActiveEmployeeSelected => SelectedActiveEmployee != null;
    public bool IsActiveEmployeeNotSelected => SelectedActiveEmployee == null;
    public bool IsArchivedEmployeeSelected => SelectedArchivedEmployee != null;
    public bool IsArchivedEmployeeNotSelected => SelectedArchivedEmployee == null;

    public string EditLastName { get => _editLastName; set => SetProperty(ref _editLastName, value); }
    public string EditFirstName { get => _editFirstName; set => SetProperty(ref _editFirstName, value); }
    public string EditPatronymic { get => _editPatronymic; set => SetProperty(ref _editPatronymic, value); }

    public string EditHireDateDisplay
    {
        get => FormatHireDateForDisplay(_editHireDateRaw);
        set
        {
            if (_isUpdatingHireDate) return;

            _isUpdatingHireDate = true;

            var cleaned = new string(value.Where(c => char.IsDigit(c)).ToArray());

            if (cleaned.Length > 8)
                cleaned = cleaned.Substring(0, 8);

            string formatted = FormatHireDateWithMask(cleaned);

            if (_editHireDateRaw != cleaned)
            {
                _editHireDateRaw = cleaned;
                OnPropertyChanged(nameof(EditHireDateRaw));
            }

            if (value != formatted)
            {
                OnPropertyChanged(nameof(EditHireDateDisplay));
            }

            _isUpdatingHireDate = false;
        }
    }

    public string EditHireDateRaw
    {
        get => _editHireDateRaw;
        set
        {
            if (_isUpdatingHireDate) return;

            _isUpdatingHireDate = true;

            var cleaned = new string(value.Where(c => char.IsDigit(c)).ToArray());
            if (cleaned.Length > 8)
                cleaned = cleaned.Substring(0, 8);

            if (SetProperty(ref _editHireDateRaw, cleaned))
            {
                OnPropertyChanged(nameof(EditHireDateDisplay));
            }

            _isUpdatingHireDate = false;
        }
    }

    private string FormatHireDateWithMask(string digits)
    {
        if (string.IsNullOrEmpty(digits))
            return string.Empty;

        if (digits.Length <= 4)
            return digits;
        else if (digits.Length <= 6)
            return $"{digits.Substring(0, 4)}-{digits.Substring(4)}";
        else
            return $"{digits.Substring(0, 4)}-{digits.Substring(4, 2)}-{digits.Substring(6)}";
    }

    private string FormatHireDateForDisplay(string rawDate)
    {
        if (string.IsNullOrEmpty(rawDate))
            return string.Empty;
        return FormatHireDateWithMask(rawDate);
    }

    private DateTime? ParseHireDateFromString(string dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return null;

        if (Regex.IsMatch(dateString, @"^\d{4}-\d{2}-\d{2}$"))
        {
            if (DateTime.TryParseExact(dateString, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime result))
                return result;
        }

        var digits = new string(dateString.Where(c => char.IsDigit(c)).ToArray());
        if (digits.Length == 8)
        {
            if (DateTime.TryParseExact(digits, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime result))
                return result;
        }

        return null;
    }

    public string EditPhoneDisplay
    {
        get => FormatPhoneForDisplay(_editPhoneRaw);
        set
        {
            if (_isUpdatingPhone) return;

            _isUpdatingPhone = true;

            var cleaned = new string(value.Where(c => char.IsDigit(c)).ToArray());

            if (cleaned.Length > 11)
                cleaned = cleaned.Substring(0, 11);

            if (_editPhoneRaw != cleaned)
            {
                _editPhoneRaw = cleaned;
                OnPropertyChanged(nameof(EditPhoneRaw));
            }

            var formatted = FormatPhoneForDisplay(_editPhoneRaw);
            if (value != formatted)
            {
                OnPropertyChanged(nameof(EditPhoneDisplay));
            }

            _isUpdatingPhone = false;
        }
    }

    public string EditPhoneRaw
    {
        get => _editPhoneRaw;
        set
        {
            if (_isUpdatingPhone) return;

            _isUpdatingPhone = true;

            var cleaned = new string(value.Where(c => char.IsDigit(c)).ToArray());
            if (cleaned.Length > 11)
                cleaned = cleaned.Substring(0, 11);

            if (SetProperty(ref _editPhoneRaw, cleaned))
            {
                OnPropertyChanged(nameof(EditPhoneDisplay));
            }

            _isUpdatingPhone = false;
        }
    }

    public string EditDepartment { get => _editDepartment; set => SetProperty(ref _editDepartment, value); }
    public string EditPosition { get => _editPosition; set => SetProperty(ref _editPosition, value); }
    public string EditRole { get => _editRole; set => SetProperty(ref _editRole, value); }
    public string EditLogin { get => _editLogin; set => SetProperty(ref _editLogin, value); }
    public string EditPassword { get => _editPassword; set => SetProperty(ref _editPassword, value); }

    public ICommand RefreshActiveCommand { get; }
    public ICommand RefreshArchiveCommand { get; }
    public ICommand AddEmployeeCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand SaveEmployeeCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand ArchiveEmployeeCommand { get; }
    public ICommand RestoreEmployeeCommand { get; }
    public ICommand ResetPasswordCommand { get; }

    private string FormatPhoneNumber(string digits)
    {
        if (string.IsNullOrEmpty(digits))
            return string.Empty;

        if (digits.Length == 1)
            return $"+{digits}";
        else if (digits.Length <= 4)
            return $"+{digits[0]} ({digits.Substring(1)})";
        else if (digits.Length <= 7)
            return $"+{digits[0]} ({digits.Substring(1, 3)}) {digits.Substring(4)}";
        else if (digits.Length <= 9)
            return $"+{digits[0]} ({digits.Substring(1, 3)}) {digits.Substring(4, 3)}-{digits.Substring(7)}";
        else if (digits.Length <= 11)
            return $"+{digits[0]} ({digits.Substring(1, 3)}) {digits.Substring(4, 3)}-{digits.Substring(7, 2)}-{digits.Substring(9)}";
        else
            return digits;
    }

    private string FormatPhoneForDisplay(string phone)
    {
        if (string.IsNullOrEmpty(phone))
            return string.Empty;
        var digits = CleanPhoneNumber(phone);
        return FormatPhoneNumber(digits);
    }

    private string CleanPhoneNumber(string phone)
    {
        if (string.IsNullOrEmpty(phone))
            return string.Empty;
        return new string(phone.Where(c => char.IsDigit(c)).ToArray());
    }

    private async Task LoadDepartmentsAsync()
    {
        try
        {
            var depts = await _db.GetDepartmentsAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Departments.Clear();
                foreach (var d in depts)
                    Departments.Add(d);

                if (Departments.Any() && string.IsNullOrEmpty(EditDepartment))
                    EditDepartment = Departments.FirstOrDefault() ?? "";
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadDepartmentsAsync error: {ex.Message}");
        }
    }

    private async Task LoadActiveEmployeesAsync()
    {
        if (IsLoading) return;

        await Dispatcher.UIThread.InvokeAsync(() => IsLoading = true);

        try
        {
            var allEmployees = await _db.GetEmployeesAsync();
            var activeEmployees = allEmployees.Where(e => e.IsActive).ToList();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _allActiveEmployees.Clear();
                foreach (var e in activeEmployees)
                    _allActiveEmployees.Add(e);

                var pos = _allActiveEmployees.Select(e => e.Position).Distinct().ToList();
                Positions.Clear();
                foreach (var p in pos)
                    Positions.Add(p);

                FilterActiveEmployees();
            });

            Console.WriteLine($"LoadActiveEmployeesAsync: загружено {activeEmployees.Count} активных сотрудников");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadActiveEmployees error: {ex.Message}");
            await ShowMessageBox("Ошибка", $"Не удалось загрузить сотрудников: {ex.Message}", ButtonEnum.Ok);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
        }
    }

    private async Task LoadArchivedEmployeesAsync()
    {
        if (IsLoading) return;

        await Dispatcher.UIThread.InvokeAsync(() => IsLoading = true);

        try
        {
            var allEmployees = await _db.GetEmployeesAsync();
            var archivedEmployees = allEmployees.Where(e => !e.IsActive).ToList();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _allArchivedEmployees.Clear();
                foreach (var e in archivedEmployees)
                    _allArchivedEmployees.Add(e);

                FilterArchivedEmployees();
            });

            Console.WriteLine($"LoadArchivedEmployeesAsync: загружено {archivedEmployees.Count} архивных сотрудников");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadArchivedEmployees error: {ex.Message}");
            await ShowMessageBox("Ошибка", $"Не удалось загрузить архив: {ex.Message}", ButtonEnum.Ok);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
        }
    }

    private void FilterActiveEmployees()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchTextActive)
            ? _allActiveEmployees
            : new ObservableCollection<Employee>(
                _allActiveEmployees.Where(e => e.LastName.ToLower().Contains(SearchTextActive.ToLower()) ||
                                                 e.FirstName.ToLower().Contains(SearchTextActive.ToLower()) ||
                                                 e.Position.ToLower().Contains(SearchTextActive.ToLower()))
            );

        ActiveEmployees.Clear();
        foreach (var e in filtered)
            ActiveEmployees.Add(e);
    }

    private void FilterArchivedEmployees()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchTextArchive)
            ? _allArchivedEmployees
            : new ObservableCollection<Employee>(
                _allArchivedEmployees.Where(e => e.LastName.ToLower().Contains(SearchTextArchive.ToLower()) ||
                                                  e.FirstName.ToLower().Contains(SearchTextArchive.ToLower()) ||
                                                  e.Position.ToLower().Contains(SearchTextArchive.ToLower()))
            );

        ArchivedEmployees.Clear();
        foreach (var e in filtered)
            ArchivedEmployees.Add(e);
    }

    private void StartAdd()
    {
        SelectedActiveEmployee = null;
        EditLastName = "";
        EditFirstName = "";
        EditPatronymic = "";
        EditPhoneRaw = "";
        EditDepartment = Departments.FirstOrDefault() ?? "";
        EditPosition = Positions.FirstOrDefault() ?? "";
        EditRole = "Оператор";
        EditLogin = "";
        EditPassword = "";
        EditHireDateRaw = DateTime.Today.ToString("yyyyMMdd");
        IsEditMode = true;
    }

    private void StartEdit()
    {
        if (SelectedActiveEmployee != null)
        {
            IsEditMode = true;
        }
    }

    private void CancelEdit()
    {
        IsEditMode = false;
        if (SelectedActiveEmployee != null)
        {
            EditLastName = SelectedActiveEmployee.LastName;
            EditFirstName = SelectedActiveEmployee.FirstName;
            EditPatronymic = SelectedActiveEmployee.Patronymic;
            EditPhoneRaw = CleanPhoneNumber(SelectedActiveEmployee.Phone);
            EditDepartment = SelectedActiveEmployee.Department;
            EditPosition = SelectedActiveEmployee.Position;
            EditRole = SelectedActiveEmployee.Role;
            EditLogin = SelectedActiveEmployee.Login;
            EditHireDateRaw = SelectedActiveEmployee.HireDate.ToString("yyyyMMdd");
        }
        EditPassword = "";
    }

    private async Task ShowMessageBox(string title, string message, ButtonEnum buttons = ButtonEnum.Ok)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var box = MessageBoxManager.GetMessageBoxStandard(title, message, buttons);
            await box.ShowAsync();
        });
    }

    private async Task SaveEmployee()
    {
        if (string.IsNullOrWhiteSpace(EditLastName))
        {
            await ShowMessageBox("Ошибка", "Фамилия обязательна");
            return;
        }

        if (string.IsNullOrWhiteSpace(EditFirstName))
        {
            await ShowMessageBox("Ошибка", "Имя обязательно");
            return;
        }

        var hireDate = ParseHireDateFromString(EditHireDateRaw);
        if (hireDate == null)
        {
            await ShowMessageBox("Ошибка", "Введите корректную дату в формате ГГГГ-ММ-ДД (например, 2024-01-15)");
            return;
        }

        if (!DatabaseService.IsHireDateValid(hireDate.Value))
        {
            await ShowMessageBox("Ошибка", DatabaseService.GetHireDateErrorMessage());
            return;
        }

        if (SelectedActiveEmployee == null)
        {
            if (string.IsNullOrWhiteSpace(EditPassword))
            {
                await ShowMessageBox("Ошибка", "Пароль обязателен для нового сотрудника");
                return;
            }

            if (!DatabaseService.IsPasswordValid(EditPassword))
            {
                await ShowMessageBox("Ошибка", DatabaseService.GetPasswordErrorMessage());
                return;
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(EditPassword))
            {
                if (!DatabaseService.IsPasswordValid(EditPassword))
                {
                    await ShowMessageBox("Ошибка", DatabaseService.GetPasswordErrorMessage());
                    return;
                }
            }
        }

        string cleanPhone = EditPhoneRaw;

        if (EditRole == "Администратор")
        {
            var currentAdminCount = _allActiveEmployees.Count(e => e.Role == "Администратор" && e.Id != (SelectedActiveEmployee?.Id ?? 0));
            if (currentAdminCount >= 2)
            {
                await ShowMessageBox("Ошибка", "Не может быть больше 2 администраторов");
                return;
            }
        }

        if (EditRole == "HR")
        {
            var currentHrCount = _allActiveEmployees.Count(e => e.Role == "HR" && e.Id != (SelectedActiveEmployee?.Id ?? 0));
            if (currentHrCount >= 1)
            {
                await ShowMessageBox("Ошибка", "HR может быть только один");
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(EditLogin))
        {
            await ShowMessageBox("Ошибка", "Логин обязателен");
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() => IsLoading = true);

        try
        {
            bool success;

            if (SelectedActiveEmployee == null)
            {
                success = await _db.AddEmployeeAsync(
                    EditLastName, EditFirstName, EditPatronymic, cleanPhone,
                    EditDepartment, EditPosition, EditRole, EditLogin, EditPassword, hireDate.Value);
            }
            else
            {
                success = await _db.UpdateEmployeeAsync(
                    SelectedActiveEmployee.Id, EditLastName, EditFirstName, EditPatronymic, cleanPhone,
                    EditDepartment, EditPosition, EditRole, EditLogin, EditPassword, hireDate.Value);
            }

            if (success)
            {
                await LoadActiveEmployeesAsync();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsEditMode = false;

                    if (SelectedActiveEmployee == null)
                    {
                        SelectedActiveEmployee = null;
                    }
                    else
                    {
                        var updatedEmployee = _allActiveEmployees.FirstOrDefault(e => e.Id == SelectedActiveEmployee.Id);
                        if (updatedEmployee != null)
                        {
                            SelectedActiveEmployee = updatedEmployee;
                        }
                    }
                });

                await ShowMessageBox("Успех",
                    SelectedActiveEmployee == null ? "Сотрудник успешно добавлен" : "Данные сотрудника обновлены");
            }
            else
            {
                await ShowMessageBox("Ошибка", "Не удалось сохранить сотрудника");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SaveEmployee error: {ex.Message}");
            await ShowMessageBox("Ошибка", $"Ошибка: {ex.Message}");
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
        }
    }

    private async Task ArchiveEmployee()
    {
        if (SelectedActiveEmployee == null) return;

        var result = ButtonResult.None;
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var box = MessageBoxManager.GetMessageBoxStandard("Подтверждение архивирования",
                $"Вы действительно хотите переместить сотрудника {SelectedActiveEmployee.FullName} в архив?\n\nСотрудник больше не сможет войти в систему.",
                ButtonEnum.YesNo);
            result = await box.ShowAsync();
        });

        if (result == ButtonResult.Yes)
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsLoading = true);

            try
            {
                var success = await _db.DeleteEmployeeAsync(SelectedActiveEmployee.Id);

                if (success)
                {
                    await LoadActiveEmployeesAsync();
                    await LoadArchivedEmployeesAsync();

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        SelectedActiveEmployee = null;
                    });

                    await ShowMessageBox("Успех", "Сотрудник перемещен в архив");
                }
                else
                {
                    await ShowMessageBox("Ошибка", "Не удалось архивировать сотрудника");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ArchiveEmployee error: {ex.Message}");
                await ShowMessageBox("Ошибка", $"Ошибка: {ex.Message}");
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
            }
        }
    }

    private async Task RestoreEmployee()
    {
        if (SelectedArchivedEmployee == null) return;

        var result = ButtonResult.None;
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var box = MessageBoxManager.GetMessageBoxStandard("Подтверждение восстановления",
                $"Восстановить сотрудника {SelectedArchivedEmployee.FullName}?",
                ButtonEnum.YesNo);
            result = await box.ShowAsync();
        });

        if (result == ButtonResult.Yes)
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsLoading = true);

            try
            {
                var success = await _db.RestoreEmployeeAsync(SelectedArchivedEmployee.Id);

                if (success)
                {
                    await LoadActiveEmployeesAsync();
                    await LoadArchivedEmployeesAsync();

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        SelectedArchivedEmployee = null;
                    });

                    await ShowMessageBox("Успех", "Сотрудник восстановлен из архива");
                }
                else
                {
                    await ShowMessageBox("Ошибка", "Не удалось восстановить сотрудника");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RestoreEmployee error: {ex.Message}");
                await ShowMessageBox("Ошибка", $"Ошибка: {ex.Message}");
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
            }
        }
    }

    private async Task ResetPassword()
    {
        if (SelectedActiveEmployee == null) return;

        var result = ButtonResult.None;
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var box = MessageBoxManager.GetMessageBoxStandard("Подтверждение",
                $"Сбросить пароль для сотрудника {SelectedActiveEmployee.FullName}?",
                ButtonEnum.YesNo);
            result = await box.ShowAsync();
        });

        if (result == ButtonResult.Yes)
        {
            var newPassword = GenerateRandomPassword();

            if (!DatabaseService.IsPasswordValid(newPassword))
            {
                await ShowMessageBox("Ошибка", DatabaseService.GetPasswordErrorMessage());
                return;
            }

            var success = await _db.ResetPasswordAsync(SelectedActiveEmployee.Id, newPassword);

            if (success)
            {
                await ShowMessageBox("Новый пароль",
                    $"Временный пароль для {SelectedActiveEmployee.FullName}:\n\n{newPassword}\n\nСохраните этот пароль и передайте сотруднику.");
            }
            else
            {
                await ShowMessageBox("Ошибка", "Не удалось сбросить пароль");
            }
        }
    }

    private string GenerateRandomPassword()
    {
        var random = new Random();
        const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowercase = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string special = "!@$%^?_";

        var password = new char[10];
        password[0] = uppercase[random.Next(uppercase.Length)];
        password[1] = lowercase[random.Next(lowercase.Length)];
        password[2] = digits[random.Next(digits.Length)];
        password[3] = special[random.Next(special.Length)];

        const string allChars = uppercase + lowercase + digits + special;
        for (int i = 4; i < password.Length; i++)
        {
            password[i] = allChars[random.Next(allChars.Length)];
        }

        return new string(password.OrderBy(x => random.Next()).ToArray());
    }
}