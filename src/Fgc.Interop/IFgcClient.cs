namespace Fgc.Interop;

/// <summary>The engine calls the app uses, so view models can be tested with a fake engine.</summary>
public interface IFgcClient : IDisposable
{
    FgcResult GetDriverStatus(out DriverStatus status);

    FgcResult RefreshKeyboards();

    FgcResult GetKeyboards(out IReadOnlyList<KeyboardInfo> keyboards);

    FgcResult IdentifyBegin();

    FgcResult IdentifyCancel();

    FgcResult IdentifyPoll(out int index);

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
