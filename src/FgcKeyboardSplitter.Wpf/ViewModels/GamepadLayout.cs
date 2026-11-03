using System.Windows;
using System.Windows.Media;
using Fgc.Interop;

namespace FgcKeyboardSplitter.ViewModels;

/// <summary>
/// One input on the controller drawing. <see cref="Shape"/> lights up on hover; <see cref="Glyph"/> is a stroked
/// symbol drawn on it; <see cref="Letter"/> is the A/B/X/Y lettering. Its key label is <see cref="Chip"/>, inside one
/// of the <see cref="GamepadLayout.Groups"/> or in the row above the controller. Inputs whose group has no line of
/// its own (<see cref="HasLeader"/>) get a leader line from the label to <see cref="Anchor"/>.
/// </summary>
public sealed record DiagramSpot(
    Geometry Shape,
    bool Outlined,
    Geometry? Glyph,
    string? Letter,
    Point Center,
    Point Anchor,
    Rect Chip,
    bool HasLeader);

/// <summary>A small caption beside a key label, naming the input where the label's position alone does not.</summary>
public sealed record GroupCaption(string Text, Rect Bounds);

/// <summary>
/// A titled panel of key labels for inputs that belong together, laid out like the controls themselves: a stick or
/// the d-pad as a cross, the face buttons as a diamond. <see cref="Leader"/> joins the panel to its control.
/// </summary>
public sealed record DiagramGroup(string Title, Rect Bounds, PointCollection? Leader, IReadOnlyList<GroupCaption> Captions)
{
    /// <summary>Where the group's line meets its control, for the dot drawn there.</summary>
    public Rect LeaderDot => Leader is { Count: > 0 } points
        ? new Rect(points[^1].X - 5, points[^1].Y - 5, 10, 10)
        : Rect.Empty;

    public bool HasLeader => Leader is not null;
}

/// <summary>The controller drawing. Coordinates are canvas units; the drawing is scaled to fit by the view.</summary>
public static class GamepadLayout
{
    public const double CanvasWidth = 1400;
    public const double CanvasHeight = 600;
    public const double ChipHeight = 36;
    public const double TopChipWidth = 96;
    public const double LetterBox = 34;
    public const double GroupTitleHeight = 26;

    // The controller body is drawn in its own 640-unit-wide space and shifted into the canvas by this offset.
    private const double OriginX = 380;
    private const double OriginY = 100;
    private const double TopChipY = 22;
    private static readonly double[] TopChipCenterX = [595, 700, 805];

    // Group panels: a column on each side of the controller, top to bottom.
    private const double LeftX = 20;
    private const double RightX = 1040;
    private const double GroupWidth = 340;
    private const double ShoulderTop = 78;
    private const double ShoulderHeight = 114;
    private const double MiddleTop = 210;
    private const double LowerTop = 384;
    private const double CrossHeight = 156;
    private const double CellWidth = 98;
    private const double CellStep = 106;
    private const double RowStep = 42;
    private const double Padding = 14; // room for the slant and shadow of the labels
    private const double CaptionWidth = 64;

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
        // Left trigger and bumper, each joined to its control
        [XboxInput.LeftTrigger] = Spot(Rounded(138, 6, 104, 28, 14), (190, 20), (138, 20), Shoulder(LeftX, 0), leader: true),
        [XboxInput.LeftBumper] = Spot(PathAt("M 120,88 C 140,50 200,42 254,48 L 254,62 C 202,58 158,66 136,100 Z"), (180, 62), (130, 80), Shoulder(LeftX, 1), leader: true),

        // Left stick: a cross with the click in the middle
        [XboxInput.LeftStickUp] = Spot(Triangle((165, 115), (185, 115), (175, 101)), (175, 110), (175, 101), Cell(LeftX, MiddleTop, 1, 0)),
        [XboxInput.LeftStickLeft] = Spot(Triangle((125, 155), (125, 175), (111, 165)), (120, 165), (111, 165), Cell(LeftX, MiddleTop, 0, 1)),
        [XboxInput.LeftStickClick] = Spot(Circle(175, 165, 25), (175, 165), (158, 183), Cell(LeftX, MiddleTop, 1, 1)),
        [XboxInput.LeftStickRight] = Spot(Triangle((225, 155), (225, 175), (239, 165)), (230, 165), (239, 165), Cell(LeftX, MiddleTop, 2, 1)),
        [XboxInput.LeftStickDown] = Spot(Triangle((165, 215), (185, 215), (175, 229)), (175, 220), (175, 229), Cell(LeftX, MiddleTop, 1, 2)),

