using System.Reflection;

namespace FgcKeyboardSplitter.Services;

public static class AppInfo
{
    /// <summary>The app version from version.txt, for example 0.1.0.</summary>
    public static string Version { get; } = Assembly.GetEntryAssembly()?
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";
}
