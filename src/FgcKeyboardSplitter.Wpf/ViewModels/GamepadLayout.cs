using System.Windows;
using System.Windows.Media;
using Fgc.Interop;

namespace FgcKeyboardSplitter.ViewModels;

/// <summary>Where an input's key label sits: in the column left or right of the controller, or in the row above it.</summary>
public enum ChipSide
{
    Left,
    Right,
    Top,
}

/// <summary>
/// One input on the controller drawing. <see cref="Shape"/> lights up on hover; <see cref="Glyph"/> is a stroked
/// symbol drawn on it; <see cref="Letter"/> is the A/B/X/Y lettering. The key label sits in slot
/// <see cref="Slot"/> on <see cref="Side"/>, joined to <see cref="Anchor"/> by a leader line.
/// </summary>
public sealed record DiagramSpot(
    Geometry Shape,
    bool Outlined,
    Geometry? Glyph,
    string? Letter,
    Point Center,
    Point Anchor,
    ChipSide Side,
    int Slot);

/// <summary>The controller drawing. Coordinates are canvas units; the drawing is scaled to fit by the view.</summary>
public static class GamepadLayout
{
    public const double CanvasWidth = 1140;
    public const double CanvasHeight = 600;
    public const double ChipHeight = 36;
    public const double SideChipWidth = 170;
    public const double TopChipWidth = 96;
    public const double LetterBox = 34;

    // The controller body is drawn in its own 640-unit-wide space and shifted into the canvas by this offset.
    private const double OriginX = 250;
    private const double OriginY = 100;
    private const double LeftChipX = 24;
    private const double RightChipX = 946;
    private const double TopChipY = 22;
    private const double FirstSlotCenterY = 100;
    private const double SlotSpacing = 46;
    private static readonly double[] TopChipCenterX = [465, 570, 675];

    public static Geometry Body { get; } = PathAt(
        "M 120,88 C 140,50 200,42 254,48 L 386,48 C 440,42 500,50 520,88 C 560,110 590,200 612,320 " +
        "C 628,400 630,440 600,452 C 570,464 540,452 520,425 C 490,385 460,350 420,345 L 220,345 " +
        "C 180,350 150,385 120,425 C 100,452 70,464 40,452 C 10,440 12,400 28,320 C 50,200 80,110 120,88 Z");

    /// <summary>The strip between the bumpers and the curve that frames the guide button.</summary>
    public static Geometry FaceLines { get; } = PathAt(
        "M 254,48 L 254,62 L 386,62 L 386,48 M 254,62 C 284,64 290,128 320,130 C 350,128 356,64 386,62");

    /// <summary>The dim outer rings around both sticks and the d-pad.</summary>
    public static Geometry OuterRings { get; } = Group(Circle(175, 165, 44), Circle(400, 268, 44), Circle(240, 268, 54));

    /// <summary>The bright rings of both stick tops, around the clickable centre.</summary>
    public static Geometry StickRings { get; } = Group(Circle(175, 165, 34), Circle(400, 268, 34));

    /// <summary>Drawn over the d-pad arms so the cross keeps its outline when an arm lights up.</summary>
    public static Geometry DpadCross { get; } =
        PathAt("M 227,224 H 253 V 255 H 284 V 281 H 253 V 312 H 227 V 281 H 196 V 255 H 227 Z");