        // D-pad: a cross
        [XboxInput.DpadUp] = Arm("M 227,224 H 253 V 255 L 240,266 L 227,255 Z", (240, 240), (240, 226), Cell(LeftX, LowerTop, 1, 0)),
        [XboxInput.DpadLeft] = Arm("M 196,255 V 281 H 227 L 238,268 L 227,255 Z", (212, 268), (198, 268), Cell(LeftX, LowerTop, 0, 1)),
        [XboxInput.DpadRight] = Arm("M 284,255 V 281 H 253 L 242,268 L 253,255 Z", (268, 268), (282, 268), Cell(LeftX, LowerTop, 2, 1)),
        [XboxInput.DpadDown] = Arm("M 227,312 H 253 V 281 L 240,270 L 227,281 Z", (240, 296), (240, 310), Cell(LeftX, LowerTop, 1, 2)),

        // Right trigger and bumper, each joined to its control
        [XboxInput.RightTrigger] = Spot(Rounded(398, 6, 104, 28, 14), (450, 20), (502, 20), Shoulder(RightX, 0), leader: true),
        [XboxInput.RightBumper] = Spot(PathAt("M 520,88 C 500,50 440,42 386,48 L 386,62 C 438,58 482,66 504,100 Z"), (460, 62), (510, 80), Shoulder(RightX, 1), leader: true),

        // Face buttons: a diamond, as on the controller
        [XboxInput.Y] = Face(488, 127, "Y", (488, 110), Cell(RightX, MiddleTop, 1, 0)),
        [XboxInput.X] = Face(450, 165, "X", (450, 182), Cell(RightX, MiddleTop, 0, 1)),
        [XboxInput.B] = Face(526, 165, "B", (543, 165), Cell(RightX, MiddleTop, 2, 1)),
        [XboxInput.A] = Face(488, 203, "A", (488, 220), Cell(RightX, MiddleTop, 1, 2)),

        // Right stick: a cross with the click in the middle
        [XboxInput.RightStickUp] = Spot(Triangle((390, 218), (410, 218), (400, 204)), (400, 213), (400, 204), Cell(RightX, LowerTop, 1, 0)),
        [XboxInput.RightStickLeft] = Spot(Triangle((350, 258), (350, 278), (336, 268)), (345, 268), (336, 268), Cell(RightX, LowerTop, 0, 1)),
        [XboxInput.RightStickClick] = Spot(Circle(400, 268, 25), (400, 268), (418, 286), Cell(RightX, LowerTop, 1, 1)),
        [XboxInput.RightStickRight] = Spot(Triangle((450, 258), (450, 278), (464, 268)), (455, 268), (464, 268), Cell(RightX, LowerTop, 2, 1)),
        [XboxInput.RightStickDown] = Spot(Triangle((390, 318), (410, 318), (400, 332)), (400, 323), (400, 332), Cell(RightX, LowerTop, 1, 2)),

