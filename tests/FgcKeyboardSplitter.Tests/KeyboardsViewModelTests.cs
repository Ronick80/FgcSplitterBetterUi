using Fgc.Interop;
using FgcKeyboardSplitter.Services;
using FgcKeyboardSplitter.Tests.Fakes;
using FgcKeyboardSplitter.ViewModels;

namespace FgcKeyboardSplitter.Tests;

public class KeyboardsViewModelTests
{
    private const string FirstId = @"HID\KB1&MI_00";
    private const string FirstMediaId = @"HID\KB1&MI_01";
    private const string SecondId = @"HID\KB2&MI_00";
    private const string TwinAId = @"HID\TWIN&MI_00";
    private const string TwinBId = @"HID\TWIN&MI_00#2";

    private readonly FakeFgcClient _client = new();
    private readonly FakeNavigator _navigator = new();
    private readonly KeyboardSelection _selection = new();
    private readonly EngineService _engine;
    private readonly KeyboardsViewModel _vm;

    public KeyboardsViewModelTests()
    {
        _client.Keyboards.Add(new KeyboardInfo(FirstId, $"Keyboard 1 - {FirstId}", "GMMK PRO", "{DEV-1}"));
        _client.Keyboards.Add(new KeyboardInfo(FirstMediaId, $"Keyboard 2 - {FirstMediaId}", "GMMK PRO", "{DEV-1}"));
        _client.Keyboards.Add(new KeyboardInfo(SecondId, $"Keyboard 3 - {SecondId}", "Apex Pro", "{DEV-2}"));
        // Identical keyboards: same name, no device ID that tells them apart.
        _client.Keyboards.Add(new KeyboardInfo(TwinAId, $"Keyboard 4 - {TwinAId}", "Twin Board", ""));
        _client.Keyboards.Add(new KeyboardInfo(TwinBId, $"Keyboard 5 - {TwinBId}", "Twin Board", ""));
        _engine = new EngineService(_client);
        _engine.RefreshDrivers();
        _vm = new KeyboardsViewModel(_engine, _navigator, _selection);
        _vm.SetActive(true);
    }

    private KeyboardItem Join(string hardwareId)
    {
        _client.Press(hardwareId);
        _engine.Poll();
        return _vm.Keyboards.Single(k => k.HardwareId == hardwareId);
    }

    [Fact]
    public void TheListStartsEmptyAndWaitsForAKeyPress()
    {
        Assert.Empty(_vm.Keyboards);
        Assert.True(_vm.HasNoKeyboards);
        Assert.True(_vm.IsWatching);
        Assert.True(_client.WatchActive);
    }

    [Fact]
    public void AKeyPressAddsTheKeyboardUnderItsProductName()
    {
        KeyboardItem keyboard = Join(FirstId);

        Assert.Equal("GMMK PRO", keyboard.Title);
        Assert.Equal(FirstId, keyboard.Subtitle);
        Assert.False(_vm.HasNoKeyboards);
        Assert.True(_vm.HasKeyboards);
    }

    [Fact]
    public void AKeyPressSelectsAndFlashesTheKeyboard()
    {
        KeyboardItem first = Join(FirstId);
        Join(SecondId);

        _client.Press(FirstId);
        _engine.Poll();

        Assert.Same(first, _vm.Selected);
        Assert.Same(first, _selection.Selected);
        Assert.True(first.IsFlashing);
        Assert.Equal(2, _vm.Keyboards.Count);
    }

    [Fact]
    public void TheFlashEndsAfterAMoment()
    {
        KeyboardItem keyboard = Join(FirstId);

        keyboard.UpdateFlash(DateTime.UtcNow + TimeSpan.FromSeconds(5));

        Assert.False(keyboard.IsFlashing);
    }

    [Fact]
    public void AnotherInterfaceOfTheSameKeyboardFindsTheSameCard()
    {
        KeyboardItem keyboard = Join(FirstId);

        _client.Press(FirstMediaId);
        _engine.Poll();

        Assert.Single(_vm.Keyboards);
        Assert.Equal(FirstId, keyboard.HardwareId); // the interface that joined first keeps driving it
        Assert.Same(keyboard, _vm.Selected);
    }

    [Fact]
    public void IdenticalKeyboardsGetNumberedCards()
    {
        Join(TwinAId);
        Join(TwinBId);

        Assert.Equal(new[] { "Twin Board", "Twin Board #2" }, _vm.Keyboards.Select(k => k.Title));
    }

    [Fact]
    public void WatchingFollowsThePage()
    {
        _vm.SetActive(false);

        Assert.False(_vm.IsWatching);
        Assert.False(_client.WatchActive);

        _vm.SetActive(true);

        Assert.True(_vm.IsWatching);
    }

