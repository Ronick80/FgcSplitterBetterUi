using System.Windows;
using System.Windows.Media;
using Fgc.Interop;

namespace FgcKeyboardSplitter.ViewModels;

/// <summary>
/// One input on the arcade stick drawing. <see cref="Shape"/> lights up; <see cref="Glyph"/> is a stroked symbol drawn
/// on it. The key name sits in <see cref="KeyBox"/>: inside the shape for the round buttons, or on a key label
/// (<see cref="OnChip"/>) beside the smaller inputs, joined by <see cref="Leader"/> when it is set.
/// <see cref="Label"/> names the Xbox input inside a round button.
/// </summary>
public sealed record ArcadeSpot(
    Geometry Shape,
    Geometry? Glyph,
    string? Label,
    Rect LabelBox,
    Rect KeyBox,
    bool OnChip,
    PointCollection? Leader)
{
    public bool InShape => !OnChip;
}

/// <summary>
/// The arcade stick drawing, seen from above: a lever whose four directions are the d-pad, eight buttons in the usual
/// curved layout with L3 and R3 below them, and Back, Guide and Start on the top edge. The analog sticks are not on
/// it. Coordinates are canvas units, the same canvas size as <see cref="GamepadLayout"/>.
/// </summary>
public static class ArcadeStickLayout
{
    public const double CanvasWidth = GamepadLayout.CanvasWidth;
    public const double CanvasHeight = GamepadLayout.CanvasHeight;

    private const double ButtonRadius = 44;
    private const double ChipWidth = 110;
    private const double TopChipWidth = 96;
    private const double ChipHeight = GamepadLayout.ChipHeight;
    private const double ArrowOffset = 80;

    private static readonly Point Lever = new(315, 340);

    // The buttons are placed from one corner with fixed steps, so every gap matches: X to Y is the curve's
    // stagger, the rest of the top row is evenly spaced, and each button sits the same step below the one above it
    // (the bottom row below the top row, and L3 and R3 below A and B).
    private static readonly Point TopLeft = new(615, 262);
    private static readonly Vector Stagger = new(110, -38);
    private static readonly Vector Pitch = new(115, 0);
    private static readonly Vector Down = new(-18, 106);

    public static Geometry Body { get; } = Frozen(new RectangleGeometry(new Rect(40, 70, 1060, 510), 40, 40));

    /// <summary>The top plate the lever and buttons sit in.</summary>
    public static Geometry Plate { get; } = Frozen(new RectangleGeometry(new Rect(72, 124, 996, 428), 18, 18));

    /// <summary>The strip on the top edge that holds Back, Guide and Start.</summary>
    public static Geometry FunctionStrip { get; } = Frozen(new RectangleGeometry(new Rect(470, 80, 200, 34), 17, 17));

    /// <summary>The square gate and dust washer around the lever.</summary>
    public static Geometry LeverRings { get; } = Group(
        new RectangleGeometry(new Rect(Lever.X - 48, Lever.Y - 48, 96, 96), 20, 20),
        new EllipseGeometry(Lever, 34, 34));

    /// <summary>The ball top, seen from above.</summary>
    public static Geometry LeverBall { get; } = Frozen(new EllipseGeometry(Lever, 22, 22));

