using Fgc.Interop;

namespace FgcKeyboardSplitter.ViewModels;

public enum ChipAccent
{
    None,
    A,
    B,
    X,
    Y,
}

/// <summary>Where an input's chip sits on the controller drawing, in canvas units.</summary>
public readonly record struct DiagramSpot(double X, double Y, string ShortLabel, ChipAccent Accent = ChipAccent.None);

public static class GamepadLayout
{
    public const double CanvasWidth = 850;
    public const double CanvasHeight = 450;
    public const double ChipWidth = 76;
    public const double ChipHeight = 44;

    public static IReadOnlyDictionary<XboxInput, DiagramSpot> Spots { get; } = new Dictionary<XboxInput, DiagramSpot>
    {
        [XboxInput.LeftTrigger] = new(96, 8, "LT"),
        [XboxInput.LeftBumper] = new(96, 56, "LB"),
        [XboxInput.RightTrigger] = new(678, 8, "RT"),
        [XboxInput.RightBumper] = new(678, 56, "RB"),

        [XboxInput.Back] = new(332, 96, "Back"),
        [XboxInput.Guide] = new(422, 96, "Guide"),
        [XboxInput.Start] = new(512, 96, "Start"),

        [XboxInput.LeftStickUp] = new(170, 112, "LS ↑"),
        [XboxInput.LeftStickLeft] = new(90, 160, "LS ←"),
        [XboxInput.LeftStickClick] = new(170, 160, "L3"),
        [XboxInput.LeftStickRight] = new(250, 160, "LS →"),
        [XboxInput.LeftStickDown] = new(170, 208, "LS ↓"),

        [XboxInput.DpadUp] = new(300, 262, "D ↑"),
        [XboxInput.DpadLeft] = new(220, 310, "D ←"),
        [XboxInput.DpadRight] = new(380, 310, "D →"),
        [XboxInput.DpadDown] = new(300, 358, "D ↓"),

        [XboxInput.Y] = new(654, 112, "Y", ChipAccent.Y),
        [XboxInput.X] = new(574, 160, "X", ChipAccent.X),
        [XboxInput.B] = new(734, 160, "B", ChipAccent.B),
        [XboxInput.A] = new(654, 208, "A", ChipAccent.A),

        [XboxInput.RightStickUp] = new(550, 262, "RS ↑"),
        [XboxInput.RightStickLeft] = new(470, 310, "RS ←"),
        [XboxInput.RightStickClick] = new(550, 310, "R3"),
        [XboxInput.RightStickRight] = new(630, 310, "RS →"),
        [XboxInput.RightStickDown] = new(550, 358, "RS ↓"),
    };
}