        // Row above the controller, left to right, each joined to its button
        [XboxInput.Back] = Spot(Circle(268, 165, 14), (268, 165), (268, 151), Top(0), leader: true,
            glyph: PathAt("M 260,158 H 269 V 167 H 260 Z M 266,163 H 275 V 172 H 266 Z")),
        [XboxInput.Guide] = Spot(Circle(320, 98, 22), (320, 98), (320, 76), Top(1), leader: true,
            glyph: PathAt("M 310,88 L 330,108 M 330,88 L 310,108")),
        [XboxInput.Start] = Spot(Circle(372, 165, 14), (372, 165), (372, 151), Top(2), leader: true,
            glyph: PathAt("M 365,160 H 379 M 365,165 H 379 M 365,170 H 379")),
    };

    /// <summary>The panels the key labels sit in. Stick, d-pad and face panels have one line to their control.</summary>
    public static IReadOnlyList<DiagramGroup> Groups { get; } =
    [
        new("TRIGGER / BUMPER", new Rect(LeftX, ShoulderTop, GroupWidth, ShoulderHeight), null,
            [Caption("LT", LeftX, 0), Caption("LB", LeftX, 1)]),
        Cross("LEFT STICK", LeftX, MiddleTop, At((131, 165))),
        Cross("D-PAD", LeftX, LowerTop, At((186, 268))),
        new("TRIGGER / BUMPER", new Rect(RightX, ShoulderTop, GroupWidth, ShoulderHeight), null,
            [Caption("RT", RightX, 0), Caption("RB", RightX, 1)]),
        Cross("FACE BUTTONS", RightX, MiddleTop, At((543, 165))),
        Cross("RIGHT STICK", RightX, LowerTop, At((444, 268))),
    ];

    /// <summary>The rectangle of an input's key label on the canvas.</summary>
    public static Rect ChipBounds(DiagramSpot spot) => spot.Chip;

    /// <summary>Where the leader line leaves the key label: the edge that faces the controller.</summary>
    public static Point LeaderStart(DiagramSpot spot)
    {
        Rect chip = spot.Chip;
        if (chip.Bottom <= OriginY)
        {
            return new Point(chip.Left + chip.Width / 2, chip.Bottom);
        }

        return chip.Left < OriginX
            ? new Point(chip.Right, chip.Top + chip.Height / 2)
            : new Point(chip.Left, chip.Top + chip.Height / 2);
    }

    // A label cell in a 3 x 3 grid inside a group panel.
    private static Rect Cell(double groupLeft, double groupTop, int column, int row) =>
        new(groupLeft + Padding + column * CellStep, groupTop + GroupTitleHeight + row * RowStep, CellWidth, ChipHeight);

    // A trigger or bumper label, wide, with its caption on the outer side.
    private static Rect Shoulder(double groupLeft, int row)
    {
        double width = GroupWidth - 3 * Padding - CaptionWidth;
        double left = groupLeft < OriginX ? groupLeft + 2 * Padding + CaptionWidth : groupLeft + Padding;
        return new Rect(left, ShoulderTop + GroupTitleHeight + row * RowStep, width, ChipHeight);
    }

    private static GroupCaption Caption(string text, double groupLeft, int row)
    {
        double left = groupLeft < OriginX ? groupLeft + Padding : groupLeft + GroupWidth - Padding - CaptionWidth;
        return new GroupCaption(text, new Rect(left, ShoulderTop + GroupTitleHeight + row * RowStep, CaptionWidth, ChipHeight));
    }

    private static Rect Top(int index) =>
        new(TopChipCenterX[index] - TopChipWidth / 2, TopChipY, TopChipWidth, ChipHeight);

    // A 3 x 3 panel with one line from its inner edge, level with the middle row, to the control.
    private static DiagramGroup Cross(string title, double left, double top, Point control)
    {
        var bounds = new Rect(left, top, GroupWidth, CrossHeight);
        double edge = left < OriginX ? bounds.Right : bounds.Left;
        var leader = new PointCollection([new Point(edge, top + GroupTitleHeight + RowStep + ChipHeight / 2), control]);
        leader.Freeze();
        return new DiagramGroup(title, bounds, leader, []);
    }

    private static DiagramSpot Spot(Geometry shape, (double X, double Y) center, (double X, double Y) anchor, Rect chip,
        bool leader = false, Geometry? glyph = null, string? letter = null, bool outlined = true) =>
        new(shape, outlined, glyph, letter, At(center), At(anchor), chip, leader);

    private static DiagramSpot Face(double x, double y, string letter, (double X, double Y) anchor, Rect chip) =>
        Spot(Circle(x, y, 17), (x, y), anchor, chip, letter: letter);

    // D-pad arms carry no outline of their own; DpadCross draws it on top.
    private static DiagramSpot Arm(string data, (double X, double Y) center, (double X, double Y) anchor, Rect chip) =>
        Spot(PathAt(data), center, anchor, chip, outlined: false);

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
