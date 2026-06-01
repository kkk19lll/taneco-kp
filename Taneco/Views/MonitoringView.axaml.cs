using System;
using Avalonia.Controls;
using Taneco.ViewModels;

namespace Taneco.Views;

public partial class MonitoringView : UserControl
{
    public MonitoringView()
    {
        InitializeComponent();
    }

    private async void OnDateSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MonitoringViewModel vm && sender is ComboBox comboBox && comboBox.SelectedItem is DateTime selectedDate)
        {
            await vm.SelectDate(selectedDate);
        }
    }
}