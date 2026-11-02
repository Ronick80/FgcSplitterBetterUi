using System.Windows;
using System.Windows.Interop;
using Fgc.Interop;

namespace FgcKeyboardSplitter.Services;

/// <summary>
/// Reads raw scan codes from the window's key messages while a binding is being captured. While capturing, every
/// key press is swallowed so Tab, Enter, Alt and the arrow keys cannot also navigate or activate controls.
/// </summary>
public sealed class KeyCaptureService
{
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int AutoRepeatBit = 1 << 30;

    public bool IsCapturing { get; set; }

    public event Action<ushort>? KeyPressed;

    /// <summary>Raised when capturing should stop without a key, for example because the window lost focus.</summary>
    public event Action? Cancelled;

    public void Attach(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            source?.AddHook(OnMessage);
        };
        window.Deactivated += (_, _) => Cancel();
    }

    public void Cancel()
    {
        if (IsCapturing)
        {
            Cancelled?.Invoke();
        }
    }

    private nint OnMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (IsCapturing && message is WmKeyDown or WmSysKeyDown)
        {
            handled = true;
            if ((lParam & AutoRepeatBit) == 0)
            {
                KeyPressed?.Invoke(KeyCode.FromKeyMessage(lParam));
            }
        }

        return 0;
    }
}
