using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;

namespace Unchained.Pptx.Shapes;

/// <summary>
/// The abstract base class for all shapes on a slide.
/// Provides position, size, rotation, identity, and formatting properties
/// that are shared by every shape type.
/// </summary>
public abstract class Shape
{
    // ── Position and size ────────────────────────────────────────────────────

    /// <summary>Horizontal position of the shape's top-left corner, measured from the slide's left edge.</summary>
    public Emu X { get; set; }

    /// <summary>Vertical position of the shape's top-left corner, measured from the slide's top edge.</summary>
    public Emu Y { get; set; }

    /// <summary>Width of the shape's bounding box.</summary>
    public Emu Width { get; set; }

    /// <summary>Height of the shape's bounding box.</summary>
    public Emu Height { get; set; }

    /// <summary>
    /// Clockwise rotation angle in degrees.
    /// 0° means no rotation; 90° rotates the shape a quarter-turn clockwise.
    /// </summary>
    public double RotationDegrees { get; set; }

    /// <summary><see langword="true"/> when the shape is flipped horizontally.</summary>
    public bool FlipHorizontal { get; set; }

    /// <summary><see langword="true"/> when the shape is flipped vertically.</summary>
    public bool FlipVertical { get; set; }

    // ── Identity ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The shape identifier, unique within the slide.
    /// Assigned automatically when a shape is added to a <see cref="ShapeCollection"/>.
    /// </summary>
    public uint ShapeId { get; internal set; }

    /// <summary>The display name of the shape (e.g. "Title 1", "TextBox 3").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Alternative (accessibility) text that describes the shape for screen readers.
    /// <see langword="null"/> means no alt text is set.
    /// </summary>
    public string? AltText { get; set; }

    /// <summary>
    /// A short title for the alt text, displayed in some accessibility tools.
    /// <see langword="null"/> means no title is set.
    /// </summary>
    public string? AltTextTitle { get; set; }

    /// <summary><see langword="true"/> when the shape is hidden during slide show playback.</summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// <see langword="true"/> when the shape is marked as purely decorative
    /// (it carries no meaningful content and is ignored by screen readers).
    /// </summary>
    public bool IsDecorative { get; set; }

    // ── Formatting ───────────────────────────────────────────────────────────

    /// <summary>The fill applied to the interior of the shape.</summary>
    public FillFormat Fill { get; } = new();

    /// <summary>
    /// The fill inherited from the shape's <c>p:style/a:fillRef</c> element.
    /// This is the theme-driven style fill that applies when <see cref="Fill"/> has
    /// <see cref="FillType.None"/>. <see langword="null"/> when no style fill is present.
    /// </summary>
    public ColorSpec? StyleFillColor { get; internal set; }

    /// <summary>
    /// The default text color inherited from the shape's <c>p:style/a:fontRef</c> element.
    /// Used as the text color fallback when runs have no explicit fill. <see langword="null"/>
    /// when no style font ref is present.
    /// </summary>
    public ColorSpec? StyleTextColor { get; internal set; }

    /// <summary>The outline (border) drawn around the shape.</summary>
    public LineFormat Line { get; } = new();

    /// <summary>
    /// The visual effects applied to the shape — shadow, glow, reflection, soft edges, blur.
    /// Always present; check <see cref="EffectFormat.IsEmpty"/> for "no effects".
    /// </summary>
    public EffectFormat Effects { get; } = new();

    /// <summary>
    /// The 3-D format applied to the shape (bevels, extrusion, contour, material). Always
    /// present; check <see cref="Shape3DFormat.IsEmpty"/> for "no 3-D".
    /// </summary>
    public Shape3DFormat ThreeD { get; } = new();

    // ── Hyperlink ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The action performed when a user clicks the shape during a slide show.
    /// <see langword="null"/> means no click action is assigned.
    /// </summary>
    public HyperlinkAction? ClickAction { get; set; }

    // ── Placeholder ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The placeholder role of this shape, or <see cref="PlaceholderType.None"/> when the shape is
    /// not a placeholder. Placeholders inherit geometry and default formatting from the matching
    /// placeholder on the slide layout / master.
    /// </summary>
    public PlaceholderType PlaceholderType { get; set; } = PlaceholderType.None;

    /// <summary>
    /// The placeholder index (<c>p:ph/@idx</c>) used to match a slide placeholder to its layout
    /// definition when several placeholders share the same type. <see langword="null"/> when absent.
    /// </summary>
    public int? PlaceholderIndex { get; set; }

    /// <summary><see langword="true"/> when this shape carries a placeholder reference.</summary>
    public bool IsPlaceholder => PlaceholderType != PlaceholderType.None;

    // ── Round-trip preservation ───────────────────────────────────────────────

    /// <summary>
    /// Raw XML element preserved from the source file for properties that are not
    /// yet mapped to strongly-typed members (e.g. effects, 3-D format).
    /// Used internally to guarantee lossless round-trips.
    /// </summary>
    internal System.Xml.Linq.XElement? RawElement { get; set; }
}
