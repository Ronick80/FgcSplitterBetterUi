using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FgcKeyboardSplitter.ViewModels;

namespace FgcKeyboardSplitter.Views;

/// <summary>The clickable controller drawing; its DataContext is the <see cref="MappingViewModel"/>.</summary>
public partial class GamepadDiagram : UserControl
{
    public GamepadDiagram() => InitializeComponent();

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
