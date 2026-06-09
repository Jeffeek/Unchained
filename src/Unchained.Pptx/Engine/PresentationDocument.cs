using Unchained.Pptx.Core;
using Unchained.Ooxml;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Media;
using Unchained.Ooxml.Media;
using Unchained.Pptx.Models;
using Unchained.Pptx.Security;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Engine;

/// <summary>
/// The public entry point for a loaded or newly-created presentation.
/// Wraps the parsed internal model and exposes the Unchained public API surface.
/// </summary>
public sealed class PresentationDocument : IDisposable, IAsyncDisposable
{
    private bool _disposed;

    // The source OpenXML-SDK engine when loaded via the SDK path; null for the custom path or
    // CreateBlank. Held open so an in-place SDK-backed save can mutate the original package
    // (unmodelled parts pass through). Disposed with the document.
    private readonly Ooxml.Engine.OoxmlEngine? _engine;

    internal PresentationDocument(
        SlideCollection slides,
        MasterSlideCollection masters,
        MediaStore mediaStore,
        DocumentProperties properties,
        ProtectionInfo protection,
        SlideSize slideSize,
        CommentAuthorCollection? commentAuthors = null,
        SectionCollection? sections = null,
        Ooxml.Engine.OoxmlEngine? engine = null)
    {
        Slides = slides;
        Masters = masters;
        Media = mediaStore;
        Properties = properties;
        Protection = protection;
        SlideSize = slideSize;
        CommentAuthors = commentAuthors ?? new CommentAuthorCollection();
        Sections = sections ?? new SectionCollection();
        _engine = engine;
    }

    /// <summary>
    /// The source OpenXML-SDK engine when this document was loaded through the SDK path;
    /// <see langword="null"/> otherwise. Internal — used by the SDK-backed save path.
    /// </summary>
    internal Ooxml.Engine.OoxmlEngine? Engine => _engine;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// The ordered collection of content slides in this presentation.
    /// Add, remove, and reorder slides through this collection.
    /// </summary>
    public SlideCollection Slides { get; }

    /// <summary>The slide masters (templates) used by this presentation.</summary>
    public MasterSlideCollection Masters { get; }

    /// <summary>
    /// The central store for all embedded images, audio, and video assets.
    /// Shapes reference objects in this store; call <see cref="MediaStore.RemoveUnused"/>
    /// after removing shapes to reclaim space.
    /// </summary>
    public MediaStore Media { get; }

    /// <summary>Document metadata (title, author, keywords, dates, etc.).</summary>
    public DocumentProperties Properties { get; }

    /// <summary>
    /// The registry of all comment authors in this presentation.
    /// Add authors here before adding comments to slides.
    /// </summary>
    public CommentAuthorCollection CommentAuthors { get; }

    /// <summary>
    /// Named sections that group consecutive slides into labelled regions
    /// visible in PowerPoint's slide panel (PowerPoint 2010+ feature).
    /// </summary>
    public SectionCollection Sections { get; }

    /// <summary>
    /// Presentation-level slide-show settings (loop, show type, narration/animation, range, pen
    /// colour). Persisted in the <c>presProps.xml</c> part on save.
    /// </summary>
    public SlideShowSettings SlideShow { get; internal set; } = new();

    /// <summary>
    /// Verbatim-preserved content (VBA macro project, digital signatures) captured at load and
    /// re-emitted unchanged on save by the custom writer. <see langword="null"/> for documents
    /// created via <c>CreateBlank</c>. Internal — round-trip plumbing.
    /// </summary>
    internal PreservedContent? Preserved { get; set; }

    /// <summary>
    /// <see langword="true"/> when this presentation carries a VBA macro project (i.e. it is a
    /// <c>.pptm</c>). Macros are preserved across a round-trip but cannot be created or edited.
    /// </summary>
    public bool HasMacros => Preserved?.HasMacros == true;

    /// <summary>
    /// Synchronises the live statistics on <see cref="Properties"/> from the current
    /// in-memory state. Called automatically before each save.
    /// </summary>
    internal void SyncStatistics()
    {
        Properties.SlideCount = Slides.Count;
        Properties.HiddenSlideCount = Slides.Count(static s => s.IsHidden);
        Properties.NoteCount = Slides.Count(static s => s.HasNotes);
    }

    /// <summary>Encryption and write-protection state of the presentation.</summary>
    public ProtectionInfo Protection { get; }

    /// <summary>
    /// The physical dimensions of all slides in this presentation.
    /// Changing this value updates the dimensions stored in <c>presentation.xml</c>
    /// on the next save; it does not reflow existing shapes.
    /// </summary>
    public SlideSize SlideSize { get; set; }

    // ── Find & replace ────────────────────────────────────────────────────────

    /// <summary>
    /// Replaces every occurrence of <paramref name="oldText"/> with <paramref name="newText"/>
    /// across all slides — shapes, grouped shapes, and table cells — preserving run formatting.
    /// When <paramref name="includeNotes"/> is <see langword="true"/>, each slide's notes text is
    /// searched as well. Matches do not span paragraph boundaries.
    /// </summary>
    /// <returns>The total number of occurrences replaced across the presentation.</returns>
    public int ReplaceText(
        string oldText,
        string newText,
        StringComparison comparison = StringComparison.Ordinal,
        bool includeNotes = false)
    {
        var count = 0;
        foreach (var slide in Slides)
            count += slide.ReplaceText(oldText, newText, comparison, includeNotes);
        return count;
    }

    // ── IDisposable / IAsyncDisposable ────────────────────────────────────────

    /// <summary>
    /// Enumerates every shape click-hyperlink across all slides (recursing into groups),
    /// each paired with its owning slide and shape so links can be inspected, retargeted, or
    /// removed in place. This is the hyperlink-management entry point.
    /// </summary>
    public IEnumerable<HyperlinkReference> GetHyperlinks()
    {
        foreach (var slide in Slides)
            foreach (var link in slide.GetHyperlinks())
                yield return link;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _engine?.Dispose();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
