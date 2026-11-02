using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fgc.Interop;
using FgcKeyboardSplitter.Services;

namespace FgcKeyboardSplitter.ViewModels;

public sealed partial class KeyboardsViewModel : ObservableObject
{
    private const int MaxControllers = 4;
    private static readonly TimeSpan IdentifyTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan LimitInterval = TimeSpan.FromSeconds(1);

    private readonly EngineService _engine;
    private readonly INavigator _navigator;
    private readonly KeyboardSelection _selection;
    private DateTime _identifyStartedAt;
    private DateTime _limitCheckedAt = DateTime.MinValue;
    private bool _limitAllowed = true;
    private bool _limitDirty;

    public KeyboardsViewModel(EngineService engine, INavigator navigator, KeyboardSelection selection)
    {
        _engine = engine;
        _navigator = navigator;
        _selection = selection;
        _selection.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Selected));
        for (int i = 0; i < MaxControllers; i++)
        {
            Slots.Add(new SlotItem());
        }

        _engine.Polled += (_, _) => OnPolled();
        _engine.DriversChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ShowDriverWarning));
            NotifyCommands();
        };
        _engine.EngineEventReceived += (_, engineEvent) => OnEngineEvent(engineEvent);
    }

    public ObservableCollection<KeyboardItem> Keyboards { get; } = [];

    public ObservableCollection<Notice> Notices { get; } = [];

    public ObservableCollection<SlotItem> Slots { get; } = [];

    public KeyboardItem? Selected
    {
        get => _selection.Selected;
        set => _selection.Selected = value;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IdentifyButtonText), nameof(IdentifyHint))]
    private bool _isIdentifying;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _statusIsError;

    [ObservableProperty]
    private string _slotsText = string.Empty;

    [ObservableProperty]
    private bool _slotsFull;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoKeyboards))]
    private string _listProblem = string.Empty;

    [ObservableProperty]
    private uint _runningSessions;

    public bool ShowDriverWarning => !_engine.DriversReady;

    public bool HasNoKeyboards => Keyboards.Count == 0 && ListProblem.Length == 0;

    public string IdentifyButtonText => IsIdentifying ? "Cancel identify" : "Identify by key press";

    public string IdentifyHint => IsIdentifying ? "Press any key on the keyboard you want to use..." : string.Empty;

    public Task InitializeAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        string? keepSelected = Selected?.HardwareId;
        (FgcResult result, IReadOnlyList<KeyboardInfo> list) = await Task.Run(() =>
        {
            FgcResult refreshed = _engine.Client.RefreshKeyboards();
            if (refreshed != FgcResult.Ok)
            {
                return (refreshed, (IReadOnlyList<KeyboardInfo>)[]);
            }

            FgcResult read = _engine.Client.GetKeyboards(out IReadOnlyList<KeyboardInfo> keyboards);
            return (read, keyboards);
        });

        ListProblem = result == FgcResult.Ok ? string.Empty : $"Keyboard list unavailable: {FgcErrors.Describe(result)}";
        ReplaceKeyboards(list, keepSelected);
        _limitDirty = true;
    }

    [RelayCommand]
    private void ToggleIdentify()
    {
        if (IsIdentifying)
        {
            _engine.Client.IdentifyCancel();
            IsIdentifying = false;
            return;
        }

        FgcResult result = _engine.Client.IdentifyBegin();
        IsIdentifying = result == FgcResult.Ok;
        _identifyStartedAt = DateTime.UtcNow;
        Report("Identify", result, string.Empty);
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

    private void NotifyCommands()
    {
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        StopAllCommand.NotifyCanExecuteChanged();
    }

    private void Report(string action, FgcResult result, string success)
    {
        StatusIsError = result != FgcResult.Ok;
        StatusMessage = result == FgcResult.Ok ? success : $"{action}: {FgcErrors.Describe(result)}";
    }

    private void ReplaceKeyboards(IReadOnlyList<KeyboardInfo> list, string? keepSelected)
    {
        Keyboards.Clear();
        foreach (KeyboardInfo info in list)
        {
            Keyboards.Add(new KeyboardItem(info.HardwareId, info.DisplayName));
        }

        Selected = Keyboards.FirstOrDefault(k => k.HardwareId == keepSelected);
        OnPropertyChanged(nameof(HasNoKeyboards));
        UpdateRunning();
    }

    private void OnPolled()
    {
        UpdateRunning();
        if (IsIdentifying)
        {
            PollIdentify();
        }

        if (_limitDirty || DateTime.UtcNow - _limitCheckedAt >= LimitInterval)
        {
            RefreshLimit();
        }

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

    private void PollIdentify()
    {
        FgcResult result = _engine.Client.IdentifyPoll(out int index);
        bool timedOut = DateTime.UtcNow - _identifyStartedAt > IdentifyTimeout;
        if (result == FgcResult.Ok && index < 0 && !timedOut)
        {
            return;
        }

        _engine.Client.IdentifyCancel();
        IsIdentifying = false;
        if (index < 0)
        {
            return;
        }

        // The engine may have rescanned while resolving the keyboard, so read the list again.
        if (_engine.Client.GetKeyboards(out IReadOnlyList<KeyboardInfo> list) == FgcResult.Ok)
        {
            ReplaceKeyboards(list, null);
            if (index < Keyboards.Count)
            {
                Selected = Keyboards[index];
            }
        }
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
        string name = Keyboards.FirstOrDefault(k => k.HardwareId == engineEvent.HardwareId)?.Title ?? engineEvent.HardwareId;
        Notices.Add(new Notice(engineEvent.Type == EngineEventType.KeyboardDisconnected
            ? $"{name} was unplugged. Its mapping stopped and the virtual controller was released."
            : $"{name} stopped unexpectedly (lost contact with the keyboard driver). Click Start to resume."));
        _limitDirty = true;
    }
}
