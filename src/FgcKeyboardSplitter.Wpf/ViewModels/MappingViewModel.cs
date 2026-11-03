using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fgc.Interop;
using FgcKeyboardSplitter.Services;

namespace FgcKeyboardSplitter.ViewModels;

public sealed partial class MappingRow(XboxInput input, string label, string group) : ObservableObject
{
    private readonly DiagramSpot _spot = GamepadLayout.Spots[input];

    public XboxInput Input { get; } = input;

    public string Label { get; } = label;

    public string Group { get; } = group;

    public Geometry Shape => _spot.Shape;

    public bool Outlined => _spot.Outlined;

    public Geometry? Glyph => _spot.Glyph;

    public string? Letter => _spot.Letter;

    public double LetterLeft => _spot.Center.X - GamepadLayout.LetterBox / 2;

    public double LetterTop => _spot.Center.Y - GamepadLayout.LetterBox / 2;

    public double ChipLeft => GamepadLayout.ChipBounds(_spot).Left;

    public double ChipTop => GamepadLayout.ChipBounds(_spot).Top;

    public double ChipWidth => GamepadLayout.ChipBounds(_spot).Width;

    public PointCollection LeaderPoints { get; } = Frozen([GamepadLayout.LeaderStart(GamepadLayout.Spots[input]), GamepadLayout.Spots[input].Anchor]);

    /// <summary>The pointer is over this input's drawing or its key label; both light up together.</summary>
    [ObservableProperty]
    private bool _isHot;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeyName), nameof(ButtonText), nameof(ChipText), nameof(IsBound))]
    private ushort _keyCode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ButtonText), nameof(ChipText))]
    private bool _isBinding;

    /// <summary>The same key is also bound to another input.</summary>
    [ObservableProperty]
    private bool _isShared;

    public bool IsBound => KeyCode != Fgc.Interop.KeyCode.Unbound;

    public string KeyName => KeyNames.GetName(KeyCode);

    public string ButtonText => IsBinding ? "Press a key..." : KeyName;

    /// <summary>Shorter than <see cref="ButtonText"/> so it fits on the controller drawing.</summary>
    public string ChipText => IsBinding ? "Press key" : KeyName;

    private static PointCollection Frozen(Point[] points)
    {
        var collection = new PointCollection(points);
        collection.Freeze();
        return collection;
    }
}

public sealed partial class MappingViewModel : ObservableObject
{
    private static readonly (string Group, XboxInput[] Inputs)[] Groups =
    [
        ("Face buttons", [XboxInput.A, XboxInput.B, XboxInput.X, XboxInput.Y]),
        ("Shoulders and triggers", [XboxInput.LeftBumper, XboxInput.RightBumper, XboxInput.LeftTrigger, XboxInput.RightTrigger]),
        ("Menu", [XboxInput.Start, XboxInput.Back, XboxInput.Guide]),
        ("Stick clicks", [XboxInput.LeftStickClick, XboxInput.RightStickClick]),
        ("D-pad", [XboxInput.DpadUp, XboxInput.DpadDown, XboxInput.DpadLeft, XboxInput.DpadRight]),
        ("Left stick", [XboxInput.LeftStickUp, XboxInput.LeftStickDown, XboxInput.LeftStickLeft, XboxInput.LeftStickRight]),
        ("Right stick", [XboxInput.RightStickUp, XboxInput.RightStickDown, XboxInput.RightStickLeft, XboxInput.RightStickRight]),
    ];

    private static readonly string[] Labels =
    [
        "A", "B", "X", "Y",
        "Left bumper (LB)", "Right bumper (RB)", "Left trigger (LT)", "Right trigger (RT)",
        "Left stick click", "Right stick click",
        "Start", "Back", "Guide",
        "D-pad up", "D-pad down", "D-pad left", "D-pad right",
        "Left stick up", "Left stick down", "Left stick left", "Left stick right",
        "Right stick up", "Right stick down", "Right stick left", "Right stick right",
    ];

    private readonly EngineService _engine;
    private readonly KeyboardSelection _selection;
    private readonly IKeyCapture _capture;
    private readonly IDialogService _dialogs;
    private readonly Dictionary<string, ProfileState> _states = [];
    private Mapping _mapping = new();
    private MappingRow? _bindingRow;
    private bool _syncingProfile;

