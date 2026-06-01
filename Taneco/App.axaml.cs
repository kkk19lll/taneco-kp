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
                if (user != null)  // Добавляем проверку на null
                {
                    var mainWindow = new MainWindow { DataContext = new MainWindowViewModel(user) };
                    mainWindow.Show();
                    loginWindow.Close();
                }
            };
            
            desktop.MainWindow = loginWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}