    [Fact]
    public void WatchingWaitsForTheDrivers()
    {
        _client.Drivers = new DriverStatus(InterceptionState.RebootRequired, true);
        _engine.RefreshDrivers();

        Assert.False(_vm.IsWatching);
        Assert.True(_vm.ShowDriverWarning);

        _client.Drivers = new DriverStatus(InterceptionState.Installed, true);
        _engine.RefreshDrivers();

        Assert.True(_vm.IsWatching);
    }

    [Fact]
    public void AWatchFailureExplainsTheProblem()
    {
        _vm.SetActive(false);
        _client.WatchResult = FgcResult.DeviceBusy;

        _vm.SetActive(true);

        Assert.False(_vm.IsWatching);
        Assert.Contains("unavailable", _vm.ListProblem);
    }

    [Fact]
    public void KeyPressesOnARunningKeyboardHighlightIt()
    {
        KeyboardItem first = Join(FirstId);
        KeyboardItem second = Join(SecondId);
        StartNow(first);
        _engine.Poll(); // first count seen: nothing to compare with yet
        Assert.Same(second, _vm.Selected);

        _client.PressCounts[FirstId] = 3;
        _engine.Poll();

        Assert.Same(first, _vm.Selected);
        Assert.True(first.IsFlashing);
    }

    [Fact]
    public void RunningKeyboardsAreNotHighlightedWhileThePageIsHidden()
    {
        KeyboardItem first = Join(FirstId);
        KeyboardItem second = Join(SecondId);
        StartNow(first);
        _engine.Poll();
        _vm.SetActive(false);

        _client.PressCounts[FirstId] = 3;
        _engine.Poll();

        Assert.Same(second, _vm.Selected);
    }

    [Fact]
    public void SelectionIsSharedWithOtherPages()
    {
        KeyboardItem keyboard = Join(FirstId);

        _vm.Selected = null;
        _vm.Selected = keyboard;

        Assert.Same(keyboard, _selection.Selected);
    }

    [Fact]
    public void StartIsBlockedUntilBothDriversAreReady()
    {
        KeyboardItem keyboard = Join(FirstId);
        _client.Drivers = new DriverStatus(InterceptionState.RebootRequired, true);
        _engine.RefreshDrivers();

        Assert.True(_vm.ShowDriverWarning);
        Assert.False(_vm.StartCommand.CanExecute(keyboard));

        _client.Drivers = new DriverStatus(InterceptionState.Installed, true);
        _engine.RefreshDrivers();

        Assert.False(_vm.ShowDriverWarning);
        Assert.True(_vm.StartCommand.CanExecute(keyboard));
    }

    [Fact]
    public void StartIsBlockedWhenNoControllerSlotIsFree()
    {
        _client.Limit = new ControllerLimit(4, 0, 1, false);

        KeyboardItem keyboard = Join(FirstId);

        Assert.True(_vm.SlotsFull);
        Assert.False(_vm.StartCommand.CanExecute(keyboard));
    }

    [Fact]
    public async Task StartingAndStoppingAKeyboardUpdatesItsState()
    {
        KeyboardItem keyboard = Join(FirstId);

        await _vm.StartCommand.ExecuteAsync(keyboard);
        _engine.Poll();

        Assert.Contains($"Start:{FirstId}", _client.Calls);
        Assert.True(keyboard.IsRunning);
        Assert.Equal("Running", keyboard.StatusText);
        Assert.Equal(1u, _vm.RunningSessions);
        Assert.False(_vm.StartCommand.CanExecute(keyboard));
        Assert.True(_vm.StopCommand.CanExecute(keyboard));
        Assert.True(_vm.StopAllCommand.CanExecute(null));

        await _vm.StopCommand.ExecuteAsync(keyboard);
        _engine.Poll();

        Assert.False(keyboard.IsRunning);
        Assert.Equal(0u, _vm.RunningSessions);
        Assert.False(_vm.StopAllCommand.CanExecute(null));
    }

    [Fact]
    public async Task AFailedStartShowsTheEngineMessage()
    {
        KeyboardItem keyboard = Join(FirstId);
        _client.StartResult = FgcResult.DeviceBusy;

        await _vm.StartCommand.ExecuteAsync(keyboard);

        Assert.True(_vm.StatusIsError);
        Assert.Equal("Start: Keyboard is in use by another program", _vm.StatusMessage);
    }

    [Fact]
    public async Task StopAllReleasesEveryKeyboard()
    {
        await _vm.StartCommand.ExecuteAsync(Join(FirstId));
        await _vm.StartCommand.ExecuteAsync(Join(SecondId));
        _engine.Poll();

        await _vm.StopAllCommand.ExecuteAsync(null);
        _engine.Poll();

        Assert.All(_vm.Keyboards, keyboard => Assert.False(keyboard.IsRunning));
        Assert.Equal("All keyboards were released.", _vm.StatusMessage);
    }

