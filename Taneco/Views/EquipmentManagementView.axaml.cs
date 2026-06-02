// Views/EquipmentManagementView.axaml.cs (без изменений, но для полноты)
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Taneco.ViewModels;

namespace Taneco.Views;

public partial class EquipmentManagementView : UserControl
{
    private EquipmentManagementViewModel? _viewModel;

    public EquipmentManagementView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        _viewModel = DataContext as EquipmentManagementViewModel;
    }

    private async void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_viewModel == null) return;
        
        var textBox = sender as TextBox;
        if (textBox == null) return;
        
        if (string.IsNullOrWhiteSpace(textBox.Text))
        {
            await _viewModel.LoadEquipmentAsync();
        }
        else
        {
            await Task.Delay(300);
            if (textBox.Text == _viewModel.SearchText)
            {
                _viewModel.SearchText = textBox.Text;
            }
        }
    }
}