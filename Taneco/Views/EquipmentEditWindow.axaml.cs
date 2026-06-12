using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Taneco.Views;

public partial class EquipmentEditWindow : Window
{
    public bool IsResult { get; private set; }
    public bool IsClosed { get; private set; }

    public EquipmentEditWindow()
    {
        InitializeComponent();
        this.Closed += OnClosed;
    }

    private void OnClosed(object? sender, System.EventArgs e)
    {
        IsClosed = true;
        this.Closed -= OnClosed;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public new void Close(bool result)
    {
        IsResult = result;
        IsClosed = true;
        base.Close();
    }
}