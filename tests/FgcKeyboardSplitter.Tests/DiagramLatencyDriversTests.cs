using System.Windows;
using Fgc.Interop;
using FgcKeyboardSplitter.Services;
using FgcKeyboardSplitter.Tests.Fakes;
using FgcKeyboardSplitter.ViewModels;

namespace FgcKeyboardSplitter.Tests;

public class GamepadLayoutTests
{
    private static readonly Rect Canvas = new(0, 0, GamepadLayout.CanvasWidth, GamepadLayout.CanvasHeight);

    private static IEnumerable<(XboxInput Input, DiagramSpot Spot)> All() =>
        GamepadLayout.Spots.Select(pair => (pair.Key, pair.Value));

    [Fact]
    public void EveryInputIsOnTheDrawingOnce()
    {
        for (int i = 0; i < (int)XboxInput.Count; i++)
        {
            Assert.True(GamepadLayout.Spots.ContainsKey((XboxInput)i), $"{(XboxInput)i} is not drawn");
        }

        Assert.Equal((int)XboxInput.Count, GamepadLayout.Spots.Count);
    }

    [Fact]
    public void KeyLabelsDoNotOverlap()
    {
        var chips = All().Select(item => (item.Input, Bounds: GamepadLayout.ChipBounds(item.Spot))).ToList();
        foreach (var first in chips)
        {
            foreach (var second in chips.Where(other => other.Input > first.Input))
            {
                Assert.False(first.Bounds.IntersectsWith(second.Bounds), $"{first.Input} overlaps {second.Input}");
            }
        }
    }

    [Fact]
    public void EverythingStaysInsideTheDrawing()
    {
        foreach ((XboxInput input, DiagramSpot spot) in All())
        {
            Assert.True(Canvas.Contains(GamepadLayout.ChipBounds(spot)), $"{input} label is outside");
            Assert.True(Canvas.Contains(spot.Shape.Bounds), $"{input} shape is outside");
            Assert.True(Canvas.Contains(spot.Anchor), $"{input} anchor is outside");
        }

        Assert.True(Canvas.Contains(GamepadLayout.Body.Bounds));
    }

    [Fact]
    public void ButtonsDoNotOverlapEachOther()
    {
        // D-pad arms meet in the middle by design, so they are compared by their centres only.
        var shapes = All().ToList();
        foreach (var first in shapes)
        {
            foreach (var second in shapes.Where(other => other.Input > first.Input))
            {
                Rect a = first.Spot.Shape.Bounds;
                Rect b = second.Spot.Shape.Bounds;
                bool bothArms = !first.Spot.Outlined && !second.Spot.Outlined;
                Assert.False(!bothArms && a.IntersectsWith(b), $"{first.Input} overlaps {second.Input}");
            }
        }
    }

    [Fact]
    public void EachSideUsesEverySlotOnce()
    {
        foreach (var side in All().GroupBy(item => item.Spot.Side))
        {
            var slots = side.Select(item => item.Spot.Slot).Order().ToList();
            Assert.Equal(Enumerable.Range(0, slots.Count), slots);
        }
    }

    [Fact]
    public void OnlyTheFaceButtonsAreLettered()
    {
        var lettered = All().Where(item => item.Spot.Letter is not null).ToDictionary(item => item.Input, item => item.Spot.Letter);

        Assert.Equal(4, lettered.Count);
        Assert.Equal("A", lettered[XboxInput.A]);
        Assert.Equal("B", lettered[XboxInput.B]);
        Assert.Equal("X", lettered[XboxInput.X]);
        Assert.Equal("Y", lettered[XboxInput.Y]);
    }

    [Fact]
    public void LeaderLinesRunFromTheLabelToTheInput()
    {
        foreach ((XboxInput input, DiagramSpot spot) in All())
        {
            Rect chip = GamepadLayout.ChipBounds(spot);
            Point start = GamepadLayout.LeaderStart(spot);
            Assert.True(chip.Contains(start), $"{input} line does not start at its label");
        }
    }
}

public class LatencyViewModelTests
{
    private const string FirstId = @"HID\KB1";
    private const string SecondId = @"HID\KB2";

    private readonly FakeFgcClient _client = new();
    private readonly EngineService _engine;
    private readonly KeyboardsViewModel _keyboards;
    private readonly LatencyViewModel _latency;

    public LatencyViewModelTests()
    {
        _client.Keyboards.Add(new KeyboardInfo(FirstId, $"Keyboard 1 - {FirstId}"));
        _client.Keyboards.Add(new KeyboardInfo(SecondId, $"Keyboard 2 - {SecondId}"));
        _engine = new EngineService(_client);
        _engine.RefreshDrivers();
        _keyboards = new KeyboardsViewModel(_engine, new FakeNavigator(), new KeyboardSelection());
        _keyboards.RefreshCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        _latency = new LatencyViewModel(_engine, _keyboards);
    }

    [Fact]
    public void ShowsOnlyRunningKeyboards()
    {
        _client.Running.Add(FirstId);
        _client.Latency[FirstId] = new LatencyStats(5, 1000, 2000, 3000, 400, 500, 600, 700);
        _client.Latency[SecondId] = new LatencyStats(1, 1, 1, 1, 1, 1, 1, 1);
        _engine.Poll();

        _latency.Refresh();

        LatencyEntry entry = Assert.Single(_latency.Entries);
        Assert.Equal("Keyboard 1", entry.Title);
        Assert.False(_latency.HasNoEntries);
    }

