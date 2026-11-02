using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Fgc.Interop;

public sealed class FgcException(FgcResult result, string message) : Exception(message)
{
    public FgcResult Result { get; } = result;
}

/// <summary>
/// One FgcCore context. Calls that the C API labels [control] may come from any thread; <see cref="Create"/> and
/// <see cref="Dispose"/> must not overlap with any other call. Methods return the engine's result code and fill
/// their out value only on success.
/// </summary>
public sealed class FgcClient : IFgcClient
{
    /// <summary>The C API version this wrapper was written for.</summary>
    public const uint SupportedApiVersion = 1;

    private const int HardwareIdMax = 256;
    private const int ProfileNameMax = 64;

    private readonly FgcContextHandle _context;

    private FgcClient(FgcContextHandle context) => _context = context;

    public static string EngineVersion => Marshal.PtrToStringUTF8(NativeMethods.GetVersion()) ?? string.Empty;

    public static uint ApiVersion => NativeMethods.GetApiVersion();

    /// <summary>Throws <see cref="DllNotFoundException"/> when FgcCore.dll is not next to the program.</summary>
    public static FgcClient Create()
    {
        uint api = ApiVersion;
        if (api != SupportedApiVersion)
        {
            throw new FgcException(FgcResult.NotImplemented,
                $"FgcCore reports API version {api}, but this program supports version {SupportedApiVersion}. Use a matching FgcCore.dll.");
        }

        var result = (FgcResult)NativeMethods.Create(out FgcContextHandle context);
        if (result != FgcResult.Ok)
        {
            context.Dispose();
            throw new FgcException(result, FgcErrors.Describe(result));
        }

        return new FgcClient(context);
    }

    /// <summary>Stops all sessions, restores keyboards and releases the context.</summary>
    public void Dispose() => _context.Dispose();

    public FgcResult GetDriverStatus(out DriverStatus status)
    {
        var native = new NativeDriverStatus { StructSize = Size<NativeDriverStatus>() };
        var result = (FgcResult)NativeMethods.GetDriverStatus(_context, ref native);
        status = result == FgcResult.Ok
            ? new DriverStatus((InterceptionState)native.Interception, native.ScpVBusInstalled != 0)
            : default;
        return result;
    }

    /// <summary>Rescans devices and replaces the cached list.</summary>
    public FgcResult RefreshKeyboards() => (FgcResult)NativeMethods.RefreshKeyboards(_context);

    /// <summary>Reads the list cached by the last <see cref="RefreshKeyboards"/>.</summary>
    public FgcResult GetKeyboards(out IReadOnlyList<KeyboardInfo> keyboards)
    {
        keyboards = [];
        var result = (FgcResult)NativeMethods.GetKeyboardCount(_context, out uint count);
        if (result != FgcResult.Ok)
        {
            return result;
        }

        var list = new List<KeyboardInfo>((int)count);
        for (uint i = 0; i < count; i++)
        {
            var native = new NativeKeyboardInfo { StructSize = Size<NativeKeyboardInfo>() };
            if ((FgcResult)NativeMethods.GetKeyboard(_context, i, ref native) == FgcResult.Ok)
            {
                list.Add(new KeyboardInfo(NativeText.FromUtf8(native.HardwareId), NativeText.FromUtf8(native.DisplayName)));
            }
        }

        keyboards = list;
        return FgcResult.Ok;
    }

    /// <summary>Starts watching all idle keyboards; their keys still reach Windows normally.</summary>
    public FgcResult IdentifyBegin() => (FgcResult)NativeMethods.IdentifyBegin(_context);

    public FgcResult IdentifyCancel() => (FgcResult)NativeMethods.IdentifyCancel(_context);

    /// <summary>Index into the list from <see cref="GetKeyboards"/>, or -1 while no key was pressed.</summary>
    public FgcResult IdentifyPoll(out int index)
    {
        var result = (FgcResult)NativeMethods.IdentifyPoll(_context, out index);
        if (result != FgcResult.Ok)
        {
            index = -1;
        }

        return result;
    }

    /// <summary>The mapping used for keyboards that have none of their own.</summary>
    public static Mapping GetDefaultMapping()
    {
        var native = new NativeMapping { StructSize = Size<NativeMapping>() };
        NativeMethods.GetDefaultMapping(ref native);
        return Mapping.FromNative(native);
    }

    public Mapping CreateDefaultMapping() => GetDefaultMapping();

    /// <summary>The keyboard's current mapping; a keyboard with none starts from its last-used profile.</summary>
    public FgcResult GetMapping(string hardwareId, out Mapping mapping)
    {
        var native = new NativeMapping { StructSize = Size<NativeMapping>() };
        var result = (FgcResult)NativeMethods.GetMapping(_context, hardwareId, ref native);
        mapping = result == FgcResult.Ok ? Mapping.FromNative(native) : new Mapping();
        return result;
    }

    /// <summary>Takes effect immediately on a running keyboard. Not saved to any profile.</summary>
    public FgcResult SetMapping(string hardwareId, Mapping mapping)
    {
        NativeMapping native = mapping.ToNative();
        return (FgcResult)NativeMethods.SetMapping(_context, hardwareId, in native);
    }

