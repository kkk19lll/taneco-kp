using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Taneco.Models;
using Taneco.Services;

namespace Taneco.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private string _login = string.Empty;
    private string _password = string.Empty;
    private bool _isLoading;
    private string _statusMessage = string.Empty;
    
    private readonly DatabaseService _databaseService;
    
    public event Action<User?> OnLoginSuccess = delegate { };
    
    public LoginViewModel()
    {
        _databaseService = new DatabaseService();
        LoginCommand = new RelayCommand(async _ => await ExecuteLogin(), _ => CanExecuteLogin());
        CancelCommand = new RelayCommand(_ => Environment.Exit(0));
    }
    
    public string Login
    {
        get => _login;
        set 
        { 
            if (SetProperty(ref _login, value))
                ((RelayCommand)LoginCommand).RaiseCanExecuteChanged();
        }
    }
    
    public string Password
    {
        get => _password;
        set 
        { 
            if (SetProperty(ref _password, value))
                ((RelayCommand)LoginCommand).RaiseCanExecuteChanged();
        }
    }
    
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }
    
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }
    
    public ICommand LoginCommand { get; }
    public ICommand CancelCommand { get; }
    
    private bool CanExecuteLogin()
    {
        return !IsLoading && !string.IsNullOrWhiteSpace(Login) && !string.IsNullOrWhiteSpace(Password);
    }
    
    private async Task ExecuteLogin()
    {
        IsLoading = true;
        StatusMessage = "⏳ Проверка учетных данных...";
        
        try
        {
            var user = await _databaseService.AuthenticateAsync(Login, Password);
            
            if (user != null)
            {
                StatusMessage = $"✅ Добро пожаловать, {user.FirstName}!";
                await Task.Delay(500);
                OnLoginSuccess?.Invoke(user);
            }
            else
            {
                StatusMessage = "❌ Неверный логин или пароль";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Ошибка: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            ((RelayCommand)LoginCommand).RaiseCanExecuteChanged();
        }
    }
}