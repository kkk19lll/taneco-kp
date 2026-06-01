using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Taneco.Models;
using Taneco.Services;

namespace Taneco.ViewModels;

public class AdminViewModel : ViewModelBase
{
    private readonly DatabaseService _db;
    private bool _isLoading;
    private User? _currentUser;
    private string _connectionStatus = "Проверка...";
    private string _connectionColor = "#F39C12";
    private Dictionary<string, int> _roleStats = new();

    public AdminViewModel()
    {
        _db = new DatabaseService();
        CheckConnectionCommand = new RelayCommand(async _ => await CheckConnectionAsync(), _ => !IsLoading);
        LoadRoleStatsCommand = new RelayCommand(async _ => await LoadRoleStatsAsync(), _ => !IsLoading);

        Task.Run(async () =>
        {
            await CheckConnectionAsync();
            await LoadRoleStatsAsync();
        });
    }

    public User? CurrentUser
    {
        get => _currentUser;
        set => SetProperty(ref _currentUser, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        set => SetProperty(ref _connectionStatus, value);
    }

    public string ConnectionColor
    {
        get => _connectionColor;
        set => SetProperty(ref _connectionColor, value);
    }

    public Dictionary<string, int> RoleStats
    {
        get => _roleStats;
        set => SetProperty(ref _roleStats, value);
    }

    public ICommand CheckConnectionCommand { get; }
    public ICommand LoadRoleStatsCommand { get; }
    public ICommand CreateBackupCommand { get; }

    private async Task CheckConnectionAsync()
    {
        IsLoading = true;
        try
        {
            var isConnected = await _db.TestConnectionAsync();
            if (isConnected)
            {
                ConnectionStatus = "✅ PostgreSQL: ПОДКЛЮЧЕНО";
                ConnectionColor = "#4CAF50";
            }
            else
            {
                ConnectionStatus = "❌ PostgreSQL: НЕТ ПОДКЛЮЧЕНИЯ";
                ConnectionColor = "#E74C3C";
            }
        }
        catch
        {
            ConnectionStatus = "❌ PostgreSQL: ОШИБКА ПОДКЛЮЧЕНИЯ";
            ConnectionColor = "#E74C3C";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadRoleStatsAsync()
    {
        try
        {
            RoleStats = await _db.GetRoleStatisticsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadRoleStats error: {ex.Message}");
        }
    }
}