using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Taneco.Models;
using Taneco.ViewModels;

namespace Taneco.Views;

public partial class RepairsView : UserControl
{
    public RepairsView()
    {
        InitializeComponent();
    }

    private void OnRepairButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Repair repair)
        {
            var viewModel = DataContext as RepairsViewModel;
            if (viewModel != null)
            {
                // Get the parent window to use as owner
                var parentWindow = FindParentWindow(this);
                viewModel.OnRepairClickFromView(repair, parentWindow);
            }
        }
    }

    private Window? FindParentWindow(Avalonia.Controls.Control control)
    {
        try
        {
            var parent = control.Parent;
            while (parent != null)
            {
                if (parent is Window window)
                    return window;
                parent = parent.Parent;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error finding parent window: {ex.Message}");
        }
        return null;
    }
}