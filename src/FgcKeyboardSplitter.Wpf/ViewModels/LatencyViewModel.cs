using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fgc.Interop;
using FgcKeyboardSplitter.Services;

namespace FgcKeyboardSplitter.ViewModels;

public sealed partial class StatItem(string label) : ObservableObject
{
    public string Label { get; } = label;

    [ObservableProperty]
    private string _value = "-";
}

/// <summary>Latency numbers of one running keyboard, in microseconds.</summary>
public sealed class LatencyEntry(string hardwareId, string title)
{
    public string HardwareId { get; } = hardwareId;

    public string Title { get; } = title;

    public StatItem Average { get; } = new("Average (µs)");

    public StatItem Last { get; } = new("Last (µs)");

    public StatItem Max { get; } = new("Max (µs)");

    public StatItem Samples { get; } = new("Samples");

    public StatItem ReadAverage { get; } = new("Read avg (µs)");

    public StatItem ReadMax { get; } = new("Read max (µs)");

    public StatItem ReportAverage { get; } = new("Report avg (µs)");

    public StatItem ReportMax { get; } = new("Report max (µs)");

    public IReadOnlyList<StatItem> Stats =>
        [Average, Last, Max, Samples, ReadAverage, ReadMax, ReportAverage, ReportMax];

    public static string Microseconds(ulong nanoseconds) => (nanoseconds / 1000.0).ToString("F1", System.Globalization.CultureInfo.CurrentCulture);

    public void Update(LatencyStats stats)
    {
        Samples.Value = stats.SampleCount.ToString(System.Globalization.CultureInfo.CurrentCulture);
        bool any = stats.SampleCount > 0;
        Average.Value = any ? Microseconds(stats.AverageNs) : "-";
        Last.Value = any ? Microseconds(stats.LastNs) : "-";
        Max.Value = any ? Microseconds(stats.MaxNs) : "-";
        ReadAverage.Value = any ? Microseconds(stats.ReadAverageNs) : "-";
        ReadMax.Value = any ? Microseconds(stats.ReadMaxNs) : "-";
        ReportAverage.Value = any ? Microseconds(stats.ReportAverageNs) : "-";
        ReportMax.Value = any ? Microseconds(stats.ReportMaxNs) : "-";
    }
}

public sealed partial class LatencyViewModel : ObservableObject
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(500);

    private readonly EngineService _engine;
    private readonly KeyboardsViewModel _keyboards;
    private DateTime _refreshedAt = DateTime.MinValue;
    private bool _active;

    public LatencyViewModel(EngineService engine, KeyboardsViewModel keyboards)
    {
        _engine = engine;
        _keyboards = keyboards;
        _engine.Polled += (_, _) =>
        {
            if (_active && DateTime.UtcNow - _refreshedAt >= RefreshInterval)
            {
                Refresh();
            }
        };
        Entries.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoEntries));
    }

    public ObservableCollection<LatencyEntry> Entries { get; } = [];

    public bool HasNoEntries => Entries.Count == 0;

    /// <summary>Statistics are only read while the page is visible.</summary>
    public void SetActive(bool active)
    {
        _active = active;
        if (active)
        {
            Refresh();
        }
    }

    internal void Refresh()
    {
        _refreshedAt = DateTime.UtcNow;
        List<KeyboardItem> running = _keyboards.Keyboards.Where(k => k.IsRunning).ToList();

        foreach (LatencyEntry stale in Entries.Where(e => running.All(k => k.HardwareId != e.HardwareId)).ToList())
        {
            Entries.Remove(stale);
        }

        foreach (KeyboardItem keyboard in running)
        {
            if (_engine.Client.GetLatencyStats(keyboard.HardwareId, out LatencyStats stats) != FgcResult.Ok)
            {
                continue;
            }

            LatencyEntry? entry = Entries.FirstOrDefault(e => e.HardwareId == keyboard.HardwareId);
            if (entry is null)
            {
                entry = new LatencyEntry(keyboard.HardwareId, keyboard.Title);
                Entries.Add(entry);
            }

            entry.Update(stats);
        }
    }

    [RelayCommand]
    private void Reset()
    {
        foreach (LatencyEntry entry in Entries)
        {
            _engine.Client.ResetLatencyStats(entry.HardwareId);
        }

        Refresh();
    }
}
