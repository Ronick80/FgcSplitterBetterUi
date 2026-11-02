using FgcKeyboardSplitter.Services;

namespace FgcKeyboardSplitter.Tests.Fakes;

public sealed class FakeKeyCapture : IKeyCapture
{
    public bool IsCapturing { get; set; }

    public event Action<ushort>? KeyPressed;

    public event Action? Cancelled;

    public void Press(ushort keyCode) => KeyPressed?.Invoke(keyCode);

    public void LoseFocus() => Cancelled?.Invoke();
}

public sealed class FakeNavigator : INavigator
{
    public int DriversShown { get; private set; }

    public int MappingShown { get; private set; }

    public void ShowDrivers() => DriversShown++;

    public void ShowMapping() => MappingShown++;
}

/// <summary>Answers dialogs from queues and records what was asked.</summary>
public sealed class FakeDialogService : IDialogService
{
    public Queue<string?> TextAnswers { get; } = [];

    public List<(string Title, string Initial, string? Error)> Prompts { get; } = [];

    public bool ConfirmAnswer { get; set; } = true;

    public List<string> Confirmations { get; } = [];

    public ushort? PickAnswer { get; set; }

    public Task<string?> PromptTextAsync(string title, string label, string initial, string? error = null)
    {
        Prompts.Add((title, initial, error));
        return Task.FromResult(TextAnswers.Count > 0 ? TextAnswers.Dequeue() : null);
    }

    public Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        Confirmations.Add(title);
        return Task.FromResult(ConfirmAnswer);
    }

    public Task<ushort?> PickKeyAsync(string title, ushort current) => Task.FromResult(PickAnswer);
}
