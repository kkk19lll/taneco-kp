using System;
using Avalonia.Controls;
using Taneco.ViewModels;

namespace Taneco.Views;

public partial class MonitoringView : UserControl
{
    public MonitoringView()
    {
        InitializeComponent();

        // Подписываемся на событие загрузки
        this.Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MonitoringViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }
}