namespace Unchained.Ooxml.Drawing;

/// <summary>
///     Describes the outline (border) of a shape or text, including its colour, width,
///     dash style, and arrowhead settings.
/// </summary>
public sealed class LineFormat
{
    /// <summary>
    ///     The line width in points. <see langword="null" /> means no explicit width is set
    ///     (the reader uses its default, typically 0.75 pt).
    /// </summary>
    public double? WidthPoints { get; set; }

    /// <summary>The dash pattern of the line. Defaults to <see cref="LineDashStyle.Solid" />.</summary>
    public LineDashStyle DashStyle { get; set; } = LineDashStyle.Solid;

    /// <summary>The cap style at the ends of the line.</summary>
    public LineCapStyle CapStyle { get; set; } = LineCapStyle.Flat;

    /// <summary>The join style at corners.</summary>
    public LineJoinStyle JoinStyle { get; set; } = LineJoinStyle.Miter;

    /// <summary>The fill applied to the line itself (usually a solid colour).</summary>
    public FillFormat Fill { get; } = new();

    /// <summary>Settings for the arrowhead at the start (head) of the line.</summary>
    public ArrowFormat HeadArrow { get; } = new();

    /// <summary>Settings for the arrowhead at the end (tail) of the line.</summary>
    public TailArrow TailArrow { get; } = new();

    // ── Convenience mutators ─────────────────────────────────────────────────

    /// <summary>Sets the line to have no visible outline.</summary>
    public void SetNone() => Fill.SetNone();

    /// <summary>Sets the line to a solid colour with the given width.</summary>
    /// <param name="color">The line colour.</param>
    /// <param name="widthPoints">The line width in points.</param>
    public void SetSolid(ColorSpec color, double widthPoints = 1.0)
    {
        WidthPoints = widthPoints;
        Fill.SetSolid(color);
    }
}
