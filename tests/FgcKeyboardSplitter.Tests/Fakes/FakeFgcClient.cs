using Fgc.Interop;

namespace FgcKeyboardSplitter.Tests.Fakes;

/// <summary>An in-memory engine. Every call is recorded in <see cref="Calls"/>.</summary>
public sealed class FakeFgcClient : IFgcClient
{
    public List<string> Calls { get; } = [];

    public FgcResult DriverResult { get; set; } = FgcResult.Ok;

    public DriverStatus Drivers { get; set; } = new(InterceptionState.Installed, true);

    public List<KeyboardInfo> Keyboards { get; } = [];

    public FgcResult RefreshResult { get; set; } = FgcResult.Ok;

    public HashSet<string> Running { get; } = [];

    public FgcResult StartResult { get; set; } = FgcResult.Ok;

    public ControllerLimit Limit { get; set; } = new(0, 0, 1, true);

    public Queue<EngineEvent> Events { get; } = [];

    public Dictionary<string, LatencyStats> Latency { get; } = [];

    public int IdentifyIndex { get; set; } = -1;

    public bool IdentifyActive { get; private set; }

    public Dictionary<string, Mapping> Mappings { get; } = [];

    public Dictionary<string, Dictionary<string, Mapping>> Profiles { get; } = [];

    public Dictionary<string, string> LastUsed { get; } = [];

    public FgcResult SetMappingResult { get; set; } = FgcResult.Ok;

    public Mapping CreateDefaultMapping()
    {
        var mapping = new Mapping();
        mapping[XboxInput.A] = 0x24;
        mapping[XboxInput.B] = 0x25;
        mapping[XboxInput.DpadUp] = 0x11;
        return mapping;
    }

    public FgcResult GetDriverStatus(out DriverStatus status)
    {
        status = DriverResult == FgcResult.Ok ? Drivers : default;
        return DriverResult;
    }

    public FgcResult RefreshKeyboards()
    {
        Calls.Add("RefreshKeyboards");
        return RefreshResult;
    }

    public FgcResult GetKeyboards(out IReadOnlyList<KeyboardInfo> keyboards)
    {
        keyboards = Keyboards.ToList();
        return FgcResult.Ok;
    }

    public FgcResult IdentifyBegin()
    {
        Calls.Add("IdentifyBegin");
        IdentifyActive = true;
        return FgcResult.Ok;
    }

    public FgcResult IdentifyCancel()
    {
        Calls.Add("IdentifyCancel");
        IdentifyActive = false;
        return FgcResult.Ok;
    }

    public FgcResult IdentifyPoll(out int index)
    {
        index = IdentifyIndex;
        return FgcResult.Ok;
    }

    public FgcResult GetMapping(string hardwareId, out Mapping mapping)
    {
        mapping = Mappings.TryGetValue(hardwareId, out Mapping? stored) ? stored.Clone() : CreateDefaultMapping();
        return FgcResult.Ok;
    }

    public FgcResult SetMapping(string hardwareId, Mapping mapping)
    {
        Calls.Add($"SetMapping:{hardwareId}");
        if (SetMappingResult == FgcResult.Ok)
        {
            Mappings[hardwareId] = mapping.Clone();
        }

        return SetMappingResult;
    }

    public FgcResult GetProfileNames(string hardwareId, out IReadOnlyList<string> names)
    {
        names = Profiles.TryGetValue(hardwareId, out Dictionary<string, Mapping>? profiles)
            ? profiles.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList()
            : [];
        return FgcResult.Ok;
    }

    public FgcResult LoadProfile(string hardwareId, string name, out Mapping mapping)
    {
        mapping = new Mapping();
        if (!TryFindProfile(hardwareId, name, out string stored))
        {
            return FgcResult.NotFound;
        }

        mapping = Profiles[hardwareId][stored].Clone();
        Mappings[hardwareId] = mapping.Clone();
        LastUsed[hardwareId] = stored;
        return FgcResult.Ok;
    }

    public FgcResult SaveProfile(string hardwareId, string name, Mapping mapping)
    {
        Calls.Add($"SaveProfile:{name}");
        if (!IsValidName(name))
        {
            return FgcResult.InvalidArgument;
        }

        if (!Profiles.TryGetValue(hardwareId, out Dictionary<string, Mapping>? profiles))
        {
            Profiles[hardwareId] = profiles = new Dictionary<string, Mapping>(StringComparer.OrdinalIgnoreCase);
        }

        profiles[name] = mapping.Clone();
        LastUsed[hardwareId] = name;
        return FgcResult.Ok;
    }

