using Fgc.Interop;
using FgcKeyboardSplitter.Services;
using FgcKeyboardSplitter.Tests.Fakes;
using FgcKeyboardSplitter.ViewModels;

namespace FgcKeyboardSplitter.Tests;

public class MappingViewModelTests
{
    private const string Id = @"HID\KB1";

    private readonly FakeFgcClient _client = new();
    private readonly FakeKeyCapture _capture = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly KeyboardSelection _selection = new();
    private readonly KeyboardItem _keyboard = new(Id, $"Keyboard 1 - {Id}");
    private readonly KeyboardsViewModel _keyboards;
    private readonly MappingViewModel _vm;

    public MappingViewModelTests()
    {
        var engine = new EngineService(_client);
        _keyboards = new KeyboardsViewModel(engine, new FakeNavigator(), _selection);
        _vm = new MappingViewModel(engine, _selection, _capture, _dialogs, _keyboards);
    }

    private MappingRow Row(XboxInput input) => _vm.Rows.Single(row => row.Input == input);

    private void Select() => _selection.Selected = _keyboard;

    private Mapping MappingWith(ushort keyForA)
    {
        Mapping mapping = _client.CreateDefaultMapping();
        mapping[XboxInput.A] = keyForA;
        return mapping;
    }

    private void AddProfile(string name, ushort keyForA) =>
        _client.SaveProfile(Id, name, MappingWith(keyForA));

    [Fact]
    public void TheArcadeStickHasEverythingButTheAnalogSticks()
    {
        Assert.Equal(17, _vm.ArcadeRows.Count);
        Assert.DoesNotContain(_vm.ArcadeRows, row => row.Input.ToString().Contains("Stick") && !row.Input.ToString().EndsWith("Click"));
        Assert.Contains(_vm.ArcadeRows, row => row.Input == XboxInput.LeftStickClick);
        Assert.Contains(_vm.ArcadeRows, row => row.Input == XboxInput.DpadLeft);
    }

    [Fact]
    public void ARunningKeyboardCannotBeEditedAndAnOpenBindingIsCancelled()
    {
        Select();
        _vm.ToggleBindCommand.Execute(Row(XboxInput.A));
        Assert.True(_capture.IsCapturing);

        _keyboard.IsRunning = true;

        Assert.True(_vm.IsTesting);
        Assert.False(_vm.CanEditKeys);
        Assert.False(_capture.IsCapturing);
        Assert.False(Row(XboxInput.A).IsBinding);
        Assert.False(_vm.ToggleBindCommand.CanExecute(Row(XboxInput.A)));
        Assert.False(_vm.PickKeyCommand.CanExecute(Row(XboxInput.A)));
        Assert.False(_vm.ClearKeyCommand.CanExecute(Row(XboxInput.A)));
        Assert.False(_vm.ResetDefaultsCommand.CanExecute(null));

        _keyboard.IsRunning = false;

        Assert.False(_vm.IsTesting);
        Assert.True(_vm.ToggleBindCommand.CanExecute(Row(XboxInput.A)));
    }

    [Fact]
    public void HeldInputsLightUpWhileRunningAndGoDarkWhenStopped()
    {
        Select();
        _vm.SetPageVisible(true);
        _keyboard.IsRunning = true;
        _client.Running.Add(Id);
        _client.Held[Id] = (1u << (int)XboxInput.A) | (1u << (int)XboxInput.DpadLeft);

        _vm.PollHeld();

        Assert.True(Row(XboxInput.A).IsHeld);
        Assert.True(Row(XboxInput.DpadLeft).IsHeld);
        Assert.False(Row(XboxInput.B).IsHeld);

        _client.Held[Id] = 1u << (int)XboxInput.B;
        _vm.PollHeld();

        Assert.False(Row(XboxInput.A).IsHeld);
        Assert.True(Row(XboxInput.B).IsHeld);

        _keyboard.IsRunning = false;

        Assert.All(_vm.Rows, row => Assert.False(row.IsHeld));
    }

    [Fact]
    public void LeavingThePageClearsHeldInputs()
    {
        Select();
        _vm.SetPageVisible(true);
        _keyboard.IsRunning = true;
        _client.Running.Add(Id);
        _client.Held[Id] = 1u << (int)XboxInput.X;
        _vm.PollHeld();
        Assert.True(Row(XboxInput.X).IsHeld);

        _vm.SetPageVisible(false);

        Assert.False(Row(XboxInput.X).IsHeld);
    }

