using System.Windows;
using System.Windows.Controls;
using FgcKeyboardSplitter.ViewModels;

namespace FgcKeyboardSplitter.Views;

public partial class MappingPage : Page
{
    // Below this the diagrams stop shrinking and the page scrolls instead, so they stay readable.
    private const double MinDiagramHeight = 260;

    public MappingPage(MappingViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        Loaded += (_, _) => viewModel.SetPageVisible(true);
        Unloaded += (_, _) =>
        {
            // A binding left open would keep swallowing keys after the user navigates away.
            viewModel.CancelBinding();
            viewModel.SetPageVisible(false);
        };
        LayoutUpdated += (_, _) => FitDiagrams();
    }

    /// <summary>The keyboard menu opens below its arrow on a left click (a context menu otherwise needs a right click).</summary>
    private void OnKeyboardMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    /// <summary>
    /// The diagrams scale to the page width, and inside the scrolling page nothing limits their height, so a short
    /// window would cut off their bottom. Limiting them to the height left below the profile card keeps the whole
    /// drawing in view at any window size.
    /// </summary>
    private void FitDiagrams()
    {
        if (Scroller.Content is not UIElement content || Scroller.ViewportHeight <= 0)
        {
            return;
        }

        foreach (FrameworkElement diagram in new FrameworkElement[] { Gamepad, Arcade })
        {
            if (!diagram.IsVisible)
            {
                continue;
            }

            double top = diagram.TranslatePoint(default, content).Y;
            double height = Math.Max(MinDiagramHeight, Scroller.ViewportHeight - top - diagram.Margin.Bottom);
            if (Math.Abs(diagram.MaxHeight - height) > 1)
            {
                diagram.MaxHeight = height; // changes only the diagram's own size, so this settles after one pass
            }
        }
    }
}
