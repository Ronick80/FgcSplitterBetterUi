using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Fgc.Interop;

/// <summary>
/// Key codes are set-1 scan codes, plus 0x100 for E0-prefixed keys; 0 is unbound. This is what the engine stores
/// in mappings and profile files.
/// </summary>
public static class KeyCode
{
    public const ushort Unbound = 0;
    public const ushort ExtendedFlag = 0x100;

    /// <summary>From the lParam of WM_KEYDOWN: bits 16-23 are the scan code, bit 24 marks E0-prefixed keys.</summary>
    public static ushort FromKeyMessage(nint lParam)
    {
        uint value = (uint)(lParam & 0xFFFFFFFF);
        ushort scan = (ushort)((value >> 16) & 0xFF);
        return (value & (1u << 24)) != 0 ? (ushort)(scan | ExtendedFlag) : scan;
    }
}

/// <summary>Windows' display names for key codes.</summary>
public static partial class KeyNames
{
    private static readonly ConcurrentDictionary<ushort, string> Cache = new();

    [LibraryImport("user32.dll", EntryPoint = "GetKeyNameTextW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int GetKeyNameText(int lParam, Span<char> buffer, int bufferSize);

    /// <summary>False when Windows has no name for the key code.</summary>
    public static bool TryGetName(ushort keyCode, out string name)
    {
        int lParam = ((keyCode & 0xFF) << 16) | ((keyCode & KeyCode.ExtendedFlag) != 0 ? 1 << 24 : 0);
        Span<char> buffer = stackalloc char[64];
        int length = GetKeyNameText(lParam, buffer, buffer.Length);
        name = length > 0 ? new string(buffer[..length]) : string.Empty;
        return length > 0;
    }

    /// <summary>Name for display: "(none)" for 0 and a hex fallback for codes Windows does not name.</summary>
    public static string GetName(ushort keyCode) => Cache.GetOrAdd(keyCode, static code =>
    {
        if (code == KeyCode.Unbound)
        {
            return "(none)";
        }

        return TryGetName(code, out string name) ? name : $"Key 0x{code:X}";
    });

    /// <summary>Every key code Windows has a name for, for a pick-from-list control.</summary>
    public static IReadOnlyList<ushort> GetPickableKeys()
    {
        var keys = new List<ushort>();
        foreach (ushort baseCode in new ushort[] { 0, KeyCode.ExtendedFlag })
        {
            for (ushort scan = 1; scan < 0x80; scan++)
            {
                // E0 2A and E0 36 are the fake shifts the keyboard sends around some extended keys.
                bool fakeShift = baseCode != 0 && (scan == 0x2A || scan == 0x36);
                if (!fakeShift && TryGetName((ushort)(baseCode | scan), out _))
                {
                    keys.Add((ushort)(baseCode | scan));
                }
            }
        }

        return keys;
    }
}