    [Fact]
    public void StartAndStopComeFromTheKeyboardsPage()
    {
        Assert.Same(_keyboards, _vm.Sessions);
    }

    private const string BuiltIn = "2XKO Default";

    private void AddBuiltInProfile()
    {
        if (!_client.Profiles.TryGetValue(Id, out Dictionary<string, Mapping>? profiles))
        {
            _client.Profiles[Id] = profiles = new Dictionary<string, Mapping>(StringComparer.OrdinalIgnoreCase);
        }

        profiles[BuiltIn] = MappingWith(0x34);
        _client.ReadOnlyProfiles.Add(BuiltIn);
    }

    [Fact]
    public void BuiltInProfilesAreListedFirst()
    {
        AddProfile("Alpha", 0x30);
        AddBuiltInProfile();
        Select();

        Assert.Equal(new[] { BuiltIn, "Alpha" }, _vm.ProfileNames);
    }

    [Fact]
    public void ABuiltInProfileCanBeUsedButNotChanged()
    {
        AddProfile("Mine", 0x30);
        _client.LastUsed[Id] = "Mine";
        AddBuiltInProfile();
        Select();
        Assert.False(_vm.IsReadOnly);
        Assert.True(_vm.RenameCommand.CanExecute(null));

        _vm.SelectedProfile = BuiltIn;

        Assert.Equal(0x34, Row(XboxInput.A).KeyCode);
        Assert.Equal(BuiltIn, _vm.CurrentProfileText);
        Assert.True(_vm.IsReadOnly);
        Assert.False(_vm.RenameCommand.CanExecute(null));
        Assert.False(_vm.DeleteCommand.CanExecute(null));

        _vm.ToggleBindCommand.Execute(Row(XboxInput.B));
        _capture.Press(0x2C);

        Assert.True(_vm.IsDirty);
        Assert.False(_vm.SaveCommand.CanExecute(null));
        Assert.True(_vm.SaveAsCommand.CanExecute(null));
    }

    [Fact]
    public async Task SaveAsRefusesABuiltInName()
    {
        AddBuiltInProfile();
        Select();
        _dialogs.TextAnswers.Enqueue("2xko default");
        _dialogs.TextAnswers.Enqueue("My 2XKO");

        await _vm.SaveAsCommand.ExecuteAsync(null);

        Assert.Contains("built-in", _dialogs.Prompts[1].Error);
        Assert.Empty(_dialogs.Confirmations);
        Assert.DoesNotContain(_client.Calls, call => call == "SaveProfile:2xko default");
        Assert.Equal("My 2XKO", _vm.CurrentProfileText);
        Assert.False(_vm.IsReadOnly);
    }

    [Fact]
    public void ListsAllInputsInGroups()
    {
        Assert.Equal((int)XboxInput.Count, _vm.Rows.Count);
        Assert.Equal(7, _vm.Rows.Select(row => row.Group).Distinct().Count());
        Assert.Equal("Left bumper (LB)", Row(XboxInput.LeftBumper).Label);
    }

    [Fact]
    public void NothingCanBeEditedUntilAKeyboardIsSelected()
    {
        Assert.False(_vm.HasKeyboard);
        Assert.False(_vm.SaveAsCommand.CanExecute(null));

        Select();

        Assert.True(_vm.HasKeyboard);
        Assert.True(_vm.SaveAsCommand.CanExecute(null));
        Assert.Same(_keyboard, _vm.Keyboard);
    }

    [Fact]
    public void SelectingAKeyboardLoadsItsCurrentMapping()
    {
        _client.Mappings[Id] = MappingWith(0x30);

        Select();

        Assert.Equal(0x30, Row(XboxInput.A).KeyCode);
        Assert.Equal("(no profile)", _vm.CurrentProfileText);
        Assert.False(_vm.IsDirty);
    }

    [Fact]
    public void StartsFromTheLastUsedProfile()
    {
        AddProfile("Main", 0x30);
        AddProfile("Alt", 0x31);
        _client.LastUsed[Id] = "Main";

        Select();

        Assert.Equal("Main", _vm.CurrentProfileText);
        Assert.Equal("Main", _vm.SelectedProfile);
        Assert.Equal(new[] { "Alt", "Main" }, _vm.ProfileNames);
        Assert.True(_vm.HasCurrentProfile);
    }

    [Fact]
    public void ClickingAKeyButtonWaitsForAKeyAndClickingAgainCancels()
    {
        Select();
        MappingRow row = Row(XboxInput.A);

        _vm.ToggleBindCommand.Execute(row);

        Assert.True(row.IsBinding);
        Assert.True(_capture.IsCapturing);
        Assert.Equal("Press a key...", row.ButtonText);

        _vm.ToggleBindCommand.Execute(row);

        Assert.False(row.IsBinding);
        Assert.False(_capture.IsCapturing);
    }

