using System.Windows;
using System.Windows.Interop;
using Fgc.Interop;

namespace FgcKeyboardSplitter.Services;

/// <summary>Source of raw key presses while the user binds a key.</summary>
public interface IKeyCapture
{
    bool IsCapturing { get; set; }

    event Action<ushort>? KeyPressed;

    /// <summary>Raised when capturing should stop without a key, for example because the window lost focus.</summary>
    event Action? Cancelled;
}

/// <summary>
/// Reads raw scan codes from the window's key messages while a binding is being captured. While capturing, every
/// key press is swallowed so Tab, Enter, Alt and the arrow keys cannot also navigate or activate controls.
/// </summary>
/// <remarks>
/// WPF turns key messages into input (including Tab navigation) before the window procedure runs, so a window
/// hook sees them too late. The thread message filter runs first, and handling a message there stops WPF from
/// processing or dispatching it.
/// </remarks>
public sealed class KeyCaptureService : IKeyCapture
{
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int AutoRepeatBit = 1 << 30;

    private nint _windowHandle;

    public bool IsCapturing { get; set; }

    public event Action<ushort>? KeyPressed;

    public event Action? Cancelled;

    public void Attach(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            _windowHandle = new WindowInteropHelper(window).Handle;
            ComponentDispatcher.ThreadFilterMessage += OnThreadMessage;
        };
        window.Closed += (_, _) => ComponentDispatcher.ThreadFilterMessage -= OnThreadMessage;
        window.Deactivated += (_, _) => Cancel();
    }

    public void Cancel()
    {
        if (IsCapturing)
        {
            Cancelled?.Invoke();
        }
    }

    private void OnThreadMessage(ref MSG msg, ref bool handled)
    {
        if (handled || !IsCapturing || msg.hwnd != _windowHandle || msg.message is not (WmKeyDown or WmSysKeyDown))
        {
            return;
        }

        handled = true;
        if ((msg.lParam & AutoRepeatBit) == 0)
        {
            KeyPressed?.Invoke(KeyCode.FromKeyMessage(msg.lParam));
        }
    }
}
