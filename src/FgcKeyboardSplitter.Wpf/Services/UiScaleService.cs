using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FgcKeyboardSplitter.Services;

/// <summary>
/// The zoom applied to the whole window (Ctrl + plus / minus / 0, or Ctrl + mouse wheel), saved between runs.
/// This sits on top of Windows' own display scaling.
/// </summary>
public sealed partial class UiScaleService : ObservableObject
{
    public static readonly double[] Levels = [0.8, 0.9, 1.0, 1.1, 1.2, 1.35, 1.5];

    private readonly string? _settingsPath;

    public UiScaleService(string? settingsPath)
    {
        _settingsPath = settingsPath;
        _scale = Load(settingsPath);
    }

    /// <summary>%APPDATA%\FgcKeyboardSplitter\ui-settings.json, next to the engine's profiles.</summary>
    public static string DefaultSettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FgcKeyboardSplitter", "ui-settings.json");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Percent))]
    private double _scale;

    public int Percent => (int)Math.Round(Scale * 100);

    public void ZoomIn() => SetLevel(Array.FindIndex(Levels, level => level > Scale + 0.001));

    public void ZoomOut() => SetLevel(Array.FindLastIndex(Levels, level => level < Scale - 0.001));

    public void Reset() => SetLevel(Array.IndexOf(Levels, 1.0));

    private void SetLevel(int index)
    {
        if (index < 0 || Math.Abs(Levels[index] - Scale) < 0.001)
        {
            return;
        }

        Scale = Levels[index];
        Save();
    }

    private static double Load(string? path)
    {
        try
        {
            if (path is not null && File.Exists(path))
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                if (document.RootElement.TryGetProperty("scale", out JsonElement value) && value.TryGetDouble(out double scale))
                {
                    // Snap to a known level so a hand-edited file cannot make the window unusable.
                    return Levels.MinBy(level => Math.Abs(level - scale));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A damaged settings file just means the default zoom.
        }

        return 1.0;
    }

    private void Save()
    {
        if (_settingsPath is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(new { scale = Scale }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not being able to remember the zoom is harmless.
        }
    }
}
