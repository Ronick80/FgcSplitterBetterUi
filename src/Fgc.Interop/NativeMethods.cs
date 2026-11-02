using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

// System32 is needed too: the same search flags apply when Windows resolves FgcCore.dll's own imports.
[assembly: DefaultDllImportSearchPaths(
    DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory | DllImportSearchPath.System32)]

namespace Fgc.Interop;

/// <summary>Owns an FgcContext. Releasing it stops all sessions and hands the keyboards back to Windows.</summary>
internal sealed class FgcContextHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public FgcContextHandle() : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle()
    {
        NativeMethods.Destroy(handle);
        return true;
    }
}

// Declarations for core/include/fgc_core.h. FgcCore.dll is x64 only, which has a single calling convention.
internal static partial class NativeMethods
{
    private const string Dll = "FgcCore";

    [LibraryImport(Dll, EntryPoint = "fgc_get_version")]
    internal static partial IntPtr GetVersion();

    [LibraryImport(Dll, EntryPoint = "fgc_get_api_version")]
    internal static partial uint GetApiVersion();

    [LibraryImport(Dll, EntryPoint = "fgc_create")]
    internal static partial int Create(out FgcContextHandle context);

    [LibraryImport(Dll, EntryPoint = "fgc_destroy")]
    internal static partial void Destroy(IntPtr context);

    [LibraryImport(Dll, EntryPoint = "fgc_get_driver_status")]
    internal static partial int GetDriverStatus(FgcContextHandle context, ref NativeDriverStatus status);

    [LibraryImport(Dll, EntryPoint = "fgc_refresh_keyboards")]
    internal static partial int RefreshKeyboards(FgcContextHandle context);

    [LibraryImport(Dll, EntryPoint = "fgc_get_keyboard_count")]
    internal static partial int GetKeyboardCount(FgcContextHandle context, out uint count);

    [LibraryImport(Dll, EntryPoint = "fgc_get_keyboard")]
    internal static partial int GetKeyboard(FgcContextHandle context, uint index, ref NativeKeyboardInfo info);

    [LibraryImport(Dll, EntryPoint = "fgc_identify_begin")]
    internal static partial int IdentifyBegin(FgcContextHandle context);

    [LibraryImport(Dll, EntryPoint = "fgc_identify_cancel")]
    internal static partial int IdentifyCancel(FgcContextHandle context);

    [LibraryImport(Dll, EntryPoint = "fgc_identify_poll")]
    internal static partial int IdentifyPoll(FgcContextHandle context, out int index);

    [LibraryImport(Dll, EntryPoint = "fgc_get_default_mapping")]
    internal static partial int GetDefaultMapping(ref NativeMapping mapping);

    [LibraryImport(Dll, EntryPoint = "fgc_get_mapping", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int GetMapping(FgcContextHandle context, string hardwareId, ref NativeMapping mapping);

    [LibraryImport(Dll, EntryPoint = "fgc_set_mapping", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int SetMapping(FgcContextHandle context, string hardwareId, in NativeMapping mapping);

    [LibraryImport(Dll, EntryPoint = "fgc_profile_count", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int ProfileCount(FgcContextHandle context, string hardwareId, out uint count);

    [LibraryImport(Dll, EntryPoint = "fgc_profile_get_name", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int ProfileGetName(FgcContextHandle context, string hardwareId, uint index, Span<byte> name, uint nameSize);

    [LibraryImport(Dll, EntryPoint = "fgc_profile_load", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int ProfileLoad(FgcContextHandle context, string hardwareId, string name, ref NativeMapping mapping);

    [LibraryImport(Dll, EntryPoint = "fgc_profile_save", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int ProfileSave(FgcContextHandle context, string hardwareId, string name, in NativeMapping mapping);

    [LibraryImport(Dll, EntryPoint = "fgc_profile_rename", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int ProfileRename(FgcContextHandle context, string hardwareId, string oldName, string newName);

    [LibraryImport(Dll, EntryPoint = "fgc_profile_delete", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int ProfileDelete(FgcContextHandle context, string hardwareId, string name);

    [LibraryImport(Dll, EntryPoint = "fgc_profile_get_last_used", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int ProfileGetLastUsed(FgcContextHandle context, string hardwareId, Span<byte> name, uint nameSize);

    [LibraryImport(Dll, EntryPoint = "fgc_check_controller_limit")]
    internal static partial int CheckControllerLimit(FgcContextHandle context, uint virtualRequested, ref NativeControllerLimit limit);

    [LibraryImport(Dll, EntryPoint = "fgc_start", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int Start(FgcContextHandle context, string hardwareId);

    [LibraryImport(Dll, EntryPoint = "fgc_stop", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int Stop(FgcContextHandle context, string hardwareId);

    [LibraryImport(Dll, EntryPoint = "fgc_stop_all")]
    internal static partial int StopAll(FgcContextHandle context);

    [LibraryImport(Dll, EntryPoint = "fgc_is_running", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int IsRunning(FgcContextHandle context, string hardwareId, out int running);

    [LibraryImport(Dll, EntryPoint = "fgc_get_status")]
    internal static partial int GetStatus(FgcContextHandle context, ref NativeStatus status);

    [LibraryImport(Dll, EntryPoint = "fgc_poll_event")]
    internal static partial int PollEvent(FgcContextHandle context, ref NativeEvent engineEvent);

    [LibraryImport(Dll, EntryPoint = "fgc_get_latency_stats", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int GetLatencyStats(FgcContextHandle context, string hardwareId, ref NativeLatencyStats stats);

    [LibraryImport(Dll, EntryPoint = "fgc_reset_latency_stats", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int ResetLatencyStats(FgcContextHandle context, string hardwareId);
}