    [Fact]
    public async Task ClearIdleKeepsRunningKeyboards()
    {
        KeyboardItem running = Join(FirstId);
        KeyboardItem idle = Join(SecondId);
        await _vm.StartCommand.ExecuteAsync(running);
        _engine.Poll();
        Assert.Same(idle, _vm.Selected);

        _vm.ClearIdleCommand.Execute(null);

        Assert.Equal(new[] { running }, _vm.Keyboards);
        Assert.Null(_vm.Selected);
        Assert.False(_vm.ClearIdleCommand.CanExecute(null));
    }

    [Fact]
    public void AClearedKeyboardComesBackWithItsNextKeyPress()
    {
        Join(FirstId);
        _vm.ClearIdleCommand.Execute(null);
        Assert.Empty(_vm.Keyboards);

        KeyboardItem keyboard = Join(FirstId);

        Assert.Equal("GMMK PRO", keyboard.Title);
    }

    [Fact]
    public void ShowsWhichControllerSlotsAreTaken()
    {
        _client.Limit = new ControllerLimit(1, 1, 1, true);

        _engine.Poll();

        Assert.Equal(new[] { SlotKind.Mine, SlotKind.Other, SlotKind.Free, SlotKind.Free }, _vm.Slots.Select(s => s.Kind));
        Assert.Equal(new[] { "P1", "P2", "P3", "P4" }, _vm.Slots.Select(s => s.PlayerLabel));
        Assert.Equal("1 other + 1 from this app = 2 of 4 slots used", _vm.SlotsText);
        Assert.False(_vm.SlotsFull);
    }

    [Fact]
    public void EngineEventsBecomeDismissibleNotices()
    {
        Join(FirstId);
        Join(SecondId);
        _client.Events.Enqueue(new EngineEvent(EngineEventType.KeyboardDisconnected, FirstId));
        _client.Events.Enqueue(new EngineEvent(EngineEventType.SessionFaulted, SecondId));

        _engine.Poll();

        Assert.Equal(2, _vm.Notices.Count);
        Assert.Contains("GMMK PRO was unplugged", _vm.Notices[0].Text);
        Assert.Contains("Apex Pro stopped unexpectedly", _vm.Notices[1].Text);

        _vm.DismissNoticeCommand.Execute(_vm.Notices[0]);

        Assert.Single(_vm.Notices);
    }

    [Fact]
    public void AnUnpluggedKeyboardLeavesTheList()
    {
        Join(FirstId);
        KeyboardItem second = Join(SecondId);
        _client.Events.Enqueue(new EngineEvent(EngineEventType.KeyboardDisconnected, FirstId));

        _engine.Poll();

        Assert.Equal(new[] { second }, _vm.Keyboards);
    }

    [Fact]
    public void EditMappingSelectsTheKeyboardAndOpensTheMappingPage()
    {
        Join(FirstId);
        KeyboardItem second = Join(SecondId);
        _vm.Selected = null;

        _vm.EditMappingCommand.Execute(second);

        Assert.Equal(SecondId, _selection.Selected?.HardwareId);
        Assert.Equal(1, _navigator.MappingShown);
    }

    [Fact]
    public void TheDriversShortcutOpensTheDriversPage()
    {
        _vm.ShowDriversCommand.Execute(null);

        Assert.Equal(1, _navigator.DriversShown);
    }

    [Fact]
    public void AJoinedKeyboardShowsTheProfileItStartsFrom()
    {
        _client.SaveProfile(FirstId, "Main", _client.CreateDefaultMapping());

        KeyboardItem first = Join(FirstId);
        KeyboardItem second = Join(SecondId);

        Assert.Equal("Main", first.Profile?.DisplayName);
        Assert.False(first.Profile?.Dirty);
        Assert.Equal("No profile", second.Profile?.DisplayName);
    }

    [Fact]
    public void AKeyboardThatJoinsAgainKeepsItsProfileState()
    {
        KeyboardItem first = Join(FirstId);
        first.Profile!.Current = "Edited";
        first.Profile.Dirty = true;

        _vm.ClearIdleCommand.Execute(null);
        KeyboardItem again = Join(FirstId);

        Assert.NotSame(first, again);
        Assert.Same(first.Profile, again.Profile);
        Assert.Same(_vm.Profiles.For(FirstId), again.Profile);
    }

    private void StartNow(KeyboardItem keyboard) => _vm.StartCommand.ExecuteAsync(keyboard).GetAwaiter().GetResult();
}
