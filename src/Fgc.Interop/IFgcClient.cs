namespace Fgc.Interop;

/// <summary>The engine calls the app uses, so view models can be tested with a fake engine.</summary>
public interface IFgcClient : IDisposable
{
    FgcResult GetDriverStatus(out DriverStatus status);

    FgcResult RefreshKeyboards();

    FgcResult GetKeyboards(out IReadOnlyList<KeyboardInfo> keyboards);

    /// <summary>Starts reporting every key press on idle keyboards. Their keys still reach Windows normally.</summary>
    FgcResult WatchBegin();

    FgcResult WatchEnd();

    /// <summary>The oldest unreported key press: the keyboard interface it came from, or null when none is queued.</summary>
    FgcResult WatchPoll(out KeyboardInfo? pressed);

    Mapping CreateDefaultMapping();

    FgcResult GetMapping(string hardwareId, out Mapping mapping);

    FgcResult SetMapping(string hardwareId, Mapping mapping);

    FgcResult GetProfileNames(string hardwareId, out IReadOnlyList<string> names);

    /// <summary>True for a built-in profile, which cannot be saved over, renamed or deleted.</summary>
    bool IsReadOnlyProfile(string name);

    FgcResult LoadProfile(string hardwareId, string name, out Mapping mapping);

    FgcResult SaveProfile(string hardwareId, string name, Mapping mapping);

    FgcResult RenameProfile(string hardwareId, string oldName, string newName);

    FgcResult DeleteProfile(string hardwareId, string name);

    FgcResult GetLastUsedProfile(string hardwareId, out string name);

    /// <summary>
    /// The inputs a running keyboard holds right now: bit <c>1 &lt;&lt; (int)input</c> per <see cref="XboxInput"/>.
    /// <see cref="FgcResult.NotRunning"/> when the keyboard is not running.
    /// </summary>
    FgcResult GetHeldInputs(string hardwareId, out uint inputs);

    /// <summary>
    /// Key presses on a running keyboard since it started (repeats excluded; wraps around), so a change shows which
    /// running keyboard is in use. <see cref="FgcResult.NotRunning"/> when the keyboard is not running.
    /// </summary>
    FgcResult GetKeyPressCount(string hardwareId, out uint count);

    FgcResult CheckControllerLimit(uint virtualRequested, out ControllerLimit limit);

    FgcResult Start(string hardwareId);

    FgcResult Stop(string hardwareId);

    FgcResult StopAll();

    FgcResult IsRunning(string hardwareId, out bool running);

    FgcResult GetRunningSessionCount(out uint count);

    FgcResult PollEvent(out EngineEvent? engineEvent);

    FgcResult GetLatencyStats(string hardwareId, out LatencyStats stats);

    FgcResult ResetLatencyStats(string hardwareId);
}
