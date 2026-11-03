using System.Windows.Controls;

namespace FgcKeyboardSplitter.Views;

/// <summary>
/// The clickable arcade stick drawing: the d-pad on the lever, the face buttons, bumpers, triggers and stick clicks
/// as round buttons. Its DataContext is the <see cref="ViewModels.MappingViewModel"/>.
/// </summary>
public partial class ArcadeStickDiagram : UserControl
{
    public ArcadeStickDiagram() => InitializeComponent();
}
