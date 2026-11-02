using System.Runtime.CompilerServices;

namespace Fgc.Interop.Tests;

// Sizes of the C structs in fgc_core.h under MSVC x64. The engine rejects a struct_size that is too small, so a
// wrong layout here shows up as InvalidArgument in the engine tests too.
public class StructLayoutTests
{
    [Fact]
    public void DriverStatus() => Assert.Equal(12, Unsafe.SizeOf<NativeDriverStatus>());

    [Fact]
    public void Mapping() => Assert.Equal(56, Unsafe.SizeOf<NativeMapping>());

    [Fact]
    public void KeyboardInfo() => Assert.Equal(388, Unsafe.SizeOf<NativeKeyboardInfo>());

    [Fact]
    public void ControllerLimit() => Assert.Equal(20, Unsafe.SizeOf<NativeControllerLimit>());

    [Fact]
    public void Status() => Assert.Equal(8, Unsafe.SizeOf<NativeStatus>());

    [Fact]
    public void Event() => Assert.Equal(264, Unsafe.SizeOf<NativeEvent>());

    [Fact]
    public void LatencyStats() => Assert.Equal(72, Unsafe.SizeOf<NativeLatencyStats>());

    [Fact]
    public void InputCountMatchesTheEngine() => Assert.Equal(25, (int)XboxInput.Count);
}