    [Fact]
    public void ClickingAnotherButtonMovesTheBinding()
    {
        Select();

        _vm.ToggleBindCommand.Execute(Row(XboxInput.A));
        _vm.ToggleBindCommand.Execute(Row(XboxInput.B));

        Assert.False(Row(XboxInput.A).IsBinding);
        Assert.True(Row(XboxInput.B).IsBinding);
        Assert.True(_capture.IsCapturing);
    }

    [Fact]
    public void APressedKeyIsBoundAndSentToTheEngineImmediately()
    {
        Select();
        _vm.ToggleBindCommand.Execute(Row(XboxInput.A));

        _capture.Press(0x2C);

        Assert.Equal(0x2C, Row(XboxInput.A).KeyCode);
        Assert.False(Row(XboxInput.A).IsBinding);
        Assert.False(_capture.IsCapturing);
        Assert.Equal(0x2C, _client.Mappings[Id][XboxInput.A]);
        Assert.Contains($"SetMapping:{Id}", _client.Calls);
        Assert.True(_vm.IsDirty);
    }

    [Fact]
    public void KeysAreIgnoredWhenNoBindingIsWaiting()
    {
        Select();
        _client.Calls.Clear();

        _capture.Press(0x2C);

        Assert.DoesNotContain($"SetMapping:{Id}", _client.Calls);
        Assert.False(_vm.IsDirty);
    }

    [Fact]
    public void BindingStopsWhenTheWindowLosesFocus()
    {
        Select();
        _vm.ToggleBindCommand.Execute(Row(XboxInput.A));

        _capture.LoseFocus();

        Assert.False(Row(XboxInput.A).IsBinding);
        Assert.False(_capture.IsCapturing);
    }

    [Fact]
    public void BindingStopsWhenAnotherKeyboardIsSelected()
    {
        Select();
        _vm.ToggleBindCommand.Execute(Row(XboxInput.A));

        _selection.Selected = new KeyboardItem(@"HID\KB2", "Keyboard 2");

        Assert.False(Row(XboxInput.A).IsBinding);
        Assert.False(_capture.IsCapturing);
    }

    [Fact]
    public void FlagsAKeyThatIsBoundTwice()
    {
        Select();
        _vm.ToggleBindCommand.Execute(Row(XboxInput.B));

        _capture.Press(0x24); // the key A already uses

        Assert.True(Row(XboxInput.A).IsShared);
        Assert.True(Row(XboxInput.B).IsShared);
        Assert.False(Row(XboxInput.DpadUp).IsShared);

        _vm.ToggleBindCommand.Execute(Row(XboxInput.B));
        _capture.Press(0x30);

        Assert.False(Row(XboxInput.A).IsShared);
        Assert.False(Row(XboxInput.B).IsShared);
    }

    [Fact]
    public void ResetToDefaultsRestoresTheDefaultMapping()
    {
        _client.Mappings[Id] = MappingWith(0x30);
        Select();

        _vm.ResetDefaultsCommand.Execute(null);

        Assert.Equal(0x24, Row(XboxInput.A).KeyCode);
        Assert.Equal(0x24, _client.Mappings[Id][XboxInput.A]);
        Assert.True(_vm.IsDirty);
    }

    [Fact]
    public void ClearingAnInputUnbindsItAndAppliesItLive()
    {
        Select();
        MappingRow row = Row(XboxInput.A);
        Assert.True(_vm.ClearKeyCommand.CanExecute(row));

        _vm.ClearKeyCommand.Execute(row);

        Assert.Equal(KeyCode.Unbound, row.KeyCode);
        Assert.Equal(KeyCode.Unbound, _client.Mappings[Id][XboxInput.A]);
        Assert.True(_vm.IsDirty);
        Assert.False(_vm.ClearKeyCommand.CanExecute(row));
    }

    [Fact]
    public void ClearingStopsAnOpenBinding()
    {
        Select();
        _vm.ToggleBindCommand.Execute(Row(XboxInput.B));

        _vm.ClearKeyCommand.Execute(Row(XboxInput.A));

        Assert.False(Row(XboxInput.B).IsBinding);
        Assert.False(_capture.IsCapturing);
    }

