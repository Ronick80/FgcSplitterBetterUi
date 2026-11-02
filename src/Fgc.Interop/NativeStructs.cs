using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Fgc.Interop;

// Mirrors the structs in fgc_core.h. Every struct starts with its own size; strings are fixed UTF-8 buffers.

[InlineArray(256)]
internal struct HardwareIdBuffer
{
    private byte _element;
}

[InlineArray(128)]
internal struct DisplayNameBuffer
{
    private byte _element;
}

[InlineArray(25)]
internal struct KeyBuffer
{
    private ushort _element;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeDriverStatus
{
    public uint StructSize;
    public int Interception;
    public int ScpVBusInstalled;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeMapping
{
    public uint StructSize;
    public KeyBuffer KeyForInput;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeKeyboardInfo
{
    public uint StructSize;
    public HardwareIdBuffer HardwareId;
    public DisplayNameBuffer DisplayName;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeControllerLimit
{
    public uint StructSize;
    public uint PhysicalConnected;
    public uint VirtualActive;
    public uint VirtualRequested;
    public int Allowed;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeStatus
{
    public uint StructSize;
    public uint RunningSessions;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeEvent
{
    public uint StructSize;
    public int Type;
    public HardwareIdBuffer HardwareId;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeLatencyStats
{
    public uint StructSize;
    public ulong SampleCount;
    public ulong LastNs;
    public ulong AverageNs;
    public ulong MaxNs;
    public ulong ReadAverageNs;
    public ulong ReadMaxNs;
    public ulong ReportAverageNs;
    public ulong ReportMaxNs;
}

internal static class NativeText
{
    public static string FromUtf8(ReadOnlySpan<byte> buffer)
    {
        int length = buffer.IndexOf((byte)0);
        return System.Text.Encoding.UTF8.GetString(length < 0 ? buffer : buffer[..length]);
    }
}
