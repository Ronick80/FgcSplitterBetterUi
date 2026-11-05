using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fgc.Interop;
using FgcKeyboardSplitter.Services;

namespace FgcKeyboardSplitter.ViewModels;

/// <summary>
/// "Press any key to join": a keyboard appears once a key is pressed on it, so only keyboards people actually use
/// are listed (a mouse's keyboard interface never shows up). Pressing a key on a listed keyboard, idle or running,
/// selects and flashes it, to remind players which one is theirs.
/// </summary>
public sealed partial class KeyboardsViewModel : ObservableObject
{
    private const int MaxControllers = 4;
    private const int MaxPressesPerPoll = 32;
    private static readonly TimeSpan LimitInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan FlashDuration = TimeSpan.FromMilliseconds(900);

    private readonly EngineService _engine;
    private readonly INavigator _navigator;
    private readonly KeyboardSelection _selection;
    private readonly Dictionary<string, uint> _pressCounts = [];
    private DateTime _limitCheckedAt = DateTime.MinValue;
    private bool _limitAllowed = true;
    private bool _limitDirty;
    private bool _active;

    public KeyboardsViewModel(EngineService engine, INavigator navigator, KeyboardSelection selection)
    {
        _engine = engine;
        _navigator = navigator;
        _selection = selection;
        Profiles = new KeyboardProfiles(engine.Client);
        _selection.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Selected));
        for (int i = 0; i < MaxControllers; i++)
        {
            Slots.Add(new SlotItem { Number = i + 1 });
        }

        _engine.Polled += (_, _) => OnPolled();
        _engine.DriversChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ShowDriverWarning));
            UpdateWatch();
            NotifyCommands();
        };
        _engine.EngineEventReceived += (_, engineEvent) => OnEngineEvent(engineEvent);
    }

    /// <summary>Keyboards that joined with a key press, one per physical device.</summary>
    public ObservableCollection<KeyboardItem> Keyboards { get; } = [];

    /// <summary>Each keyboard's current profile, shared with the Mapping page.</summary>
    public KeyboardProfiles Profiles { get; }

    public ObservableCollection<Notice> Notices { get; } = [];

    public ObservableCollection<SlotItem> Slots { get; } = [];

    public KeyboardItem? Selected
    {
        get => _selection.Selected;
        set => _selection.Selected = value;
    }

    /// <summary>True while key presses are being watched for, which is only while the page is in front.</summary>
    [ObservableProperty]
    private bool _isWatching;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _statusIsError;

    [ObservableProperty]
    private string _slotsText = string.Empty;

    [ObservableProperty]
    private bool _slotsFull;

    [ObservableProperty]
    private string _listProblem = string.Empty;

    [ObservableProperty]
    private uint _runningSessions;

    public bool ShowDriverWarning => !_engine.DriversReady;

    public bool HasNoKeyboards => Keyboards.Count == 0;

    public bool HasKeyboards => Keyboards.Count > 0;

    /// <summary>
    /// Watches for key presses while the page is shown and the window is active. Watching routes idle keyboards
    /// through the engine (their keys still reach Windows), so it stops whenever nobody is looking at the list.
    /// </summary>
    public void SetActive(bool active)
    {
        _active = active;
        UpdateWatch();
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync(KeyboardItem? keyboard)
    {
        if (keyboard is null)
        {
            return;
        }

        FgcResult result = await Task.Run(() => _engine.Client.Start(keyboard.HardwareId));
        Report("Start", result, $"{keyboard.Title} is running.");
        _limitDirty = true;
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync(KeyboardItem? keyboard)
    {
        if (keyboard is null)
        {
            return;
        }

        FgcResult result = await Task.Run(() => _engine.Client.Stop(keyboard.HardwareId));
        Report("Stop", result, $"{keyboard.Title} was released.");
        _limitDirty = true;
    }

    [RelayCommand(CanExecute = nameof(CanStopAll))]
    private async Task StopAllAsync()
    {
        FgcResult result = await Task.Run(() => _engine.Client.StopAll());
        Report("Stop all", result, "All keyboards were released.");
        _limitDirty = true;
    }

    /// <summary>Removes the keyboards that are not running; they come back with their next key press.</summary>
    [RelayCommand(CanExecute = nameof(CanClearIdle))]
    private void ClearIdle()
    {
        foreach (KeyboardItem keyboard in Keyboards.Where(k => !k.IsRunning).ToList())
        {
            Remove(keyboard);
        }

        StatusIsError = false;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void DismissNotice(Notice? notice)
    {
        if (notice is not null)
        {
            Notices.Remove(notice);
        }
    }

    [RelayCommand]
    private void ShowDrivers() => _navigator.ShowDrivers();

    [RelayCommand]
    private void EditMapping(KeyboardItem? keyboard)
    {
        if (keyboard is not null)
        {
            Selected = keyboard;
            _navigator.ShowMapping();
        }
    }

    private bool CanStart(KeyboardItem? keyboard) => keyboard is { IsRunning: false } && _engine.DriversReady && _limitAllowed;

    private bool CanStop(KeyboardItem? keyboard) => keyboard is { IsRunning: true };

    private bool CanStopAll() => RunningSessions > 0;

    private bool CanClearIdle() => Keyboards.Any(k => !k.IsRunning);

    private void NotifyCommands()
    {
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        StopAllCommand.NotifyCanExecuteChanged();
        ClearIdleCommand.NotifyCanExecuteChanged();
    }

    private void Report(string action, FgcResult result, string success)
    {
        StatusIsError = result != FgcResult.Ok;
        StatusMessage = result == FgcResult.Ok ? success : $"{action}: {FgcErrors.Describe(result)}";
    }

    private void UpdateWatch()
    {
        bool wanted = _active && _engine.DriversReady;
        if (wanted && !IsWatching)
        {
            FgcResult result = _engine.Client.WatchBegin();
            IsWatching = result == FgcResult.Ok;
            ListProblem = IsWatching ? string.Empty : $"Keyboard detection unavailable: {FgcErrors.Describe(result)}";
        }
        else if (!wanted && IsWatching)
        {
            _engine.Client.WatchEnd();
            IsWatching = false;
        }
    }

    private void OnPolled()
    {
        UpdateRunning();
        if (IsWatching)
        {
            PollPresses();
        }

        if (_active)
        {
            CheckRunningPresses();
        }

        DateTime now = DateTime.UtcNow;
        foreach (KeyboardItem keyboard in Keyboards)
        {
            keyboard.UpdateFlash(now);
        }

        if (_limitDirty || now - _limitCheckedAt >= LimitInterval)
        {
            RefreshLimit();
        }

        NotifyCommands();
    }

    private void PollPresses()
    {
        for (int i = 0; i < MaxPressesPerPoll; i++)
        {
            if (_engine.Client.WatchPoll(out KeyboardInfo? pressed) != FgcResult.Ok || pressed is null)
            {
                return;
            }

            OnKeyPressed(pressed);
        }
    }

    private void OnKeyPressed(KeyboardInfo info)
    {
        // Interfaces of one device share a device ID, so a press on any of them finds the same card. The card keeps
        // the interface that joined first: that is the one a session captures and the one its profiles belong to.
        string deviceKey = info.DeviceId.Length > 0 ? info.DeviceId : info.HardwareId;
        KeyboardItem? keyboard = Keyboards.FirstOrDefault(k => k.DeviceKey == deviceKey);
        if (keyboard is null)
        {
            keyboard = new KeyboardItem(info.HardwareId, TitleFor(info.ProductName), deviceKey, ProductNameOf(info.ProductName))
            {
                Profile = Profiles.For(info.HardwareId),
            };
            Keyboards.Add(keyboard);
            OnKeyboardsChanged();
            UpdateRunning();
        }

        Highlight(keyboard);
    }

    /// <summary>A running keyboard's keys never reach the watch, so its press counter shows it is in use.</summary>
    private void CheckRunningPresses()
    {
        foreach (KeyboardItem keyboard in Keyboards)
        {
            if (!keyboard.IsRunning || _engine.Client.GetKeyPressCount(keyboard.HardwareId, out uint count) != FgcResult.Ok)
            {
                _pressCounts.Remove(keyboard.HardwareId);
                continue;
            }

            if (_pressCounts.TryGetValue(keyboard.HardwareId, out uint seen) && seen != count)
            {
                Highlight(keyboard);
            }

            _pressCounts[keyboard.HardwareId] = count;
        }
    }

    private void Highlight(KeyboardItem keyboard)
    {
        Selected = keyboard;
        keyboard.Flash(DateTime.UtcNow + FlashDuration);
    }

    private static string ProductNameOf(string productName) => productName.Length > 0 ? productName : "Keyboard";

    // Identical keyboards share a product name, so later ones are numbered: "GMMK PRO", "GMMK PRO #2".
    private string TitleFor(string productName)
    {
        string name = ProductNameOf(productName);
        int same = Keyboards.Count(k => k.ProductName == name);
        return same == 0 ? name : $"{name} #{same + 1}";
    }

    private void Remove(KeyboardItem keyboard)
    {
        Keyboards.Remove(keyboard);
        _pressCounts.Remove(keyboard.HardwareId);
        if (ReferenceEquals(Selected, keyboard))
        {
            Selected = null;
        }

        OnKeyboardsChanged();
    }

    private void OnKeyboardsChanged()
    {
        OnPropertyChanged(nameof(HasNoKeyboards));
        OnPropertyChanged(nameof(HasKeyboards));
        NotifyCommands();
    }

    private void UpdateRunning()
    {
        foreach (KeyboardItem keyboard in Keyboards)
        {
            if (_engine.Client.IsRunning(keyboard.HardwareId, out bool running) == FgcResult.Ok)
            {
                keyboard.IsRunning = running;
            }
        }

        RunningSessions = _engine.Client.GetRunningSessionCount(out uint count) == FgcResult.Ok ? count : 0;
    }

    private void RefreshLimit()
    {
        _limitCheckedAt = DateTime.UtcNow;
        _limitDirty = false;
        if (_engine.Client.CheckControllerLimit(1, out ControllerLimit limit) != FgcResult.Ok)
        {
            _limitAllowed = true; // an unreadable count must not block the user; Start still enforces the limit
            return;
        }

        _limitAllowed = limit.Allowed;
        uint used = limit.PhysicalConnected + limit.VirtualActive;
        for (int i = 0; i < Slots.Count; i++)
        {
            Slots[i].Kind = i < limit.VirtualActive ? SlotKind.Mine
                : i < used ? SlotKind.Other
                : SlotKind.Free;
        }

        SlotsText = $"{limit.PhysicalConnected} other + {limit.VirtualActive} from this app = {used} of {MaxControllers} slots used";
        SlotsFull = !limit.Allowed;
    }

    private void OnEngineEvent(EngineEvent engineEvent)
    {
        KeyboardItem? keyboard = Keyboards.FirstOrDefault(k => k.HardwareId == engineEvent.HardwareId);
        string name = keyboard?.Title ?? engineEvent.HardwareId;
        bool unplugged = engineEvent.Type == EngineEventType.KeyboardDisconnected;
        Notices.Add(new Notice(unplugged
            ? $"{name} was unplugged. Its mapping stopped and the virtual controller was released. Plug it back in and press a key on it to add it again."
            : $"{name} stopped unexpectedly (lost contact with the keyboard driver). Click Start to resume."));
        if (unplugged && keyboard is not null)
        {
            Remove(keyboard);
        }

        _limitDirty = true;
    }
}
