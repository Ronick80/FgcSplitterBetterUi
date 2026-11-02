using System.Windows;
using System.Windows.Controls;
using Fgc.Interop;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using TextBox = System.Windows.Controls.TextBox;

namespace FgcKeyboardSplitter.Services;

public interface IDialogService
{
    /// <summary>Asks for a line of text. Null when cancelled. A non-null error is shown under the box.</summary>
    Task<string?> PromptTextAsync(string title, string label, string initial, string? error = null);

    Task<bool> ConfirmAsync(string title, string message, string confirmText);

    /// <summary>Lets the user pick a key from the list. Null when cancelled; <see cref="KeyCode.Unbound"/> for "(none)".</summary>
    Task<ushort?> PickKeyAsync(string title, ushort current);
}

public sealed class DialogService : IDialogService
{
    private readonly ContentDialogService _service = new();
    private IReadOnlyList<KeyChoice>? _choices;

    public void SetHost(ContentDialogHost host) => _service.SetDialogHost(host);

    public async Task<string?> PromptTextAsync(string title, string label, string initial, string? error = null)
    {
        var input = new TextBox { Text = initial, MinWidth = 320, MaxLength = 63 };
        input.Loaded += (_, _) =>
        {
            input.Focus();
            input.SelectAll();
        };

        var content = new StackPanel();
        content.Children.Add(new System.Windows.Controls.TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 6) });
        content.Children.Add(input);
        if (!string.IsNullOrEmpty(error))
        {
            content.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = error,
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 380,
                Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("SystemFillColorCriticalBrush"),
            });
        }

        ContentDialogResult result = await _service.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions
        {
            Title = title,
            Content = content,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
        });
        return result == ContentDialogResult.Primary ? input.Text : null;
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        ContentDialogResult result = await _service.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions
        {
            Title = title,
            Content = new System.Windows.Controls.TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 380 },
            PrimaryButtonText = confirmText,
            CloseButtonText = "Cancel",
        });
        return result == ContentDialogResult.Primary;
    }

    public async Task<ushort?> PickKeyAsync(string title, ushort current)
    {
        _choices ??= BuildChoices();
        var list = new ListBox
        {
            ItemsSource = _choices,
            DisplayMemberPath = nameof(KeyChoice.Name),
            Height = 360,
            MinWidth = 280,
            SelectedItem = _choices.FirstOrDefault(choice => choice.Code == current),
        };
        list.Loaded += (_, _) =>
        {
            if (list.SelectedItem is not null)
            {
                list.ScrollIntoView(list.SelectedItem);
            }
        };

        ContentDialogResult result = await _service.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions
        {
            Title = title,
            Content = list,
            PrimaryButtonText = "Use this key",
            CloseButtonText = "Cancel",
        });
        return result == ContentDialogResult.Primary && list.SelectedItem is KeyChoice choice ? choice.Code : null;
    }

    private static IReadOnlyList<KeyChoice> BuildChoices()
    {
        var choices = new List<KeyChoice> { new(KeyCode.Unbound, KeyNames.GetName(KeyCode.Unbound)) };
        choices.AddRange(KeyNames.GetPickableKeys().Select(code => new KeyChoice(code, KeyNames.GetName(code))));
        return choices;
    }

    private sealed record KeyChoice(ushort Code, string Name);
}
