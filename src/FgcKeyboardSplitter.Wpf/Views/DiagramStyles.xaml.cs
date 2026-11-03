using System.Windows;
using System.Windows.Input;
using FgcKeyboardSplitter.ViewModels;

namespace FgcKeyboardSplitter.Views;

/// <summary>Styles shared by <see cref="GamepadDiagram"/> and <see cref="ArcadeStickDiagram"/>.</summary>
public partial class DiagramStyles : ResourceDictionary
{
    public DiagramStyles() => InitializeComponent();

    // Hovering any part of an input lights up its drawing and its key label together.
    private void OnHotspotEnter(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MappingRow row })
        {
            row.IsHot = true;
        }
    }

    private void OnHotspotLeave(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MappingRow row })
        {
            row.IsHot = false;
        }
    }
}