    public static IReadOnlyDictionary<XboxInput, ArcadeSpot> Spots { get; } = new Dictionary<XboxInput, ArcadeSpot>
    {
        // The lever's four directions; each key label sits just beyond its arrow.
        [XboxInput.DpadUp] = Arrow(0, -ArrowOffset, new Rect(Lever.X - ChipWidth / 2, Lever.Y - 130 - ChipHeight / 2, ChipWidth, ChipHeight)),
        [XboxInput.DpadDown] = Arrow(0, ArrowOffset, new Rect(Lever.X - ChipWidth / 2, Lever.Y + 130 - ChipHeight / 2, ChipWidth, ChipHeight)),
        [XboxInput.DpadLeft] = Arrow(-ArrowOffset, 0, new Rect(Lever.X - 110 - ChipWidth, Lever.Y - ChipHeight / 2, ChipWidth, ChipHeight)),
        [XboxInput.DpadRight] = Arrow(ArrowOffset, 0, new Rect(Lever.X + 110, Lever.Y - ChipHeight / 2, ChipWidth, ChipHeight)),

        // Top row, left to right
        [XboxInput.X] = Button(TopLeft, "X"),
        [XboxInput.Y] = Button(TopLeft + Stagger, "Y"),
        [XboxInput.RightBumper] = Button(TopLeft + Stagger + Pitch, "RB"),
        [XboxInput.LeftBumper] = Button(TopLeft + Stagger + 2 * Pitch, "LB"),

        // Bottom row, left to right
        [XboxInput.A] = Button(TopLeft + Down, "A"),
        [XboxInput.B] = Button(TopLeft + Stagger + Down, "B"),
        [XboxInput.RightTrigger] = Button(TopLeft + Stagger + Pitch + Down, "RT"),
        [XboxInput.LeftTrigger] = Button(TopLeft + Stagger + 2 * Pitch + Down, "LT"),

        // Thumb buttons, the same step below A and B
        [XboxInput.LeftStickClick] = Button(TopLeft + 2 * Down, "L3"),
        [XboxInput.RightStickClick] = Button(TopLeft + Stagger + 2 * Down, "R3"),

        // Top edge, key labels above the case
        [XboxInput.Back] = Function(520, 430, "M 512,90 H 521 V 99 H 512 Z M 518,95 H 527 V 104 H 518 Z"),
        [XboxInput.Guide] = Function(570, 570, "M 563,90 L 577,104 M 577,90 L 563,104"),
        [XboxInput.Start] = Function(620, 710, "M 613,92 H 627 M 613,97 H 627 M 613,102 H 627"),
    };

    // A block arrow pointing away from the lever, offset (dx, dy) from it.
    private static ArcadeSpot Arrow(double dx, double dy, Rect chip)
    {
        // Drawn pointing up around (0, 0), then turned to face its direction.
        var arrow = Geometry.Parse("M -21,0 L 0,-23 L 21,0 L 10,0 L 10,21 L -10,21 L -10,0 Z").CloneCurrentValue();
        double angle = dx > 0 ? 90 : dx < 0 ? -90 : dy > 0 ? 180 : 0;
        var transform = new TransformGroup();
        transform.Children.Add(new RotateTransform(angle));
        transform.Children.Add(new TranslateTransform(Lever.X + dx, Lever.Y + dy));
        arrow.Transform = transform;
        return new ArcadeSpot(Frozen(arrow), null, null, Rect.Empty, chip, OnChip: true, Leader: null);
    }

    private static ArcadeSpot Button(Point center, string label) => new(
        Frozen(new EllipseGeometry(center, ButtonRadius, ButtonRadius)),
        null,
        label,
        new Rect(center.X - 30, center.Y - 34, 60, 20),
        new Rect(center.X - 36, center.Y - 14, 72, 42),
        OnChip: false,
        Leader: null);

    // A small round button on the top edge, with its key label in the row above the case.
    private static ArcadeSpot Function(double x, double chipCenterX, string glyph)
    {
        const double y = 97;
        var chip = new Rect(chipCenterX - TopChipWidth / 2, 18, TopChipWidth, ChipHeight);
        var leader = new PointCollection([new Point(chipCenterX, chip.Bottom), new Point(x, y - 13)]);
        leader.Freeze();
        return new ArcadeSpot(
            Frozen(new EllipseGeometry(new Point(x, y), 13, 13)),
            Frozen(Geometry.Parse(glyph)),
            null,
            Rect.Empty,
            chip,
            OnChip: true,
            leader);
    }

    private static Geometry Group(params Geometry[] parts)
    {
        var group = new GeometryGroup();
        foreach (Geometry part in parts)
        {
            group.Children.Add(part);
        }

        return Frozen(group);
    }

    private static Geometry Frozen(Geometry geometry)
    {
        geometry.Freeze();
        return geometry;
    }
}
