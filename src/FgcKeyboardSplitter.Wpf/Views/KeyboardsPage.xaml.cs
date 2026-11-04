using System.Windows;
using System.Windows.Controls;
using FgcKeyboardSplitter.ViewModels;

namespace FgcKeyboardSplitter.Views;

public partial class KeyboardsPage : Page
{
    private readonly KeyboardsViewModel _viewModel;
    private Window? _window;
    private bool _shown;

    public KeyboardsPage(KeyboardsViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        // Key presses are only watched for while this page is in front and the window is active, so playing a game
        // with the app left on this page does not route idle keyboards through the engine.
        Loaded += (_, _) =>
        {
            _shown = true;
            Attach(Window.GetWindow(this));
            Update();
        };
        Unloaded += (_, _) =>
        {
            _shown = false;
            Update();
        };
    }

    private void Attach(Window? window)
    {
        if (ReferenceEquals(window, _window))
        {
            return;
        }

        if (_window is not null)
        {
            _window.Activated -= OnWindowActivation;
            _window.Deactivated -= OnWindowActivation;
        }

        _window = window;
        if (_window is not null)
        {
            _window.Activated += OnWindowActivation;
            _window.Deactivated += OnWindowActivation;
        }
    }

    private void OnWindowActivation(object? sender, EventArgs e) => Update();

    private void Update() => _viewModel.SetActive(_shown && _window is { IsActive: true });
}