    public FgcResult RenameProfile(string hardwareId, string oldName, string newName)
    {
        Calls.Add($"RenameProfile:{oldName}->{newName}");
        if (!IsValidName(newName))
        {
            return FgcResult.InvalidArgument;
        }

        if (!TryFindProfile(hardwareId, oldName, out string stored))
        {
            return FgcResult.NotFound;
        }

        Dictionary<string, Mapping> profiles = Profiles[hardwareId];
        if (!string.Equals(stored, newName, StringComparison.OrdinalIgnoreCase) && profiles.ContainsKey(newName))
        {
            return FgcResult.AlreadyExists;
        }

        Mapping mapping = profiles[stored];
        profiles.Remove(stored);
        profiles[newName] = mapping;
        if (LastUsed.TryGetValue(hardwareId, out string? last) && string.Equals(last, stored, StringComparison.OrdinalIgnoreCase))
        {
            LastUsed[hardwareId] = newName;
        }

        return FgcResult.Ok;
    }

    public FgcResult DeleteProfile(string hardwareId, string name)
    {
        Calls.Add($"DeleteProfile:{name}");
        if (!TryFindProfile(hardwareId, name, out string stored))
        {
            return FgcResult.NotFound;
        }

        Profiles[hardwareId].Remove(stored);
        if (LastUsed.TryGetValue(hardwareId, out string? last) && string.Equals(last, stored, StringComparison.OrdinalIgnoreCase))
        {
            LastUsed.Remove(hardwareId);
        }

        return FgcResult.Ok;
    }

    public FgcResult GetLastUsedProfile(string hardwareId, out string name)
    {
        if (LastUsed.TryGetValue(hardwareId, out string? last) && TryFindProfile(hardwareId, last, out string stored))
        {
            name = stored;
            return FgcResult.Ok;
        }

        name = string.Empty;
        return FgcResult.NotFound;
    }

    public FgcResult CheckControllerLimit(uint virtualRequested, out ControllerLimit limit)
    {
        limit = Limit;
        return FgcResult.Ok;
    }

    public FgcResult Start(string hardwareId)
    {
        Calls.Add($"Start:{hardwareId}");
        if (StartResult == FgcResult.Ok)
        {
            Running.Add(hardwareId);
        }

        return StartResult;
    }

    public FgcResult Stop(string hardwareId)
    {
        Calls.Add($"Stop:{hardwareId}");
        Running.Remove(hardwareId);
        return FgcResult.Ok;
    }

    public FgcResult StopAll()
    {
        Calls.Add("StopAll");
        Running.Clear();
        return FgcResult.Ok;
    }

    public FgcResult IsRunning(string hardwareId, out bool running)
    {
        running = Running.Contains(hardwareId);
        return FgcResult.Ok;
    }

    public FgcResult GetRunningSessionCount(out uint count)
    {
        count = (uint)Running.Count;
        return FgcResult.Ok;
    }

    public FgcResult PollEvent(out EngineEvent? engineEvent)
    {
        engineEvent = Events.Count > 0 ? Events.Dequeue() : null;
        return FgcResult.Ok;
    }

    public FgcResult GetLatencyStats(string hardwareId, out LatencyStats stats)
    {
        Calls.Add($"GetLatencyStats:{hardwareId}");
        if (Latency.TryGetValue(hardwareId, out stats))
        {
            return FgcResult.Ok;
        }

        return FgcResult.NotRunning;
    }

    public FgcResult ResetLatencyStats(string hardwareId)
    {
        Calls.Add($"ResetLatencyStats:{hardwareId}");
        Latency[hardwareId] = default;
        return FgcResult.Ok;
    }

    public void Dispose()
    {
    }

    private static bool IsValidName(string name) => name.Length is > 0 and < 64 && name.IndexOfAny(['\\', '/', ':', '*', '?', '"', '<', '>', '|']) < 0;

    private bool TryFindProfile(string hardwareId, string name, out string stored)
    {
        stored = Profiles.TryGetValue(hardwareId, out Dictionary<string, Mapping>? profiles)
            ? profiles.Keys.FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase)) ?? string.Empty
            : string.Empty;
        return stored.Length > 0;
    }
}
