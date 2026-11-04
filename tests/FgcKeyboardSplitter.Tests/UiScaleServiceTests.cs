using System.IO;
using FgcKeyboardSplitter.Services;

namespace FgcKeyboardSplitter.Tests;

public sealed class UiScaleServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "fgc-ui-scale-" + Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_folder, "ui-settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void StartsAtOneHundredPercentWithoutASettingsFile()
    {
        var scale = new UiScaleService(SettingsPath);

        Assert.Equal(1.0, scale.Scale);
        Assert.Equal(100, scale.Percent);
    }

    [Fact]
    public void StepsThroughTheLevels()
    {
        var scale = new UiScaleService(null);

        scale.ZoomIn();
        Assert.Equal(1.1, scale.Scale);
        scale.ZoomIn();
        Assert.Equal(1.2, scale.Scale);
        scale.ZoomOut();
        scale.ZoomOut();
        scale.ZoomOut();
        Assert.Equal(0.9, scale.Scale);
        scale.Reset();
        Assert.Equal(1.0, scale.Scale);
    }

    [Fact]
    public void StopsAtTheSmallestAndLargestLevel()
    {
        var scale = new UiScaleService(null);

        for (int i = 0; i < 20; i++)
        {
            scale.ZoomIn();
        }

        Assert.Equal(UiScaleService.Levels[^1], scale.Scale);

        for (int i = 0; i < 20; i++)
        {
            scale.ZoomOut();
        }

        Assert.Equal(UiScaleService.Levels[0], scale.Scale);
    }

    [Fact]
    public void ReportsTheLevelAndWhetherItCanGoFurther()
    {
        var scale = new UiScaleService(null);

        Assert.Equal("100%", scale.PercentText);
        Assert.True(scale.CanZoomIn);
        Assert.True(scale.CanZoomOut);

        while (scale.CanZoomIn)
        {
            scale.ZoomIn();
        }

        Assert.Equal("150%", scale.PercentText);
        Assert.True(scale.CanZoomOut);

        while (scale.CanZoomOut)
        {
            scale.ZoomOut();
        }

        Assert.Equal("80%", scale.PercentText);
        Assert.False(scale.CanZoomOut);
    }

    [Fact]
    public void RemembersTheZoomBetweenRuns()
    {
        var first = new UiScaleService(SettingsPath);
        first.ZoomIn();
        first.ZoomIn();

        var second = new UiScaleService(SettingsPath);

        Assert.Equal(1.2, second.Scale);
    }

    [Fact]
    public void SnapsAHandEditedValueToTheNearestLevel()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, "{\"scale\": 7.5}");

        Assert.Equal(UiScaleService.Levels[^1], new UiScaleService(SettingsPath).Scale);
    }

    [Fact]
    public void IgnoresADamagedSettingsFile()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, "not json");

        Assert.Equal(1.0, new UiScaleService(SettingsPath).Scale);
    }

    [Fact]
    public void ReportsChangesSoTheWindowCanRescale()
    {
        var scale = new UiScaleService(null);
        var changed = new List<string?>();
        scale.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        scale.ZoomIn();

        Assert.Contains(nameof(UiScaleService.Scale), changed);
        Assert.Contains(nameof(UiScaleService.Percent), changed);
    }
}
