using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FgcKeyboardSplitter.Services;
using FgcKeyboardSplitter.Views;
using Wpf.Ui.Controls;

namespace FgcKeyboardSplitter;

public partial class MainWindow : FluentWindow, INavigator
{
    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(nameof(IsCompact), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

    // Below this width (in device-independent pixels) the sidebar shrinks to icons.
    private const double CompactSidebarWidth = 1000;
    private const double WideSidebar = 236;
    private const double NarrowSidebar = 84;

    private readonly Dictionary<Type, RadioButton> _navButtons;
    private IReadOnlyDictionary<Type, object> _pages = new Dictionary<Type, object>();
    private UiScaleService? _scale;

    public MainWindow()
    {
        InitializeComponent();
        _navButtons = new Dictionary<Type, RadioButton>
        {
            [typeof(KeyboardsPage)] = NavKeyboards,
            [typeof(MappingPage)] = NavMapping,
            [typeof(LatencyPage)] = NavLatency,
            [typeof(DriversPage)] = NavDrivers,
        };
        // Pages are reused, so the frame's back history would only keep them alive twice.
        PageHost.Navigated += (_, _) =>
        {
            while (PageHost.CanGoBack)
            {
                PageHost.RemoveBackEntry();
            }
        };
        FitToWorkArea();
        SizeChanged += (_, _) => UpdateSidebar();
    }

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public void SetPages(IReadOnlyDictionary<Type, object> pages)
    {
        _pages = pages;
        Loaded += (_, _) => Navigate(typeof(KeyboardsPage));
    }

    public ContentDialogHost DialogHost => RootDialogHost;

    public void ShowDrivers() => Navigate(typeof(DriversPage));

    public void ShowMapping() => Navigate(typeof(MappingPage));

    public void UseScale(UiScaleService scale)
    {
        _scale = scale;
        ApplyScale();
        scale.PropertyChanged += (_, _) => ApplyScale();
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewMouseWheel += OnPreviewMouseWheel;
    }

    private void Navigate(Type pageType)
    {
        if (_navButtons.TryGetValue(pageType, out RadioButton? button))
        {
            button.IsChecked = true; // raises OnNavChecked, which shows the page
        }
    }

    private void OnNavChecked(object sender, RoutedEventArgs e)
    {
        Type? pageType = _navButtons.FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (pageType is not null && _pages.TryGetValue(pageType, out object? page) && !ReferenceEquals(PageHost.Content, page))
        {
            PageHost.Navigate(page);
        }
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
        UpdateSidebar();
    }

    private void UpdateSidebar()
    {
        if (ActualWidth <= 0)
        {
            return;
        }

        IsCompact = ActualWidth / (_scale?.Scale ?? 1.0) < CompactSidebarWidth;
        Sidebar.Width = IsCompact ? NarrowSidebar : WideSidebar;
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