    public MappingViewModel(EngineService engine, KeyboardSelection selection, IKeyCapture capture, IDialogService dialogs)
    {
        _engine = engine;
        _selection = selection;
        _capture = capture;
        _dialogs = dialogs;

        foreach ((string group, XboxInput[] inputs) in Groups)
        {
            foreach (XboxInput input in inputs)
            {
                Rows.Add(new MappingRow(input, Labels[(int)input], group));
            }
        }

        RowsView = CollectionViewSource.GetDefaultView(Rows);
        RowsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(MappingRow.Group)));

        _selection.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(KeyboardSelection.Selected))
            {
                OnSelectionChanged();
            }
        };
        _capture.KeyPressed += OnKeyPressed;
        _capture.Cancelled += CancelBinding;
    }

    private sealed class ProfileState
    {
        public string Current { get; set; } = string.Empty;

        public bool Dirty { get; set; }
    }

    public ObservableCollection<MappingRow> Rows { get; } = [];

    public ICollectionView RowsView { get; }

    public ObservableCollection<string> ProfileNames { get; } = [];

    public KeyboardItem? Keyboard => _selection.Selected;

    public bool HasKeyboard => Keyboard is not null;

    public bool HasCurrentProfile => State?.Current.Length > 0;

    public bool IsDirty => State?.Dirty == true;

    /// <summary>The current profile is built in, so it can be used and copied with Save as but not changed.</summary>
    public bool IsReadOnly => State is { Current.Length: > 0 } state && _engine.Client.IsReadOnlyProfile(state.Current);

    public string CurrentProfileText => State is { Current.Length: > 0 } state ? state.Current : "(no profile)";

    [ObservableProperty]
    private string? _selectedProfile;

    [ObservableProperty]
    private string _message = string.Empty;

    public bool HasMessage => Message.Length > 0;

    private ProfileState? State => Keyboard is { } keyboard && _states.TryGetValue(keyboard.HardwareId, out ProfileState? state) ? state : null;

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnSelectedProfileChanged(string? value)
    {
        if (!_syncingProfile && value is not null && value != State?.Current)
        {
            LoadProfile(value);
        }
    }

    public void CancelBinding()
    {
        if (_bindingRow is not null)
        {
            _bindingRow.IsBinding = false;
            _bindingRow = null;
        }

        _capture.IsCapturing = false;
    }

    [RelayCommand]
    private void ToggleBind(MappingRow? row)
    {
        if (row is null)
        {
            return;
        }

        bool wasThis = ReferenceEquals(_bindingRow, row);
        CancelBinding();
        if (!wasThis)
        {
            _bindingRow = row;
            row.IsBinding = true;
            _capture.IsCapturing = true;
        }
    }

    [RelayCommand]
    private async Task PickKeyAsync(MappingRow? row)
    {
        if (row is null)
        {
            return;
        }

        CancelBinding();
        ushort? key = await _dialogs.PickKeyAsync($"Key for {row.Label}", row.KeyCode);
        if (key is { } chosen)
        {
            Assign(row.Input, chosen);
        }
    }

    [RelayCommand(CanExecute = nameof(CanClearKey))]
    private void ClearKey(MappingRow? row)
    {
        if (row is null)
        {
            return;
        }

        CancelBinding();
        Assign(row.Input, KeyCode.Unbound);
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        CancelBinding();
        _mapping = _engine.Client.CreateDefaultMapping();
        ApplyMapping();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (Keyboard is { } keyboard && State is { } state)
        {
            Message = TrySave(keyboard, state.Current, out string? error) ? string.Empty : $"Save: {error}";
        }
    }

    [RelayCommand(CanExecute = nameof(HasKeyboard))]
    private async Task SaveAsAsync()
    {
        if (Keyboard is not { } keyboard)
        {
            return;
        }

        CancelBinding();
        string initial = State?.Current ?? string.Empty;
        string? error = null;
        while (await _dialogs.PromptTextAsync("Save profile as", "Profile name", initial, error) is { } entered)
        {
            string name = entered.Trim();
            initial = name;
            if (_engine.Client.IsReadOnlyProfile(name))
            {
                error = "That name belongs to a built-in profile. Choose another name.";
                continue;
            }

            bool replaces = ProfileNames.Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase));
            if (replaces && !await _dialogs.ConfirmAsync("Replace profile?", $"A profile named \"{name}\" already exists. Replace it?", "Replace"))
            {
                error = null;
                continue;
            }

            if (TrySave(keyboard, name, out error))
            {
                Message = string.Empty;
                return;
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanChangeProfile))]
    private async Task RenameAsync()
    {
        if (Keyboard is not { } keyboard || State is not { } state)
        {
            return;
        }

        CancelBinding();
        string initial = state.Current;
        string? error = null;
        while (await _dialogs.PromptTextAsync("Rename profile", "New name", initial, error) is { } entered)
        {
            string name = entered.Trim();
            initial = name;
            FgcResult result = _engine.Client.RenameProfile(keyboard.HardwareId, state.Current, name);
            if (result == FgcResult.Ok)
            {
                state.Current = name;
                Message = string.Empty;
                RefreshProfiles();
                NotifyProfileState();
                return;
            }

            error = FgcErrors.DescribeNameError(result);
        }
    }

    [RelayCommand(CanExecute = nameof(CanChangeProfile))]
    private async Task DeleteAsync()
    {
        if (Keyboard is not { } keyboard || State is not { } state)
        {
            return;
        }

        CancelBinding();
        if (!await _dialogs.ConfirmAsync("Delete profile?",
                $"Delete the profile \"{state.Current}\"? The keyboard keeps its current mapping, but it will no longer be saved.", "Delete"))
        {
            return;
        }

        FgcResult result = _engine.Client.DeleteProfile(keyboard.HardwareId, state.Current);
        if (result != FgcResult.Ok)
        {
            Message = $"Delete: {FgcErrors.Describe(result)}";
            return;
        }

        state.Current = string.Empty;
        state.Dirty = false;
        Message = string.Empty;
        RefreshProfiles();
        NotifyProfileState();
    }

    private bool CanClearKey(MappingRow? row) => row is { KeyCode: not KeyCode.Unbound };

    private bool CanSave() => CanChangeProfile() && IsDirty;

    private bool CanChangeProfile() => HasCurrentProfile && !IsReadOnly;

    private void OnKeyPressed(ushort keyCode)
    {
        if (_bindingRow is not { } row)
        {
            return;
        }

        CancelBinding();
        Assign(row.Input, keyCode);
    }

    private void Assign(XboxInput input, ushort keyCode)
    {
        _mapping[input] = keyCode;
        ApplyMapping();
    }

    private void ApplyMapping()
    {
        if (Keyboard is not { } keyboard)
        {
            return;
        }

        if (State is { } state)
        {
            state.Dirty = true;
        }

        FgcResult result = _engine.Client.SetMapping(keyboard.HardwareId, _mapping);
        Message = result == FgcResult.Ok ? string.Empty : $"Mapping: {FgcErrors.Describe(result)}";
        UpdateRows();
        NotifyProfileState();
    }

    private void LoadProfile(string name)
    {
        if (Keyboard is not { } keyboard || State is not { } state)
        {
            return;
        }

        CancelBinding();
        FgcResult result = _engine.Client.LoadProfile(keyboard.HardwareId, name, out Mapping loaded);
        if (result != FgcResult.Ok)
        {
            Message = $"Load: {FgcErrors.Describe(result)}";
            SyncSelectedProfile();
            return;
        }

        _mapping = loaded;
        state.Current = name;
        state.Dirty = false;
        Message = string.Empty;
        UpdateRows();
        NotifyProfileState();
    }

    private bool TrySave(KeyboardItem keyboard, string name, out string? error)
    {
        FgcResult result = _engine.Client.SaveProfile(keyboard.HardwareId, name, _mapping);
        if (result != FgcResult.Ok)
        {
            error = FgcErrors.DescribeNameError(result);
            return false;
        }

        error = null;
        RefreshProfiles();
        // Names match case-insensitively, so keep the spelling the file already has.
        string stored = ProfileNames.FirstOrDefault(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)) ?? name;
        if (State is { } state)
        {
            state.Current = stored;
            state.Dirty = false;
        }

        NotifyProfileState();
        return true;
    }

    private void OnSelectionChanged()
    {
        CancelBinding();
        Message = string.Empty;
        ProfileNames.Clear();
        OnPropertyChanged(nameof(Keyboard));
        OnPropertyChanged(nameof(HasKeyboard));
        SaveAsCommand.NotifyCanExecuteChanged();

        if (Keyboard is { } keyboard)
        {
            // The engine starts a keyboard from its last-used profile.
            if (_engine.Client.GetMapping(keyboard.HardwareId, out Mapping mapping) == FgcResult.Ok)
            {
                _mapping = mapping;
            }

            if (!_states.ContainsKey(keyboard.HardwareId))
            {
                _states[keyboard.HardwareId] = new ProfileState
                {
                    Current = _engine.Client.GetLastUsedProfile(keyboard.HardwareId, out string lastUsed) == FgcResult.Ok ? lastUsed : string.Empty,
                };
            }

            RefreshProfiles();
            UpdateRows();
        }

        NotifyProfileState();
    }

    private void RefreshProfiles()
    {
        ProfileNames.Clear();
        if (Keyboard is { } keyboard && _engine.Client.GetProfileNames(keyboard.HardwareId, out IReadOnlyList<string> names) == FgcResult.Ok)
        {
            foreach (string name in names)
            {
                ProfileNames.Add(name);
            }
        }

        SyncSelectedProfile();
    }

    private void SyncSelectedProfile()
    {
        _syncingProfile = true;
        SelectedProfile = State is { Current.Length: > 0 } state && ProfileNames.Contains(state.Current) ? state.Current : null;
        _syncingProfile = false;
    }

    private void UpdateRows()
    {
        foreach (MappingRow row in Rows)
        {
            ushort key = _mapping[row.Input];
            row.KeyCode = key;
            row.IsShared = key != KeyCode.Unbound && Rows.Any(other => other != row && _mapping[other.Input] == key);
        }
    }

    private void NotifyProfileState()
    {
        OnPropertyChanged(nameof(HasCurrentProfile));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(CurrentProfileText));
        SyncSelectedProfile();
        SaveCommand.NotifyCanExecuteChanged();
        RenameCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }
}
