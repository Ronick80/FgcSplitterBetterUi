namespace Fgc.Interop;

/// <summary>User-facing texts for engine results.</summary>
public static class FgcErrors
{
    public static string Describe(FgcResult result) => result switch
    {
        FgcResult.Ok => "OK",
        FgcResult.InvalidArgument => "Invalid request",
        FgcResult.NotImplemented => "Not implemented",
        FgcResult.BufferTooSmall => "Buffer too small",
        FgcResult.DriverMissing => "A driver is missing or not active yet. Check the Drivers page",
        FgcResult.AlreadyRunning => "This keyboard is already running",
        FgcResult.NotRunning => "This keyboard is not running",
        FgcResult.ControllerLimit => "No free controller slot (Windows allows 4; ScpVBus slots can also be stuck until it restarts)",
        FgcResult.NotFound => "Not found (the keyboard may have been unplugged; refresh the list)",
        FgcResult.Io => "Could not talk to the driver. Close other programs that use this keyboard and try again",
        FgcResult.Internal => "Unexpected error",
        FgcResult.AccessDenied => "Access denied (run as administrator)",
        FgcResult.DeviceBusy => "Keyboard is in use by another program",
        FgcResult.BadFormat => "The file is damaged or from a newer version",
        FgcResult.AlreadyExists => "A profile with that name already exists",
        FgcResult.ReadOnly => "Built-in profiles cannot be changed. Use Save as to keep a copy under another name",
        _ => "Unknown error",
    };

    /// <summary>Profile names become file names, so invalid ones are the common failure when saving or renaming.</summary>
    public static string DescribeNameError(FgcResult result) => result == FgcResult.InvalidArgument
        ? "Use 1-63 characters without \\ / : * ? \" < > |, with no leading or trailing space or trailing dot."
        : Describe(result);
}