    [Fact]
    public async Task PickingFromTheListBindsThatKey()
    {
        Select();
        _dialogs.PickAnswer = 0x1F;

        await _vm.PickKeyCommand.ExecuteAsync(Row(XboxInput.A));

        Assert.Equal(0x1F, Row(XboxInput.A).KeyCode);
        Assert.Equal(0x1F, _client.Mappings[Id][XboxInput.A]);
    }

    [Fact]
    public async Task PickingNoneUnbindsTheInput()
    {
        Select();
        _dialogs.PickAnswer = KeyCode.Unbound;

        await _vm.PickKeyCommand.ExecuteAsync(Row(XboxInput.A));

        Assert.Equal(KeyCode.Unbound, Row(XboxInput.A).KeyCode);
        Assert.Equal("(none)", Row(XboxInput.A).KeyName);
    }

    [Fact]
    public async Task CancellingThePickerChangesNothing()
    {
        Select();
        _dialogs.PickAnswer = null;

        await _vm.PickKeyCommand.ExecuteAsync(Row(XboxInput.A));

        Assert.Equal(0x24, Row(XboxInput.A).KeyCode);
        Assert.False(_vm.IsDirty);
    }

    [Fact]
    public void ShowsAMessageWhenTheEngineRefusesTheMapping()
    {
        Select();
        _client.SetMappingResult = FgcResult.NotFound;
        _vm.ToggleBindCommand.Execute(Row(XboxInput.A));

        _capture.Press(0x2C);

        Assert.True(_vm.HasMessage);
        Assert.StartsWith("Mapping:", _vm.Message);
    }

    [Fact]
    public async Task SaveAsStoresTheProfileAndMakesItCurrent()
    {
        Select();
        _vm.ToggleBindCommand.Execute(Row(XboxInput.A));
        _capture.Press(0x2C);
        _dialogs.TextAnswers.Enqueue("  Fight  ");

        await _vm.SaveAsCommand.ExecuteAsync(null);

        Assert.Equal(0x2C, _client.Profiles[Id]["Fight"][XboxInput.A]);
        Assert.Equal("Fight", _vm.CurrentProfileText);
        Assert.Equal("Fight", _vm.SelectedProfile);
        Assert.False(_vm.IsDirty);
        Assert.Equal(new[] { "Fight" }, _vm.ProfileNames);
    }

    [Fact]
    public async Task SaveAsAsksBeforeReplacingAnExistingProfile()
    {
        AddProfile("Fight", 0x30);
        Select();
        _dialogs.ConfirmAnswer = false;
        _dialogs.TextAnswers.Enqueue("fight");
        _client.Calls.Clear();

        await _vm.SaveAsCommand.ExecuteAsync(null);

        Assert.Single(_dialogs.Confirmations);
        Assert.DoesNotContain(_client.Calls, call => call.StartsWith("SaveProfile"));
        Assert.Equal(2, _dialogs.Prompts.Count); // asked again after the refusal, then cancelled
    }

    [Fact]
    public async Task SaveAsKeepsTheSpellingOfAReplacedProfile()
    {
        AddProfile("Fight", 0x30);
        Select();
        _dialogs.TextAnswers.Enqueue("fight");

        await _vm.SaveAsCommand.ExecuteAsync(null);

        Assert.Equal("Fight", _vm.CurrentProfileText);
        Assert.Equal(new[] { "Fight" }, _vm.ProfileNames);
    }

    [Fact]
    public async Task SaveAsKeepsAskingAfterAnInvalidName()
    {
        Select();
        _dialogs.TextAnswers.Enqueue("bad/name");
        _dialogs.TextAnswers.Enqueue("Good");

        await _vm.SaveAsCommand.ExecuteAsync(null);

        Assert.Null(_dialogs.Prompts[0].Error);
        Assert.Contains("1-63 characters", _dialogs.Prompts[1].Error);
        Assert.Equal("bad/name", _dialogs.Prompts[1].Initial);
        Assert.Equal("Good", _vm.CurrentProfileText);
    }

    [Fact]
    public async Task CancellingSaveAsSavesNothing()
    {
        Select();
        _client.Calls.Clear();

        await _vm.SaveAsCommand.ExecuteAsync(null);

        Assert.DoesNotContain(_client.Calls, call => call.StartsWith("SaveProfile"));
        Assert.Equal("(no profile)", _vm.CurrentProfileText);
    }

