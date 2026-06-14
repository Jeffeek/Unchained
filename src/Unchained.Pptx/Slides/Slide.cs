using System.Xml.Linq;
using Unchained.Pptx.Animations;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Shapes;

namespace Unchained.Pptx.Slides;

/// <summary>
///     A single content slide in a presentation.
/// </summary>
public sealed class Slide
{
    // ── Comments ──────────────────────────────────────────────────────────────

    private readonly List<Comment> _comments = [];

    private NotesSlide? _notes;
    // ── Identity ─────────────────────────────────────────────────────────────

    /// <summary>
    ///     The display name of the slide (e.g. "Slide 1").
    ///     Changing this name does not affect slide ordering.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     The stable numeric identifier of the slide, unique within the presentation.
    ///     This value is preserved from the source file and does not change when slides are reordered.
    /// </summary>
    public uint SlideId { get; internal set; }

    /// <summary>
    ///     The one-based display number of the slide, reflecting its current position in the deck.
    ///     This updates automatically when slides are reordered.
    /// </summary>
    public int SlideNumber { get; internal set; }

    /// <summary>
    ///     <see langword="true" /> when the slide is hidden and will be skipped during a slide show.
    /// </summary>
    public bool IsHidden { get; set; }

    // ── Content ───────────────────────────────────────────────────────────────

    /// <summary>All shapes on this slide.</summary>
    public ShapeCollection Shapes { get; } = new();

    /// <summary>The slide background.</summary>
    public SlideBackground Background { get; } = new();

    // ── Animations & Transitions ───────────────────────────────────────────────

    /// <summary>
    ///     The animation effects applied to shapes on this slide.
    ///     Modify the <see cref="AnimationTimeline.MainSequence" /> to add or remove effects.
    /// </summary>
    public AnimationTimeline Animations { get; } = new();

    /// <summary>
    ///     The visual transition that plays when advancing to this slide.
    ///     Set <see cref="SlideTransition.Effect" /> to configure the transition type.
    /// </summary>
    public SlideTransition Transition { get; } = new();

    // ── Layout / Master ────────────────────────────────────────────────────────

    /// <summary>The layout that controls placeholder positions and default formatting.</summary>
    public SlideLayout Layout { get; set; } = null!;

    /// <summary>
    ///     The master slide for this slide, resolved via <see cref="Layout" />.
    /// </summary>
    public MasterSlide Master => Layout.Master;

    // ── Notes ─────────────────────────────────────────────────────────────────

    /// <summary>
    ///     The speaker notes for this slide. Created automatically when first accessed.
    /// </summary>
    public NotesSlide Notes => _notes ??= new NotesSlide();

    /// <summary><see langword="true" /> when notes have been created for this slide.</summary>
    internal bool HasNotes => _notes != null;

    /// <summary><see langword="true" /> when this slide has at least one comment.</summary>
    internal bool HasComments => _comments.Count > 0;

    // ── Round-trip blobs ──────────────────────────────────────────────────────

    /// <summary>Colour map override element, preserved verbatim.</summary>
    internal XElement? ColorMapOverrideElement { get; set; }

    /// <summary>
    ///     OPC part URI of this slide (e.g. <c>/ppt/slides/slide1.xml</c>).
    ///     Used internally by the writer.
    /// </summary>
    internal string PartUri { get; set; } = string.Empty;

    /// <summary>Relationship ID of this slide within the presentation relationships.</summary>
    internal string RelationshipId { get; set; } = string.Empty;

    /// <summary>Returns all comments on this slide.</summary>
    public IReadOnlyList<Comment> GetComments() => _comments;

    /// <summary>
    ///     Adds a new comment to this slide and returns it.
    /// </summary>
    /// <param name="text">The comment body text.</param>
    /// <param name="position">The anchor position on the slide.</param>
    /// <param name="author">
    ///     The author. When <see langword="null" />, a default author named
    ///     <c>"Unknown"</c> is used if no authors exist yet (managed by the caller).
    /// </param>
    /// <param name="createdAt">Timestamp. Defaults to <see cref="DateTimeOffset.UtcNow" />.</param>
    public Comment AddComment(
        string text,
        SlidePosition position,
        CommentAuthor? author,
        DateTimeOffset? createdAt = null
    )
    {
        ArgumentNullException.ThrowIfNull(author);
        var index = ++author.LastIndex;
        var comment = new Comment(author, text, position, createdAt ?? DateTimeOffset.UtcNow, index);
        _comments.Add(comment);
        return comment;
    }

    /// <summary>Removes the given comment from this slide.</summary>
    /// <exception cref="ArgumentException">Thrown when the comment is not on this slide.</exception>
    public void RemoveComment(Comment comment)
    {
        if (!_comments.Remove(comment))
            throw new ArgumentException("The comment does not belong to this slide.", nameof(comment));
    }

    /// <summary>Adds a pre-parsed comment (used by the parser).</summary>
    internal void AddParsedComment(Comment comment) => _comments.Add(comment);

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    ///     Returns all visible text on this slide in reading order — the text content of
    ///     every shape that has a text frame, concatenated with newlines.
    /// </summary>
    public string GetAllText() =>
        string.Join(
            "\n",
            Shapes
                .OfType<AutoShape>()
                .Select(static s => s.TextFrame.PlainText)
                .Where(static t => !string.IsNullOrEmpty(t))
        );

    /// <summary>
    ///     Finds the first shape with the given name, or <see langword="null" /> if not found.
    /// </summary>
    public Shape? FindShapeByName(string name) =>
        Shapes.FirstOrDefault(s => s.Name.Equals(name, StringComparison.Ordinal));

    /// <summary>
    ///     Finds the first shape whose <see cref="Shape.AltText" /> matches the given string,
    ///     or <see langword="null" /> if not found.
    /// </summary>
    public Shape? FindShapeByAltText(string altText) =>
        Shapes.FirstOrDefault(s => s.AltText != null && s.AltText.Equals(altText, StringComparison.Ordinal));

    /// <summary>
    ///     Replaces every occurrence of <paramref name="oldText" /> with <paramref name="newText" />
    ///     in all text on this slide — shapes, grouped shapes, and table cells — preserving run
    ///     formatting. When <paramref name="includeNotes" /> is <see langword="true" />, the slide's
    ///     notes text is searched as well. Matches do not span paragraph boundaries.
    /// </summary>
    /// <returns>The total number of occurrences replaced.</returns>
    public int ReplaceText(
        string oldText,
        string newText,
        StringComparison comparison = StringComparison.Ordinal,
        bool includeNotes = false
    )
    {
        var count = ShapeTextWalker.EnumerateTextFrames(Shapes).Sum(frame => frame.ReplaceText(oldText, newText, comparison));

        if (includeNotes && Notes.NotesTextFrame is { } notesFrame)
            count += notesFrame.ReplaceText(oldText, newText, comparison);

        return count;
    }

    /// <summary>
    ///     Enumerates every shape click-hyperlink on this slide (recursing into group shapes),
    ///     each paired with its owning shape so links can be inspected, retargeted, or removed.
    /// </summary>
    public IEnumerable<HyperlinkReference> GetHyperlinks()
    {
        foreach (var shape in EnumerateShapes(Shapes))
        {
            if (shape.ClickAction is { } action)
                yield return new HyperlinkReference(this, shape, action);
        }

        yield break;

        static IEnumerable<Shape> EnumerateShapes(IEnumerable<Shape> shapes)
        {
            foreach (var shape in shapes)
            {
                yield return shape;

                if (shape is not GroupShape group) continue;

                foreach (var child in EnumerateShapes(group.Children))
                    yield return child;
            }
        }
    }
}
