namespace Unchained.Pptx.Models.Shapes;

/// <summary>
///     Preset shape geometries available in OOXML DrawingML.
///     These correspond to the <c>prst</c> attribute values on <c>&lt;a:prstGeom&gt;</c>.
/// </summary>
public enum AutoShapeType
{
    // ── Lines & connectors ────────────────────────────────────────────────────

    /// <summary>A straight line.</summary>
    Line,
    /// <summary>A straight connector.</summary>
    StraightConnector,
    /// <summary>A bent (elbow) connector.</summary>
    BentConnector,
    /// <summary>A curved connector.</summary>
    CurvedConnector,

    // ── Basic shapes ──────────────────────────────────────────────────────────

    /// <summary>A rectangle.</summary>
    Rectangle,
    /// <summary>A rectangle with rounded corners.</summary>
    RoundedRectangle,
    /// <summary>A snipped single corner rectangle.</summary>
    SnipSingleCornerRectangle,
    /// <summary>A snipped same-side corner rectangle.</summary>
    SnipSameSideCornerRectangle,
    /// <summary>A snipped diagonal corner rectangle.</summary>
    SnipDiagonalCornerRectangle,
    /// <summary>A snipped and rounded single corner rectangle.</summary>
    SnipRoundSingleCornerRectangle,
    /// <summary>A rounded single corner rectangle.</summary>
    RoundSingleCornerRectangle,
    /// <summary>A rounded same-side corner rectangle.</summary>
    RoundSameSideCornerRectangle,
    /// <summary>A rounded diagonal corner rectangle.</summary>
    RoundDiagonalCornerRectangle,
    /// <summary>An ellipse (or circle when width equals height).</summary>
    Ellipse,
    /// <summary>A right triangle.</summary>
    RightTriangle,
    /// <summary>An isosceles triangle.</summary>
    IsoscelesTriangle,
    /// <summary>A trapezoid.</summary>
    Trapezoid,
    /// <summary>A parallelogram.</summary>
    Parallelogram,
    /// <summary>A regular diamond.</summary>
    Diamond,
    /// <summary>A regular pentagon.</summary>
    Pentagon,
    /// <summary>A regular hexagon.</summary>
    Hexagon,
    /// <summary>A regular heptagon.</summary>
    Heptagon,
    /// <summary>A regular octagon.</summary>
    Octagon,
    /// <summary>A regular decagon.</summary>
    Decagon,
    /// <summary>A regular dodecagon.</summary>
    Dodecagon,
    /// <summary>A pie (circular sector) shape.</summary>
    Pie,
    /// <summary>A chord shape.</summary>
    Chord,
    /// <summary>A teardrop shape.</summary>
    Teardrop,
    /// <summary>A frame shape.</summary>
    Frame,
    /// <summary>A half frame shape.</summary>
    HalfFrame,
    /// <summary>An L-shaped corner shape.</summary>
    Corner,
    /// <summary>A diagonal stripe shape.</summary>
    DiagonalStripe,
    /// <summary>A plus / cross shape.</summary>
    Plus,
    /// <summary>A donut (ring) shape.</summary>
    Donut,
    /// <summary>A no-symbol (circle with diagonal slash).</summary>
    NoSymbol,
    /// <summary>A cube.</summary>
    Cube,
    /// <summary>A can (cylinder).</summary>
    Can,
    /// <summary>A bevel shape.</summary>
    Bevel,
    /// <summary>A folded corner shape.</summary>
    FoldedCorner,
    /// <summary>A smiley face shape.</summary>
    SmileyFace,
    /// <summary>A heart shape.</summary>
    Heart,
    /// <summary>A lightning bolt shape.</summary>
    LightningBolt,
    /// <summary>A sun shape.</summary>
    Sun,
    /// <summary>A moon shape.</summary>
    Moon,
    /// <summary>A cloud shape.</summary>
    Cloud,
    /// <summary>An arc shape.</summary>
    Arc,
    /// <summary>A wave shape.</summary>
    Wave,
    /// <summary>A double wave shape.</summary>
    DoubleWave,

    // ── Block arrows ──────────────────────────────────────────────────────────

    /// <summary>A right-pointing block arrow.</summary>
    RightArrow,
    /// <summary>A left-pointing block arrow.</summary>
    LeftArrow,
    /// <summary>An upward-pointing block arrow.</summary>
    UpArrow,
    /// <summary>A downward-pointing block arrow.</summary>
    DownArrow,
    /// <summary>A left-right block arrow.</summary>
    LeftRightArrow,
    /// <summary>An up-down block arrow.</summary>
    UpDownArrow,
    /// <summary>A four-directional block arrow.</summary>
    QuadArrow,
    /// <summary>A left-right-up block arrow.</summary>
    LeftRightUpArrow,
    /// <summary>A bent-up block arrow.</summary>
    BentUpArrow,
    /// <summary>A bent block arrow.</summary>
    BentArrow,
    /// <summary>A U-turn block arrow.</summary>
    UTurnArrow,
    /// <summary>A circular block arrow.</summary>
    CircularArrow,
    /// <summary>A notched right block arrow.</summary>
    NotchedRightArrow,
    /// <summary>A striped right block arrow.</summary>
    StripedRightArrow,

    // ── Stars & ribbons ───────────────────────────────────────────────────────

