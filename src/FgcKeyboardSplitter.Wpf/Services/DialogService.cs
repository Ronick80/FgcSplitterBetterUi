using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Fgc.Interop;
using FgcKeyboardSplitter.Views;

namespace FgcKeyboardSplitter.Services;

public interface IDialogService
{
    /// <summary>Asks for a line of text. Null when cancelled. A non-null error is shown under the box.</summary>
    Task<string?> PromptTextAsync(string title, string label, string initial, string? error = null);

    /// <summary>Asks before something destructive; the confirm button is red.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmText);

    /// <summary>Lets the user pick a key from the list. Null when cancelled; <see cref="KeyCode.Unbound"/> for "(none)".</summary>
    Task<ushort?> PickKeyAsync(string title, ushort current);
}

public sealed class DialogService(DialogOverlay overlay) : IDialogService
{
    private IReadOnlyList<KeyChoice>? _choices;

    public async Task<string?> PromptTextAsync(string title, string label, string initial, string? error = null)
    {
        var input = new TextBox { Text = initial, MinWidth = 360, MaxLength = 63 };
        input.GotKeyboardFocus += (_, _) => input.SelectAll();

        var content = new StackPanel();
        content.Children.Add(Body(label, "ArcadeTextDim"));
        content.Children.Add(input);
        if (!string.IsNullOrEmpty(error))
        {
            TextBlock problem = Body(error, "ArcadeRed");
            problem.Margin = new Thickness(0, 10, 0, 0);
            content.Children.Add(problem);
        }

        return await overlay.ShowAsync(title, content, "OK", "Cancel", danger: false, focus: input) ? input.Text : null;
    }

    public Task<bool> ConfirmAsync(string title, string message, string confirmText) =>
        overlay.ShowAsync(title, Body(message, "ArcadeText"), confirmText, "Cancel", danger: true);

    public async Task<ushort?> PickKeyAsync(string title, ushort current)
    {
        _choices ??= BuildChoices();
        var list = new ListBox
        {
            ItemsSource = _choices,
            DisplayMemberPath = nameof(KeyChoice.Name),
            Height = 360,
            MinWidth = 320,
            Style = (Style)Application.Current.FindResource("ArcadePickList"),
            ItemContainerStyle = (Style)Application.Current.FindResource("ArcadePickItem"),
            SelectedItem = _choices.FirstOrDefault(choice => choice.Code == current),
        };
        list.Loaded += (_, _) =>
        {
            if (list.SelectedItem is not null)
            {
                list.ScrollIntoView(list.SelectedItem);
            }
        };
        list.MouseDoubleClick += (_, e) =>
        {
            if (list.SelectedItem is not null && e.ChangedButton == MouseButton.Left)
            {
                overlay.Accept();
            }
        };

        bool chosen = await overlay.ShowAsync(title, list, "Use this key", "Cancel", danger: false, focus: list);
        return chosen && list.SelectedItem is KeyChoice choice ? choice.Code : null;
    }

    private static TextBlock Body(string text, string brushKey) => new()
    {
        Text = text,
        Margin = new Thickness(0, 0, 0, 8),
        MaxWidth = 520,
        HorizontalAlignment = HorizontalAlignment.Left,
        Style = (Style)Application.Current.FindResource("ArcadeBody"),
        Foreground = (System.Windows.Media.Brush)Application.Current.FindResource(brushKey),
    };

    private static IReadOnlyList<KeyChoice> BuildChoices()
    {
        var choices = new List<KeyChoice> { new(KeyCode.Unbound, KeyNames.GetName(KeyCode.Unbound)) };
        choices.AddRange(KeyNames.GetPickableKeys().Select(code => new KeyChoice(code, KeyNames.GetName(code))));
        return choices;
    }

    private sealed record KeyChoice(ushort Code, string Name);
}
