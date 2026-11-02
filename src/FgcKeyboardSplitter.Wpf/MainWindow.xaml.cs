using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FgcKeyboardSplitter.Services;
using FgcKeyboardSplitter.Views;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace FgcKeyboardSplitter;

public partial class MainWindow : FluentWindow, INavigator
{
    // Below this width (in device-independent pixels) the navigation pane collapses to icons.
    private const double CompactPaneWidth = 1000;

    private UiScaleService? _scale;
    private bool? _wasNarrow;

    public MainWindow()
    {
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
        FitToWorkArea();
        SizeChanged += (_, _) => UpdatePane();
    }

    public void SetPages(IReadOnlyDictionary<Type, object> pages)
    {
        RootNavigation.SetPageProviderService(new AppPageProvider(pages));
        Loaded += (_, _) => RootNavigation.Navigate(typeof(KeyboardsPage));
    }

    public ContentDialogHost DialogHost => RootDialogHost;

    public void ShowDrivers() => RootNavigation.Navigate(typeof(DriversPage));

    public void ShowMapping() => RootNavigation.Navigate(typeof(MappingPage));

    public void UseScale(UiScaleService scale)
    {
        _scale = scale;
        ApplyScale();
        scale.PropertyChanged += (_, _) => ApplyScale();
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewMouseWheel += OnPreviewMouseWheel;
    }

    /// <summary>Keeps the first window inside small laptop screens, which may be only about 1280 x 680 usable.</summary>
    private void FitToWorkArea()
    {
        Rect area = SystemParameters.WorkArea;
        Width = Math.Min(Width, area.Width * 0.94);
        Height = Math.Min(Height, area.Height * 0.94);
        MinWidth = Math.Min(MinWidth, area.Width);
        MinHeight = Math.Min(MinHeight, area.Height);
    }

    private void ApplyScale()
    {
        double factor = _scale?.Scale ?? 1.0;
        ScaleRoot.LayoutTransform = Math.Abs(factor - 1.0) < 0.001 ? Transform.Identity : new ScaleTransform(factor, factor);
        UpdatePane();
    }

    private void UpdatePane()
    {
        // Only act when crossing the threshold, so the user can still open or close the pane by hand.
        bool narrow = ActualWidth / (_scale?.Scale ?? 1.0) < CompactPaneWidth;
        if (narrow != _wasNarrow && ActualWidth > 0)
        {
            RootNavigation.IsPaneOpen = !narrow;
            _wasNarrow = narrow;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_scale is null || Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.OemPlus or Key.Add:
                _scale.ZoomIn();
                e.Handled = true;
                break;
            case Key.OemMinus or Key.Subtract:
                _scale.ZoomOut();
                e.Handled = true;
                break;
            case Key.D0 or Key.NumPad0:
                _scale.Reset();
                e.Handled = true;
                break;
        }
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_scale is null || Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        if (e.Delta > 0)
        {
            _scale.ZoomIn();
        }
        else
        {
            _scale.ZoomOut();
        }

        e.Handled = true;
    }
}
