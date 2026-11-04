namespace Fgc.Interop;

/// <summary>
/// One keyboard interface. A physical device can expose several; <paramref name="DeviceId"/> is shared by the
/// interfaces of one device, and empty when it cannot be told apart from an identical device.
/// </summary>
public sealed record KeyboardInfo(string HardwareId, string DisplayName, string ProductName = "", string DeviceId = "");

public readonly record struct DriverStatus(InterceptionState Interception, bool ScpVBusInstalled)
{
    public bool Ready => Interception == InterceptionState.Installed && ScpVBusInstalled;
}

public readonly record struct ControllerLimit(uint PhysicalConnected, uint VirtualActive, uint VirtualRequested, bool Allowed);

/// <summary>Application latency of one running session, in nanoseconds.</summary>
public readonly record struct LatencyStats(
    ulong SampleCount,
    ulong LastNs,
    ulong AverageNs,
    ulong MaxNs,
    ulong ReadAverageNs,
    ulong ReadMaxNs,
    ulong ReportAverageNs,
    ulong ReportMaxNs);

public readonly record struct EngineEvent(EngineEventType Type, string HardwareId);

/// <summary>A key code for each Xbox input. See <see cref="KeyCode"/>; 0 means unbound.</summary>
public sealed class Mapping : IEquatable<Mapping>
{
    private readonly ushort[] _keys;

    public Mapping() => _keys = new ushort[(int)XboxInput.Count];

    private Mapping(ushort[] keys) => _keys = keys;

    public ushort this[XboxInput input]
    {
        get => _keys[(int)input];
        set => _keys[(int)input] = value;
    }

    public Mapping Clone() => new((ushort[])_keys.Clone());

    public bool Equals(Mapping? other) => other is not null && _keys.AsSpan().SequenceEqual(other._keys);

    public override bool Equals(object? obj) => Equals(obj as Mapping);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (ushort key in _keys)
        {
            hash.Add(key);
        }

        return hash.ToHashCode();
    }

    internal NativeMapping ToNative()
    {
        var native = new NativeMapping { StructSize = (uint)System.Runtime.CompilerServices.Unsafe.SizeOf<NativeMapping>() };
        Span<ushort> keys = native.KeyForInput;
        _keys.CopyTo(keys);
        return native;
    }

    internal static Mapping FromNative(in NativeMapping native)
    {
        var keys = new ushort[(int)XboxInput.Count];
        ReadOnlySpan<ushort> source = native.KeyForInput;
        source.CopyTo(keys);
        return new Mapping(keys);
    }
}
