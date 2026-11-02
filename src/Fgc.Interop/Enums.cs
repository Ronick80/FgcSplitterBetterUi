namespace Fgc.Interop;

public enum FgcResult
{
    Ok = 0,
    InvalidArgument = 1,
    NotImplemented = 2,
    BufferTooSmall = 3,
    DriverMissing = 4,
    AlreadyRunning = 5,
    NotRunning = 6,
    ControllerLimit = 7,
    NotFound = 8,
    Io = 9,
    Internal = 10,
    AccessDenied = 11,
    DeviceBusy = 12,
    BadFormat = 13,
    AlreadyExists = 14,
}

public enum InterceptionState
{
    NotInstalled = 0,
    RebootRequired = 1,
    Installed = 2,
}

/// <summary>Xbox inputs a key can be bound to. Analog directions are held at full deflection.</summary>
public enum XboxInput
{
    A = 0,
    B,
    X,
    Y,
    LeftBumper,
    RightBumper,
    LeftTrigger,
    RightTrigger,
    LeftStickClick,
    RightStickClick,
    Start,
    Back,
    Guide,
    DpadUp,
    DpadDown,
    DpadLeft,
    DpadRight,
    LeftStickUp,
    LeftStickDown,
    LeftStickLeft,
    LeftStickRight,
    RightStickUp,
    RightStickDown,
    RightStickLeft,
    RightStickRight,
    Count,
}

public enum EngineEventType
{
    None = 0,
    KeyboardDisconnected = 1,
    SessionFaulted = 2,
}
