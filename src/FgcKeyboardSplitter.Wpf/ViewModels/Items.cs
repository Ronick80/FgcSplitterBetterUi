using CommunityToolkit.Mvvm.ComponentModel;

namespace FgcKeyboardSplitter.ViewModels;

public enum StatusKind
{
    Neutral,
    Good,
    Warning,
    Bad,
}

public enum SlotKind
{
    Free,
    Other,
    Mine,
}

/// <summary>One physical keyboard, driven through the interface it joined with (<see cref="HardwareId"/>).</summary>
public sealed partial class KeyboardItem(string hardwareId, string title, string deviceKey = "", string productName = "") : ObservableObject
{
    private DateTime _flashUntil;

    /// <summary>The keyboard interface sessions and profiles use.</summary>
    public string HardwareId { get; } = hardwareId;

    /// <summary>The product name, numbered when identical keyboards are listed: "GMMK PRO #2".</summary>
    public string Title { get; } = title;

    /// <summary>Shared by every interface of this physical keyboard.</summary>
    public string DeviceKey { get; } = deviceKey.Length > 0 ? deviceKey : hardwareId;

    public string ProductName { get; } = productName.Length > 0 ? productName : title;

    public string Subtitle => HardwareId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(StatusKind))]
    private bool _isRunning;

    /// <summary>True for a moment after a key was pressed on this keyboard.</summary>
    [ObservableProperty]
    private bool _isFlashing;

    public void Flash(DateTime until)
    {
        _flashUntil = until;
        IsFlashing = true;
    }

    public void UpdateFlash(DateTime now)
    {
        if (IsFlashing && now >= _flashUntil)
        {
            IsFlashing = false;
        }
    }

    public string StatusText => IsRunning ? "Running" : "Stopped";

    public StatusKind StatusKind => IsRunning ? StatusKind.Good : StatusKind.Neutral;
}

public sealed partial class SlotItem : ObservableObject
{
    public int Number { get; init; }

    public string PlayerLabel => $"P{Number}";

    [ObservableProperty]
    private SlotKind _kind;
}

public sealed record Notice(string Text);
