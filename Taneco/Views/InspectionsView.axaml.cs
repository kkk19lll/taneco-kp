using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Taneco.ViewModels;

namespace Taneco.Views;

public partial class InspectionsView : UserControl
{
    public InspectionsView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        
        // Пытаемся найти родительское окно и передать его в ViewModel
        var window = this.FindAncestorOfType<Window>();
        if (window != null && DataContext is InspectionsViewModel vm)
        {
            vm.SetParentWindow(window);
        }
    }
}