    public static IReadOnlyDictionary<XboxInput, DiagramSpot> Spots { get; } = new Dictionary<XboxInput, DiagramSpot>
    {
        // Left column, top to bottom
        [XboxInput.LeftTrigger] = Spot(Rounded(138, 6, 104, 28, 14), (190, 20), (138, 20), ChipSide.Left, 0),
        [XboxInput.LeftBumper] = Spot(PathAt("M 120,88 C 140,50 200,42 254,48 L 254,62 C 202,58 158,66 136,100 Z"), (180, 62), (130, 80), ChipSide.Left, 1),
        [XboxInput.LeftStickUp] = Spot(Triangle((165, 115), (185, 115), (175, 101)), (175, 110), (175, 101), ChipSide.Left, 2),
        [XboxInput.LeftStickLeft] = Spot(Triangle((125, 155), (125, 175), (111, 165)), (120, 165), (111, 165), ChipSide.Left, 3),
        [XboxInput.LeftStickClick] = Spot(Circle(175, 165, 25), (175, 165), (158, 183), ChipSide.Left, 4),
        [XboxInput.LeftStickRight] = Spot(Triangle((225, 155), (225, 175), (239, 165)), (230, 165), (239, 165), ChipSide.Left, 5),
        [XboxInput.LeftStickDown] = Spot(Triangle((165, 215), (185, 215), (175, 229)), (175, 220), (175, 229), ChipSide.Left, 6),
        [XboxInput.DpadUp] = Arm("M 227,224 H 253 V 255 L 240,266 L 227,255 Z", (240, 240), (240, 226), 7),
        [XboxInput.DpadLeft] = Arm("M 196,255 V 281 H 227 L 238,268 L 227,255 Z", (212, 268), (198, 268), 8),
        [XboxInput.DpadRight] = Arm("M 284,255 V 281 H 253 L 242,268 L 253,255 Z", (268, 268), (282, 268), 9),
        [XboxInput.DpadDown] = Arm("M 227,312 H 253 V 281 L 240,270 L 227,281 Z", (240, 296), (240, 310), 10),

        // Right column, top to bottom
        [XboxInput.RightTrigger] = Spot(Rounded(398, 6, 104, 28, 14), (450, 20), (502, 20), ChipSide.Right, 0),
        [XboxInput.RightBumper] = Spot(PathAt("M 520,88 C 500,50 440,42 386,48 L 386,62 C 438,58 482,66 504,100 Z"), (460, 62), (510, 80), ChipSide.Right, 1),
        [XboxInput.Y] = Face(488, 127, "Y", (488, 110), 2),
        [XboxInput.B] = Face(526, 165, "B", (543, 165), 3),
        [XboxInput.X] = Face(450, 165, "X", (450, 182), 4),
        [XboxInput.A] = Face(488, 203, "A", (488, 220), 5),
        [XboxInput.RightStickUp] = Spot(Triangle((390, 218), (410, 218), (400, 204)), (400, 213), (400, 204), ChipSide.Right, 6),
        [XboxInput.RightStickRight] = Spot(Triangle((450, 258), (450, 278), (464, 268)), (455, 268), (464, 268), ChipSide.Right, 7),
        [XboxInput.RightStickClick] = Spot(Circle(400, 268, 25), (400, 268), (418, 286), ChipSide.Right, 8),
        [XboxInput.RightStickLeft] = Spot(Triangle((350, 258), (350, 278), (336, 268)), (345, 268), (336, 268), ChipSide.Right, 9),
        [XboxInput.RightStickDown] = Spot(Triangle((390, 318), (410, 318), (400, 332)), (400, 323), (400, 332), ChipSide.Right, 10),

        // Row above the controller, left to right
        [XboxInput.Back] = Spot(Circle(268, 165, 14), (268, 165), (268, 151), ChipSide.Top, 0,
            glyph: PathAt("M 260,158 H 269 V 167 H 260 Z M 266,163 H 275 V 172 H 266 Z")),
        [XboxInput.Guide] = Spot(Circle(320, 98, 22), (320, 98), (320, 76), ChipSide.Top, 1,
            glyph: PathAt("M 310,88 L 330,108 M 330,88 L 310,108")),
        [XboxInput.Start] = Spot(Circle(372, 165, 14), (372, 165), (372, 151), ChipSide.Top, 2,
            glyph: PathAt("M 365,160 H 379 M 365,165 H 379 M 365,170 H 379")),
    };

    /// <summary>The rectangle of an input's key label on the canvas.</summary>
    public static Rect ChipBounds(DiagramSpot spot) => spot.Side switch
    {
        ChipSide.Left => new Rect(LeftChipX, SlotCenterY(spot.Slot) - ChipHeight / 2, SideChipWidth, ChipHeight),
        ChipSide.Right => new Rect(RightChipX, SlotCenterY(spot.Slot) - ChipHeight / 2, SideChipWidth, ChipHeight),
        _ => new Rect(TopChipCenterX[spot.Slot] - TopChipWidth / 2, TopChipY, TopChipWidth, ChipHeight),
    };

    /// <summary>Where the leader line leaves the key label: the edge that faces the controller.</summary>
    public static Point LeaderStart(DiagramSpot spot)
    {
        Rect chip = ChipBounds(spot);
        return spot.Side switch
        {
            ChipSide.Left => new Point(chip.Right, chip.Top + ChipHeight / 2),
            ChipSide.Right => new Point(chip.Left, chip.Top + ChipHeight / 2),
            _ => new Point(chip.Left + chip.Width / 2, chip.Bottom),
        };
    }

    private static double SlotCenterY(int slot) => FirstSlotCenterY + slot * SlotSpacing;

    private static DiagramSpot Spot(Geometry shape, (double X, double Y) center, (double X, double Y) anchor, ChipSide side, int slot,
        Geometry? glyph = null, string? letter = null, bool outlined = true) =>
        new(shape, outlined, glyph, letter, At(center), At(anchor), side, slot);

    private static DiagramSpot Face(double x, double y, string letter, (double X, double Y) anchor, int slot) =>
        Spot(Circle(x, y, 17), (x, y), anchor, ChipSide.Right, slot, letter: letter);

    // D-pad arms carry no outline of their own; DpadCross draws it on top.
    private static DiagramSpot Arm(string data, (double X, double Y) center, (double X, double Y) anchor, int slot) =>
        Spot(PathAt(data), center, anchor, ChipSide.Left, slot, outlined: false);

    private static Point At((double X, double Y) local) => new(local.X + OriginX, local.Y + OriginY);

    private static Geometry Circle(double x, double y, double radius) =>
        Frozen(new EllipseGeometry(At((x, y)), radius, radius));

    private static Geometry Rounded(double x, double y, double width, double height, double radius) =>
        Frozen(new RectangleGeometry(new Rect(At((x, y)), new Size(width, height)), radius, radius));

    private static Geometry Triangle((double X, double Y) a, (double X, double Y) b, (double X, double Y) c) =>
        PathAt($"M {a.X},{a.Y} L {b.X},{b.Y} L {c.X},{c.Y} Z");

    private static Geometry PathAt(string data)
    {
        Geometry geometry = Geometry.Parse(data).CloneCurrentValue();
        geometry.Transform = new TranslateTransform(OriginX, OriginY);
        return Frozen(geometry);
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
