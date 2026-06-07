using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Taneco.Views;

public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
    }
    
    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        
        if (DataContext is ViewModels.ReportsViewModel viewModel && e.Root is Window window)
        {
            viewModel.SetParentWindow(window);
        }
    }
}