    [Fact]
    public void SaveOverwritesTheCurrentProfileOnlyAfterAChange()
    {
        AddProfile("Main", 0x30);
        _client.LastUsed[Id] = "Main";
        Select();
        Assert.False(_vm.SaveCommand.CanExecute(null));

        _vm.ToggleBindCommand.Execute(Row(XboxInput.B));
        _capture.Press(0x2C);

        Assert.True(_vm.SaveCommand.CanExecute(null));
        _vm.SaveCommand.Execute(null);

        Assert.Equal(0x2C, _client.Profiles[Id]["Main"][XboxInput.B]);
        Assert.False(_vm.IsDirty);
        Assert.False(_vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task RenameChangesTheCurrentProfile()
    {
        AddProfile("Main", 0x30);
        _client.LastUsed[Id] = "Main";
        Select();
        _dialogs.TextAnswers.Enqueue("Fight");

        await _vm.RenameCommand.ExecuteAsync(null);

        Assert.Equal("Fight", _vm.CurrentProfileText);
        Assert.Equal(new[] { "Fight" }, _vm.ProfileNames);
        Assert.Equal("Fight", _vm.SelectedProfile);
    }

    [Fact]
    public async Task RenameShowsTheEngineErrorAndAsksAgain()
    {
        AddProfile("Main", 0x30);
        AddProfile("Other", 0x31);
        _client.LastUsed[Id] = "Main";
        Select();
        _dialogs.TextAnswers.Enqueue("Other");

        await _vm.RenameCommand.ExecuteAsync(null);

        Assert.Equal(2, _dialogs.Prompts.Count);
        Assert.Equal("A profile with that name already exists", _dialogs.Prompts[1].Error);
        Assert.Equal("Main", _vm.CurrentProfileText);
    }

    [Fact]
    public void RenameAndDeleteNeedACurrentProfile()
    {
        Select();

        Assert.False(_vm.RenameCommand.CanExecute(null));
        Assert.False(_vm.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public void RenameAndDeleteAreAvailableForTheCurrentProfile()
    {
        AddProfile("Main", 0x30);
        _client.LastUsed[Id] = "Main";

        Select();

        Assert.True(_vm.RenameCommand.CanExecute(null));
        Assert.True(_vm.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task DeleteNeedsConfirmation()
    {
        AddProfile("Main", 0x30);
        _client.LastUsed[Id] = "Main";
        Select();
        _dialogs.ConfirmAnswer = false;

        await _vm.DeleteCommand.ExecuteAsync(null);

        Assert.Equal("Main", _vm.CurrentProfileText);
        Assert.Contains("Main", _client.Profiles[Id].Keys);

        _dialogs.ConfirmAnswer = true;
        await _vm.DeleteCommand.ExecuteAsync(null);

        Assert.Equal("(no profile)", _vm.CurrentProfileText);
        Assert.Empty(_vm.ProfileNames);
        Assert.Empty(_client.Profiles[Id]);
        Assert.Null(_vm.SelectedProfile);
    }

    [Fact]
    public void ChoosingAProfileLoadsItsMapping()
    {
        AddProfile("Main", 0x30);
        AddProfile("Alt", 0x31);
        _client.LastUsed[Id] = "Main";
        Select();

        _vm.SelectedProfile = "Alt";

        Assert.Equal(0x31, Row(XboxInput.A).KeyCode);
        Assert.Equal("Alt", _vm.CurrentProfileText);
        Assert.False(_vm.IsDirty);
        Assert.Equal(0x31, _client.Mappings[Id][XboxInput.A]);
    }

    [Fact]
    public void LoadingAProfileDiscardsUnsavedChanges()
    {
        AddProfile("Main", 0x30);
        AddProfile("Alt", 0x31);
        _client.LastUsed[Id] = "Main";
        Select();
        _vm.ToggleBindCommand.Execute(Row(XboxInput.B));
        _capture.Press(0x2C);
        Assert.True(_vm.IsDirty);

        _vm.SelectedProfile = "Alt";

        Assert.Equal(0x25, Row(XboxInput.B).KeyCode);
        Assert.False(_vm.IsDirty);
    }

    [Fact]
    public void EachKeyboardKeepsItsOwnProfileState()
    {
        AddProfile("Main", 0x30);
        _client.LastUsed[Id] = "Main";
        Select();
        _vm.ToggleBindCommand.Execute(Row(XboxInput.B));
        _capture.Press(0x2C);

        _selection.Selected = new KeyboardItem(@"HID\KB2", "Keyboard 2");
        Assert.Equal("(no profile)", _vm.CurrentProfileText);
        Assert.False(_vm.IsDirty);

        _selection.Selected = _keyboard;
        Assert.Equal("Main", _vm.CurrentProfileText);
        Assert.True(_vm.IsDirty);
    }
}
