using CommunityToolkit.Mvvm.ComponentModel;

namespace FgcKeyboardSplitter.ViewModels;

/// <summary>The keyboard chosen on the Keyboards page, shared with the pages that edit it.</summary>
public sealed partial class KeyboardSelection : ObservableObject
{
    [ObservableProperty]
    private KeyboardItem? _selected;
}
