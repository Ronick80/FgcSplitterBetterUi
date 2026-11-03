using System.Text.RegularExpressions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Fgc.Interop.Tests;

public sealed class EngineFixture : IDisposable
{
    public FgcClient Client { get; } = FgcClient.Create();

    public void Dispose() => Client.Dispose();
}

[CollectionDefinition("engine")]
public class EngineCollection : ICollectionFixture<EngineFixture>
{
}

// Talks to the real FgcCore.dll. Nothing here starts a session, so no keyboard is ever captured.
[Collection("engine")]
public class EngineTests(EngineFixture engine)
{
    private readonly FgcClient _client = engine.Client;

    [Fact]
    public void ReportsVersions()
    {
        Assert.Equal(FgcClient.SupportedApiVersion, FgcClient.ApiVersion);
        Assert.Matches(@"^\d+\.\d+\.\d+$", FgcClient.EngineVersion);
    }

    [Fact]
    public void HeldInputsNeedARunningKeyboard()
    {
        Assert.Equal(FgcResult.NotRunning, _client.GetHeldInputs(@"TEST\NO_SUCH_KEYBOARD", out uint inputs));
        Assert.Equal(0u, inputs);
    }

    [Fact]
    public void Lists2XkoDefaultFirstAsAReadOnlyProfile()
    {
        Assert.Equal(FgcResult.Ok, _client.GetProfileNames(@"TEST\NO_SUCH_KEYBOARD", out IReadOnlyList<string> names));

        Assert.Equal("2XKO Default", names[0]);
        Assert.True(_client.IsReadOnlyProfile("2xko default"));
        Assert.False(_client.IsReadOnlyProfile("Some profile"));
    }

    [Fact]
    public void DefaultMappingBindsJToA()
    {
        Mapping mapping = FgcClient.GetDefaultMapping();

        Assert.Equal(0x24, mapping[XboxInput.A]);
        Assert.NotEqual(KeyCode.Unbound, mapping[XboxInput.DpadUp]);
    }

    [Fact]
    public void DriverStatusIsReadable() => Assert.Equal(FgcResult.Ok, _client.GetDriverStatus(out _));

    [Fact]
    public void KeyboardListIsReadable()
    {
        Assert.Equal(FgcResult.Ok, _client.RefreshKeyboards());
        Assert.Equal(FgcResult.Ok, _client.GetKeyboards(out IReadOnlyList<KeyboardInfo> keyboards));
        Assert.All(keyboards, keyboard => Assert.False(string.IsNullOrEmpty(keyboard.HardwareId)));
    }

    [Fact]
    public void ControllerLimitIsReadable()
    {
        Assert.Equal(FgcResult.Ok, _client.CheckControllerLimit(1, out ControllerLimit limit));
        Assert.Equal(1u, limit.VirtualRequested);
    }

    [Fact]
    public void NothingIsRunning()
    {
        Assert.Equal(FgcResult.Ok, _client.GetRunningSessionCount(out uint count));
        Assert.Equal(0u, count);
        Assert.Equal(FgcResult.Ok, _client.PollEvent(out EngineEvent? engineEvent));
        Assert.Null(engineEvent);
    }

    [Fact]
    public void LatencyOfAKeyboardThatIsNotRunning()
    {
        FgcResult result = _client.GetLatencyStats(@"HID\NOT_A_KEYBOARD", out _);

        Assert.True(result is FgcResult.NotRunning or FgcResult.NotFound, $"Unexpected result {result}");
    }

    [Fact]
    public void ProfileRoundTrip()
    {
        string hardwareId = @"FGC_INTEROP_TEST\" + Guid.NewGuid().ToString("N");
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FgcKeyboardSplitter", "profiles", Regex.Replace(hardwareId, @"[\\/:*?""<>|]", "_"));
        try
        {
            Mapping mapping = FgcClient.GetDefaultMapping();
            mapping[XboxInput.A] = 0x148;

            Assert.Equal(FgcResult.Ok, _client.SaveProfile(hardwareId, "First", mapping));
            Assert.Equal(FgcResult.Ok, _client.GetProfileNames(hardwareId, out IReadOnlyList<string> names));
            Assert.Equal(["2XKO Default", "First"], names);
            Assert.Equal(FgcResult.Ok, _client.GetLastUsedProfile(hardwareId, out string lastUsed));
            Assert.Equal("First", lastUsed);

            Assert.Equal(FgcResult.Ok, _client.RenameProfile(hardwareId, "First", "Second"));
            Assert.Equal(FgcResult.Ok, _client.LoadProfile(hardwareId, "Second", out Mapping loaded));
            Assert.Equal(mapping, loaded);

            Assert.Equal(FgcResult.InvalidArgument, _client.SaveProfile(hardwareId, "bad/name", mapping));
            Assert.Equal(FgcResult.ReadOnly, _client.SaveProfile(hardwareId, "2xko default", mapping));
            Assert.Equal(FgcResult.ReadOnly, _client.DeleteProfile(hardwareId, "2XKO Default"));
            Assert.Equal(FgcResult.AlreadyExists, _client.RenameProfile(hardwareId, "Second", "2XKO Default"));
            Assert.Equal(FgcResult.Ok, _client.DeleteProfile(hardwareId, "Second"));
            Assert.Equal(FgcResult.NotFound, _client.GetLastUsedProfile(hardwareId, out _));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
