using Avalonia.Controls;
using Avalonia.Interactivity;
using Taneco.Models;
using Taneco.ViewModels;
using Taneco.Views;

namespace Taneco.Views;

public partial class ProblemsView : UserControl
{
    public ProblemsView()
    {
        InitializeComponent();
    }

    private async void OnProblemClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Problem problem)
        {
            var viewModel = DataContext as ProblemsViewModel;
            if (viewModel != null && viewModel.OpenProblemDetailsCommand.CanExecute(problem))
            {
                viewModel.OpenProblemDetailsCommand.Execute(problem);
            }
        }
    }
}