    [Fact]
    public void ConvertsNanosecondsToMicroseconds()
    {
        _client.Running.Add(FirstId);
        _client.Latency[FirstId] = new LatencyStats(5, 1000, 23_400, 90_000, 4_000, 5_500, 6_000, 7_200);
        _engine.Poll();

        _latency.Refresh();

        LatencyEntry entry = _latency.Entries.Single();
        Assert.Equal((23.4).ToString("F1"), entry.Average.Value);
        Assert.Equal((1.0).ToString("F1"), entry.Last.Value);
        Assert.Equal((90.0).ToString("F1"), entry.Max.Value);
        Assert.Equal((5.5).ToString("F1"), entry.ReadMax.Value);
        Assert.Equal((7.2).ToString("F1"), entry.ReportMax.Value);
        Assert.Equal("5", entry.Samples.Value);
    }

    [Fact]
    public void ShowsDashesBeforeTheFirstSample()
    {
        _client.Running.Add(FirstId);
        _client.Latency[FirstId] = new LatencyStats(0, 0, 0, 0, 0, 0, 0, 0);
        _engine.Poll();

        _latency.Refresh();

        LatencyEntry entry = _latency.Entries.Single();
        Assert.Equal("-", entry.Average.Value);
        Assert.Equal("0", entry.Samples.Value);
    }

    [Fact]
    public void RemovesAKeyboardThatStopped()
    {
        _client.Running.Add(FirstId);
        _client.Latency[FirstId] = new LatencyStats(1, 1, 1, 1, 1, 1, 1, 1);
        _engine.Poll();
        _latency.Refresh();
        Assert.Single(_latency.Entries);

        _client.Running.Clear();
        _engine.Poll();
        _latency.Refresh();

        Assert.Empty(_latency.Entries);
        Assert.True(_latency.HasNoEntries);
    }

    [Fact]
    public void ResetClearsTheStatisticsOfEveryShownKeyboard()
    {
        _client.Running.Add(FirstId);
        _client.Latency[FirstId] = new LatencyStats(9, 1, 1, 1, 1, 1, 1, 1);
        _engine.Poll();
        _latency.Refresh();

        _latency.ResetCommand.Execute(null);

        Assert.Contains($"ResetLatencyStats:{FirstId}", _client.Calls);
        Assert.Equal("0", _latency.Entries.Single().Samples.Value);
    }

    [Fact]
    public void OnlyReadsStatisticsWhileThePageIsVisible()
    {
        _client.Running.Add(FirstId);
        _client.Latency[FirstId] = new LatencyStats(1, 1, 1, 1, 1, 1, 1, 1);
        _engine.Poll();
        _client.Calls.Clear();

        _engine.Poll();
        Assert.DoesNotContain($"GetLatencyStats:{FirstId}", _client.Calls);

        _latency.SetActive(true);
        Assert.Contains($"GetLatencyStats:{FirstId}", _client.Calls);

        _client.Calls.Clear();
        _latency.SetActive(false);
        _engine.Poll();
        Assert.DoesNotContain($"GetLatencyStats:{FirstId}", _client.Calls);
    }
}

public class DriversViewModelTests
{
    private readonly FakeFgcClient _client = new();
    private readonly EngineService _engine;

    public DriversViewModelTests() => _engine = new EngineService(_client);

    private DriversViewModel Create()
    {
        _engine.RefreshDrivers();
        return new DriversViewModel(_engine);
    }

    [Fact]
    public void ShowsBothDriversAsInstalled()
    {
        DriversViewModel vm = Create();

        Assert.Equal(("Installed", StatusKind.Good), (vm.InterceptionStatus, vm.InterceptionKind));
        Assert.Equal(("Installed", StatusKind.Good), (vm.ScpVBusStatus, vm.ScpVBusKind));
        Assert.False(vm.HasQueryProblem);
    }

    [Fact]
    public void AskForARestartWhenInterceptionIsNotActiveYet()
    {
        _client.Drivers = new DriverStatus(InterceptionState.RebootRequired, true);

        DriversViewModel vm = Create();

        Assert.Equal(("Restart required", StatusKind.Warning), (vm.InterceptionStatus, vm.InterceptionKind));
    }

    [Fact]
    public void MarksMissingDriversAsNotInstalled()
    {
        _client.Drivers = new DriverStatus(InterceptionState.NotInstalled, false);

        DriversViewModel vm = Create();

        Assert.Equal(("Not installed", StatusKind.Bad), (vm.InterceptionStatus, vm.InterceptionKind));
        Assert.Equal(("Not installed", StatusKind.Bad), (vm.ScpVBusStatus, vm.ScpVBusKind));
    }

    [Fact]
    public void ReportsAFailedQuery()
    {
        _client.DriverResult = FgcResult.Internal;

        DriversViewModel vm = Create();

        Assert.True(vm.HasQueryProblem);
        Assert.Equal(StatusKind.Neutral, vm.InterceptionKind);
    }

    [Fact]
    public void ExposesTheVersionTextForTheAboutLine()
    {
        _engine.RefreshDrivers();

        var vm = new DriversViewModel(_engine, "UI 1.0.0 - engine 2.0.0");

        Assert.Equal("UI 1.0.0 - engine 2.0.0", vm.AboutText);
    }

    [Fact]
    public void UpdatesWhenTheDriversAreCheckedAgain()
    {
        _client.Drivers = new DriverStatus(InterceptionState.NotInstalled, false);
        DriversViewModel vm = Create();

        _client.Drivers = new DriverStatus(InterceptionState.Installed, true);
        vm.RecheckCommand.Execute(null);

        Assert.Equal(StatusKind.Good, vm.InterceptionKind);
        Assert.Equal(StatusKind.Good, vm.ScpVBusKind);
    }
}
