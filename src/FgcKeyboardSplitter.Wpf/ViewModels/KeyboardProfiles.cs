using CommunityToolkit.Mvvm.ComponentModel;
using Fgc.Interop;

namespace FgcKeyboardSplitter.ViewModels;

/// <summary>The profile a keyboard's mapping came from, and whether it was changed since.</summary>
public sealed partial class KeyboardProfile : ObservableObject
{
    /// <summary>Empty when the mapping belongs to no profile, for example after its profile was deleted.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _current = string.Empty;

    /// <summary>The mapping was changed since the profile was loaded or saved.</summary>
    [ObservableProperty]
    private bool _dirty;

    public string DisplayName => Current.Length > 0 ? Current : "No profile";
}

/// <summary>
/// One <see cref="KeyboardProfile"/> per keyboard, shared by the Keyboards page (which shows it) and the Mapping page
/// (which changes it). It outlives the keyboard's card, so a keyboard that is cleared and joins again keeps it.
/// </summary>
public sealed class KeyboardProfiles(IFgcClient client)
{
    private readonly Dictionary<string, KeyboardProfile> _profiles = [];

    /// <summary>The keyboard's profile; the first time, the profile the engine starts it from.</summary>
    public KeyboardProfile For(string hardwareId)
    {
        if (!_profiles.TryGetValue(hardwareId, out KeyboardProfile? profile))
        {
            profile = new KeyboardProfile
            {
                Current = client.GetLastUsedProfile(hardwareId, out string lastUsed) == FgcResult.Ok ? lastUsed : string.Empty,
            };
            _profiles[hardwareId] = profile;
        }

        return profile;
    }
}
