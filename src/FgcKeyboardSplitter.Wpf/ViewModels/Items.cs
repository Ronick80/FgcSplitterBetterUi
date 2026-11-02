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

public sealed partial class KeyboardItem(string hardwareId, string displayName) : ObservableObject
{
    public string HardwareId { get; } = hardwareId;

    public string DisplayName { get; } = displayName;

    // The engine names keyboards "Keyboard N - <hardware id>"; show the two parts on separate lines.
    public string Title => DisplayName.Contains(" - ", StringComparison.Ordinal) ? DisplayName[..DisplayName.IndexOf(" - ", StringComparison.Ordinal)] : DisplayName;

    public string Subtitle => DisplayName.Contains(" - ", StringComparison.Ordinal) ? DisplayName[(DisplayName.IndexOf(" - ", StringComparison.Ordinal) + 3)..] : HardwareId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(StatusKind))]
    private bool _isRunning;

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
