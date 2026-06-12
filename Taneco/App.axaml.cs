using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Taneco.ViewModels;
using Taneco.Views;

namespace Taneco;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var loginViewModel = new LoginViewModel();
            var loginWindow = new LoginWindow { DataContext = loginViewModel };

            loginViewModel.OnLoginSuccess += (user) =>
            {
                if (user != null)
                {
                    // Создаем главное окно
                    var mainWindow = new MainWindow();
                    // Передаем в MainWindowViewModel текущего пользователя и само окно
                    var mainViewModel = new MainWindowViewModel(user, mainWindow);
                    mainWindow.DataContext = mainViewModel;
                    mainWindow.Show();
                    loginWindow.Close();
                }
            };

            desktop.MainWindow = loginWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}