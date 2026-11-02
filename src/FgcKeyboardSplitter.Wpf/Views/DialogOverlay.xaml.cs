using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace FgcKeyboardSplitter.Views;

/// <summary>An arcade-styled modal dialog drawn over the window. One dialog is shown at a time.</summary>
public partial class DialogOverlay : UserControl
{
    private TaskCompletionSource<bool>? _pending;

    public DialogOverlay() => InitializeComponent();

    /// <summary>True when the primary button was chosen (or Enter pressed), false for close, Escape or a newer dialog.</summary>
    public Task<bool> ShowAsync(string title, object body, string primaryText, string closeText, bool danger, IInputElement? focus = null)
    {
        _pending?.TrySetResult(false);
        _pending = new TaskCompletionSource<bool>();

        TitleText.Text = title.ToUpper(CultureInfo.CurrentCulture);
        BodyHost.Content = body;
        PrimaryButton.Content = primaryText.ToUpper(CultureInfo.CurrentCulture);
        PrimaryButton.Style = (Style)FindResource(danger ? "ArcadeDangerButton" : "ArcadePrimaryButton");
        CloseButton.Content = closeText.ToUpper(CultureInfo.CurrentCulture);

        // Default and cancel are only armed while visible, so Enter and Escape do nothing to a hidden dialog.
        PrimaryButton.IsDefault = true;
        CloseButton.IsCancel = true;
        Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(() => (focus ?? PrimaryButton).Focus(), System.Windows.Threading.DispatcherPriority.Input);
        return _pending.Task;
    }

    /// <summary>Chooses the primary button, for example on a double-click inside the dialog body.</summary>
    public void Accept() => Finish(true);

    private void OnPrimary(object sender, RoutedEventArgs e) => Finish(true);

    private void OnClose(object sender, RoutedEventArgs e) => Finish(false);

    private void Finish(bool result)
    {
        PrimaryButton.IsDefault = false;
        CloseButton.IsCancel = false;
        Visibility = Visibility.Collapsed;
        BodyHost.Content = null;
        TaskCompletionSource<bool>? pending = _pending;
        _pending = null;
        pending?.TrySetResult(result);
    }
}