    /// <summary>A four-pointed star.</summary>
    Star4,
    /// <summary>A five-pointed star.</summary>
    Star5,
    /// <summary>A six-pointed star.</summary>
    Star6,
    /// <summary>A seven-pointed star.</summary>
    Star7,
    /// <summary>An eight-pointed star.</summary>
    Star8,
    /// <summary>A ten-pointed star.</summary>
    Star10,
    /// <summary>A twelve-pointed star.</summary>
    Star12,
    /// <summary>A sixteen-pointed star.</summary>
    Star16,
    /// <summary>A twenty-four-pointed star.</summary>
    Star24,
    /// <summary>A thirty-two-pointed star.</summary>
    Star32,
    /// <summary>An irregular star (explosion).</summary>
    IrregularSeal1,
    /// <summary>An irregular star 2 (explosion).</summary>
    IrregularSeal2,
    /// <summary>A ribbon shape.</summary>
    Ribbon,
    /// <summary>A ribbon 2 shape.</summary>
    Ribbon2,
    /// <summary>An ellipse ribbon.</summary>
    EllipseRibbon,
    /// <summary>An ellipse ribbon 2.</summary>
    EllipseRibbon2,

    // ── Callouts ──────────────────────────────────────────────────────────────

    /// <summary>A rectangular callout.</summary>
    RectangularCallout,
    /// <summary>A rounded rectangular callout.</summary>
    RoundedRectangularCallout,
    /// <summary>An oval callout.</summary>
    OvalCallout,
    /// <summary>A cloud callout.</summary>
    CloudCallout,
    /// <summary>A line callout 1.</summary>
    LineCallout1,
    /// <summary>A line callout 2.</summary>
    LineCallout2,
    /// <summary>A line callout 3.</summary>
    LineCallout3,
    /// <summary>An accent line callout 1.</summary>
    AccentCallout1,
    /// <summary>An accent line callout 2.</summary>
    AccentCallout2,
    /// <summary>An accent line callout 3.</summary>
    AccentCallout3,

    // ── Flowchart ─────────────────────────────────────────────────────────────

    /// <summary>Flowchart: process (rectangle).</summary>
    FlowChartProcess,
    /// <summary>Flowchart: decision (diamond).</summary>
    FlowChartDecision,
    /// <summary>Flowchart: data (parallelogram).</summary>
    FlowChartInputOutput,
    /// <summary>Flowchart: predefined process.</summary>
    FlowChartPredefinedProcess,
    /// <summary>Flowchart: internal storage.</summary>
    FlowChartInternalStorage,
    /// <summary>Flowchart: document.</summary>
    FlowChartDocument,
    /// <summary>Flowchart: multi-document.</summary>
    FlowChartMultiDocument,
    /// <summary>Flowchart: terminator.</summary>
    FlowChartTerminator,
    /// <summary>Flowchart: preparation (hexagon).</summary>
    FlowChartPreparation,
    /// <summary>Flowchart: manual input.</summary>
    FlowChartManualInput,
    /// <summary>Flowchart: manual operation.</summary>
    FlowChartManualOperation,
    /// <summary>Flowchart: connector.</summary>
    FlowChartConnector,
    /// <summary>Flowchart: off-page connector.</summary>
    FlowChartOffPageConnector,
    /// <summary>Flowchart: punch card.</summary>
    FlowChartPunchedCard,
    /// <summary>Flowchart: punched tape.</summary>
    FlowChartPunchedTape,
    /// <summary>Flowchart: summing junction.</summary>
    FlowChartSummingJunction,
    /// <summary>Flowchart: OR gate.</summary>
    FlowChartOr,
    /// <summary>Flowchart: collate.</summary>
    FlowChartCollate,
    /// <summary>Flowchart: sort.</summary>
    FlowChartSort,
    /// <summary>Flowchart: extract.</summary>
    FlowChartExtract,
    /// <summary>Flowchart: merge.</summary>
    FlowChartMerge,
    /// <summary>Flowchart: offline storage.</summary>
    FlowChartOfflineStorage,
    /// <summary>Flowchart: online storage.</summary>
    FlowChartOnlineStorage,
    /// <summary>Flowchart: magnetic tape.</summary>
    FlowChartMagneticTape,
    /// <summary>Flowchart: magnetic disk.</summary>
    FlowChartMagneticDisk,
    /// <summary>Flowchart: magnetic drum.</summary>
    FlowChartMagneticDrum,
    /// <summary>Flowchart: display.</summary>
    FlowChartDisplay,
    /// <summary>Flowchart: delay.</summary>
    FlowChartDelay,

    // ── Math ──────────────────────────────────────────────────────────────────

    /// <summary>A math addition (+) shape.</summary>
    MathPlus,
    /// <summary>A math subtraction (−) shape.</summary>
    MathMinus,
    /// <summary>A math multiplication (×) shape.</summary>
    MathMultiply,
    /// <summary>A math division (÷) shape.</summary>
    MathDivide,
    /// <summary>A math equal (=) shape.</summary>
    MathEqual,
    /// <summary>A math not-equal (≠) shape.</summary>
    MathNotEqual,

    /// <summary>
    ///     A custom geometry defined by the author. The OOXML element will contain
    ///     a <c>&lt;a:custGeom&gt;</c> child instead of <c>&lt;a:prstGeom&gt;</c>.
    /// </summary>
    Custom = 0x7FFF_FFFF
}
