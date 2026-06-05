using Unchained.Pptx.Core;
using Unchained.Pptx.Shapes;

namespace Unchained.Pptx.Slides;

/// <summary>
/// A single content slide in a presentation.
/// </summary>
public sealed class Slide
{
    // ── Identity ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The display name of the slide (e.g. "Slide 1").
    /// Changing this name does not affect slide ordering.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The stable numeric identifier of the slide, unique within the presentation.
    /// This value is preserved from the source file and does not change when slides are reordered.
    /// </summary>
    public uint SlideId { get; internal set; }

    /// <summary>
    /// The one-based display number of the slide, reflecting its current position in the deck.
    /// This updates automatically when slides are reordered.
    /// </summary>
    public int SlideNumber { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when the slide is hidden and will be skipped during a slide show.
    /// </summary>
    public bool IsHidden { get; set; }

    // ── Content ───────────────────────────────────────────────────────────────

    /// <summary>All shapes on this slide.</summary>
    public ShapeCollection Shapes { get; } = new();

    /// <summary>The slide background.</summary>
    public SlideBackground Background { get; } = new();

    // ── Layout / Master ────────────────────────────────────────────────────────

    /// <summary>The layout that controls placeholder positions and default formatting.</summary>
    public SlideLayout Layout { get; set; } = null!;

    /// <summary>
    /// The master slide for this slide, resolved via <see cref="Layout"/>.
    /// </summary>
    public MasterSlide Master => Layout.Master;

    // ── Notes ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// The speaker notes for this slide. Created automatically when first accessed.
    /// </summary>
    public NotesSlide Notes => _notes ??= new NotesSlide();

    private NotesSlide? _notes;

    /// <summary><see langword="true"/> when notes have been created for this slide.</summary>
    internal bool HasNotes => _notes != null;

    // ── Round-trip blobs ──────────────────────────────────────────────────────

    /// <summary>Animation timing XML, preserved verbatim until M6 implementation.</summary>
    internal System.Xml.Linq.XElement? TimingElement { get; set; }

    /// <summary>Slide transition XML, preserved verbatim until M6 implementation.</summary>
    internal System.Xml.Linq.XElement? TransitionElement { get; set; }

    /// <summary>Colour map override element, preserved verbatim.</summary>
    internal System.Xml.Linq.XElement? ColorMapOverrideElement { get; set; }

    /// <summary>
    /// OPC part URI of this slide (e.g. <c>/ppt/slides/slide1.xml</c>).
    /// Used internally by the writer.
    /// </summary>
    internal string PartUri { get; set; } = string.Empty;

    /// <summary>Relationship ID of this slide within the presentation relationships.</summary>
    internal string RelationshipId { get; set; } = string.Empty;

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all visible text on this slide in reading order — the text content of
    /// every shape that has a text frame, concatenated with newlines.
    /// </summary>
    public string GetAllText() =>
        string.Join(
            "\n",
            Shapes
                .OfType<AutoShape>()
                .Select(static s => s.TextFrame.PlainText)
                .Where(static t => !string.IsNullOrEmpty(t)));

    /// <summary>
    /// Finds the first shape with the given name, or <see langword="null"/> if not found.
    /// </summary>
    public Shape? FindShapeByName(string name) =>
        Shapes.FirstOrDefault(s => s.Name.Equals(name, StringComparison.Ordinal));

    /// <summary>
    /// Finds the first shape whose <see cref="Shape.AltText"/> matches the given string,
    /// or <see langword="null"/> if not found.
    /// </summary>
    public Shape? FindShapeByAltText(string altText) =>
        Shapes.FirstOrDefault(s => s.AltText != null && s.AltText.Equals(altText, StringComparison.Ordinal));
}
