using Fgc.Interop;
using FgcKeyboardSplitter.Services;
using FgcKeyboardSplitter.Tests.Fakes;
using FgcKeyboardSplitter.ViewModels;

namespace FgcKeyboardSplitter.Tests;

public class KeyboardsViewModelTests
{
    private const string FirstId = @"HID\KB1";
    private const string SecondId = @"HID\KB2";

    private readonly FakeFgcClient _client = new();
    private readonly FakeNavigator _navigator = new();
    private readonly KeyboardSelection _selection = new();
    private readonly EngineService _engine;
    private readonly KeyboardsViewModel _vm;

    public KeyboardsViewModelTests()
    {
        _client.Keyboards.Add(new KeyboardInfo(FirstId, $"Keyboard 1 - {FirstId}"));
        _client.Keyboards.Add(new KeyboardInfo(SecondId, $"Keyboard 2 - {SecondId}"));
        _engine = new EngineService(_client);
        _engine.RefreshDrivers();
        _vm = new KeyboardsViewModel(_engine, _navigator, _selection);
    }

    private async Task<KeyboardItem> LoadFirstAsync()
    {
        await _vm.RefreshCommand.ExecuteAsync(null);
        return _vm.Keyboards[0];
    }

    [Fact]
    public async Task RefreshLoadsTheKeyboardList()
    {
        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "Keyboard 1", "Keyboard 2" }, _vm.Keyboards.Select(k => k.Title));
        Assert.Equal(new[] { FirstId, SecondId }, _vm.Keyboards.Select(k => k.Subtitle));
        Assert.False(_vm.HasNoKeyboards);
    }

    [Fact]
    public async Task RefreshKeepsTheSelectedKeyboard()
    {
        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.Selected = _vm.Keyboards[1];

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(SecondId, _vm.Selected?.HardwareId);
    }

    [Fact]
    public async Task RefreshFailureExplainsTheProblem()
    {
        _client.RefreshResult = FgcResult.DriverMissing;

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(_vm.Keyboards);
        Assert.Contains("unavailable", _vm.ListProblem);
        Assert.False(_vm.HasNoKeyboards);
    }

    [Fact]
    public async Task EmptyListWithoutAProblemShowsTheEmptyHint()
    {
        _client.Keyboards.Clear();

        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(_vm.HasNoKeyboards);
    }

    [Fact]
    public async Task SelectionIsSharedWithOtherPages()
    {
        KeyboardItem keyboard = await LoadFirstAsync();

        _vm.Selected = keyboard;

        Assert.Same(keyboard, _selection.Selected);
    }

    [Fact]
    public async Task StartIsBlockedUntilBothDriversAreReady()
    {
        _client.Drivers = new DriverStatus(InterceptionState.RebootRequired, true);
        _engine.RefreshDrivers();
        KeyboardItem keyboard = await LoadFirstAsync();

        Assert.True(_vm.ShowDriverWarning);
        Assert.False(_vm.StartCommand.CanExecute(keyboard));

        _client.Drivers = new DriverStatus(InterceptionState.Installed, true);
        _engine.RefreshDrivers();

        Assert.False(_vm.ShowDriverWarning);
        Assert.True(_vm.StartCommand.CanExecute(keyboard));
    }

    [Fact]
    public async Task StartIsBlockedWhenNoControllerSlotIsFree()
    {
        KeyboardItem keyboard = await LoadFirstAsync();
        _client.Limit = new ControllerLimit(4, 0, 1, false);

        _engine.Poll();

        Assert.True(_vm.SlotsFull);
        Assert.False(_vm.StartCommand.CanExecute(keyboard));
    }

    [Fact]
    public async Task StartingAndStoppingAKeyboardUpdatesItsState()
    {
        KeyboardItem keyboard = await LoadFirstAsync();

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
        KeyboardItem keyboard = await LoadFirstAsync();
        _client.StartResult = FgcResult.DeviceBusy;

        await _vm.StartCommand.ExecuteAsync(keyboard);

        Assert.True(_vm.StatusIsError);
        Assert.Equal("Start: Keyboard is in use by another program", _vm.StatusMessage);
    }

    [Fact]
    public async Task StopAllReleasesEveryKeyboard()
    {
        await _vm.RefreshCommand.ExecuteAsync(null);
        await _vm.StartCommand.ExecuteAsync(_vm.Keyboards[0]);
        await _vm.StartCommand.ExecuteAsync(_vm.Keyboards[1]);
        _engine.Poll();

        await _vm.StopAllCommand.ExecuteAsync(null);
        _engine.Poll();

        Assert.All(_vm.Keyboards, keyboard => Assert.False(keyboard.IsRunning));
        Assert.Equal("All keyboards were released.", _vm.StatusMessage);
    }

    [Fact]
    public async Task ShowsWhichControllerSlotsAreTaken()
    {
        await LoadFirstAsync();
        _client.Limit = new ControllerLimit(1, 1, 1, true);

        _engine.Poll();

        Assert.Equal(new[] { SlotKind.Mine, SlotKind.Other, SlotKind.Free, SlotKind.Free }, _vm.Slots.Select(s => s.Kind));
        Assert.Equal("1 other + 1 from this app = 2 of 4 slots used", _vm.SlotsText);
        Assert.False(_vm.SlotsFull);
    }

    [Fact]
    public async Task IdentifySelectsTheKeyboardThatReceivedTheKeyPress()
    {
        await LoadFirstAsync();

        _vm.ToggleIdentifyCommand.Execute(null);
        Assert.True(_vm.IsIdentifying);
        Assert.True(_client.IdentifyActive);
        Assert.Equal("Cancel identify", _vm.IdentifyButtonText);

        _engine.Poll();
        Assert.True(_vm.IsIdentifying);

        _client.IdentifyIndex = 1;
        _engine.Poll();

        Assert.False(_vm.IsIdentifying);
        Assert.False(_client.IdentifyActive);
        Assert.Equal(SecondId, _vm.Selected?.HardwareId);
    }

    [Fact]
    public async Task IdentifyCanBeCancelled()
    {
        await LoadFirstAsync();
        _vm.ToggleIdentifyCommand.Execute(null);

        _vm.ToggleIdentifyCommand.Execute(null);

        Assert.False(_vm.IsIdentifying);
        Assert.False(_client.IdentifyActive);
        Assert.Null(_vm.Selected);
    }

    [Fact]
    public async Task EngineEventsBecomeDismissibleNotices()
    {
        await LoadFirstAsync();
        _client.Events.Enqueue(new EngineEvent(EngineEventType.KeyboardDisconnected, FirstId));
        _client.Events.Enqueue(new EngineEvent(EngineEventType.SessionFaulted, SecondId));

        _engine.Poll();

        Assert.Equal(2, _vm.Notices.Count);
        Assert.Contains("Keyboard 1 was unplugged", _vm.Notices[0].Text);
        Assert.Contains("Keyboard 2 stopped unexpectedly", _vm.Notices[1].Text);

        _vm.DismissNoticeCommand.Execute(_vm.Notices[0]);

        Assert.Single(_vm.Notices);
    }

    [Fact]
    public async Task EditMappingSelectsTheKeyboardAndOpensTheMappingPage()
    {
        await _vm.RefreshCommand.ExecuteAsync(null);

        _vm.EditMappingCommand.Execute(_vm.Keyboards[1]);

        Assert.Equal(SecondId, _selection.Selected?.HardwareId);
        Assert.Equal(1, _navigator.MappingShown);
    }

    [Fact]
    public void TheDriversShortcutOpensTheDriversPage()
    {
        _vm.ShowDriversCommand.Execute(null);

        Assert.Equal(1, _navigator.DriversShown);
    }
}
