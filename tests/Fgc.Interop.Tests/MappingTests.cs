namespace Fgc.Interop.Tests;

public class MappingTests
{
    [Fact]
    public void NativeRoundTripKeepsEveryKey()
    {
        var mapping = new Mapping();
        for (int i = 0; i < (int)XboxInput.Count; i++)
        {
            mapping[(XboxInput)i] = (ushort)(i == 3 ? 0x148 : i + 1);
        }

        Mapping copy = Mapping.FromNative(mapping.ToNative());

        Assert.Equal(mapping, copy);
        Assert.Equal(0x148, copy[XboxInput.Y]);
    }

    [Fact]
    public void CloneIsIndependent()
    {
        var original = new Mapping();
        original[XboxInput.A] = 0x24;

        Mapping clone = original.Clone();
        clone[XboxInput.A] = 0x25;

        Assert.Equal(0x24, original[XboxInput.A]);
        Assert.NotEqual(original, clone);
    }

    [Theory]
    [InlineData(0x001E0001, 0x1E)]
    [InlineData(0x01480001, 0x148)]
    public void KeyCodeFromKeyMessage(int lParam, int expected) =>
        Assert.Equal(expected, KeyCode.FromKeyMessage(lParam));
}

public class KeyNamesTests
{
    [Fact]
    public void NamesCommonKeys()
    {
        Assert.True(KeyNames.TryGetName(0x1E, out string a));
        Assert.False(string.IsNullOrWhiteSpace(a));
        Assert.Equal("(none)", KeyNames.GetName(KeyCode.Unbound));
    }

    [Fact]
    public void UnnamedKeyFallsBackToHex() => Assert.StartsWith("Key 0x", KeyNames.GetName(0x1FF));

    [Fact]
    public void PickableKeysAreUniqueAndNonEmpty()
    {
        IReadOnlyList<ushort> keys = KeyNames.GetPickableKeys();

        Assert.NotEmpty(keys);
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.DoesNotContain(keys, key => key == (KeyCode.ExtendedFlag | 0x2A));
    }
}
