using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Taneco.ViewModels;

namespace Taneco.Views;

public partial class RepairDetailsWindow : Window
{
    public RepairDetailsWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is RepairDetailsViewModel viewModel)
        {
            viewModel.SetParentWindow(this);
        }
    }
}