    /// <summary>Profile names for the keyboard, sorted case-insensitively.</summary>
    public FgcResult GetProfileNames(string hardwareId, out IReadOnlyList<string> names)
    {
        names = [];
        var result = (FgcResult)NativeMethods.ProfileCount(_context, hardwareId, out uint count);
        if (result != FgcResult.Ok)
        {
            return result;
        }

        var list = new List<string>((int)count);
        Span<byte> buffer = stackalloc byte[ProfileNameMax];
        for (uint i = 0; i < count; i++)
        {
            if ((FgcResult)NativeMethods.ProfileGetName(_context, hardwareId, i, buffer, (uint)buffer.Length) == FgcResult.Ok)
            {
                list.Add(NativeText.FromUtf8(buffer));
            }
        }

        names = list;
        return FgcResult.Ok;
    }

    /// <summary>Makes the profile the keyboard's current mapping and marks it as last used.</summary>
    public FgcResult LoadProfile(string hardwareId, string name, out Mapping mapping)
    {
        var native = new NativeMapping { StructSize = Size<NativeMapping>() };
        var result = (FgcResult)NativeMethods.ProfileLoad(_context, hardwareId, name, ref native);
        mapping = result == FgcResult.Ok ? Mapping.FromNative(native) : new Mapping();
        return result;
    }

    /// <summary>Creates or replaces a profile and marks it as last used.</summary>
    public FgcResult SaveProfile(string hardwareId, string name, Mapping mapping)
    {
        NativeMapping native = mapping.ToNative();
        return (FgcResult)NativeMethods.ProfileSave(_context, hardwareId, name, in native);
    }

    public FgcResult RenameProfile(string hardwareId, string oldName, string newName) =>
        (FgcResult)NativeMethods.ProfileRename(_context, hardwareId, oldName, newName);

    public FgcResult DeleteProfile(string hardwareId, string name) =>
        (FgcResult)NativeMethods.ProfileDelete(_context, hardwareId, name);

    /// <summary><see cref="FgcResult.NotFound"/> when the keyboard has no last-used profile.</summary>
    public FgcResult GetLastUsedProfile(string hardwareId, out string name)
    {
        Span<byte> buffer = stackalloc byte[ProfileNameMax];
        var result = (FgcResult)NativeMethods.ProfileGetLastUsed(_context, hardwareId, buffer, (uint)buffer.Length);
        name = result == FgcResult.Ok ? NativeText.FromUtf8(buffer) : string.Empty;
        return result;
    }

    /// <summary>Counts XInput controllers and this library's virtual pads, plus the pads about to be added.</summary>
    public FgcResult CheckControllerLimit(uint virtualRequested, out ControllerLimit limit)
    {
        var native = new NativeControllerLimit { StructSize = Size<NativeControllerLimit>() };
        var result = (FgcResult)NativeMethods.CheckControllerLimit(_context, virtualRequested, ref native);
        limit = result == FgcResult.Ok
            ? new ControllerLimit(native.PhysicalConnected, native.VirtualActive, native.VirtualRequested, native.Allowed != 0)
            : default;
        return result;
    }

    public FgcResult Start(string hardwareId) => (FgcResult)NativeMethods.Start(_context, hardwareId);

    public FgcResult Stop(string hardwareId) => (FgcResult)NativeMethods.Stop(_context, hardwareId);

    public FgcResult StopAll() => (FgcResult)NativeMethods.StopAll(_context);

    public FgcResult IsRunning(string hardwareId, out bool running)
    {
        var result = (FgcResult)NativeMethods.IsRunning(_context, hardwareId, out int value);
        running = result == FgcResult.Ok && value != 0;
        return result;
    }

    public FgcResult GetRunningSessionCount(out uint count)
    {
        var native = new NativeStatus { StructSize = Size<NativeStatus>() };
        var result = (FgcResult)NativeMethods.GetStatus(_context, ref native);
        count = result == FgcResult.Ok ? native.RunningSessions : 0;
        return result;
    }

    /// <summary>Pops one event. Returns Ok with a null event when the queue is empty.</summary>
    public FgcResult PollEvent(out EngineEvent? engineEvent)
    {
        engineEvent = null;
        var native = new NativeEvent { StructSize = Size<NativeEvent>() };
        var result = (FgcResult)NativeMethods.PollEvent(_context, ref native);
        if (result == FgcResult.Ok && (EngineEventType)native.Type != EngineEventType.None)
        {
            engineEvent = new EngineEvent((EngineEventType)native.Type, NativeText.FromUtf8(native.HardwareId));
        }

        return result;
    }

    /// <summary><see cref="FgcResult.NotRunning"/> when the keyboard has no session.</summary>
    public FgcResult GetLatencyStats(string hardwareId, out LatencyStats stats)
    {
        var native = new NativeLatencyStats { StructSize = Size<NativeLatencyStats>() };
        var result = (FgcResult)NativeMethods.GetLatencyStats(_context, hardwareId, ref native);
        stats = result == FgcResult.Ok
            ? new LatencyStats(native.SampleCount, native.LastNs, native.AverageNs, native.MaxNs,
                native.ReadAverageNs, native.ReadMaxNs, native.ReportAverageNs, native.ReportMaxNs)
            : default;
        return result;
    }

    public FgcResult ResetLatencyStats(string hardwareId) =>
        (FgcResult)NativeMethods.ResetLatencyStats(_context, hardwareId);

    private static uint Size<T>() where T : struct => (uint)Unsafe.SizeOf<T>();
}
