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

    internal PresentationDocument(
        SlideCollection slides,
        MasterSlideCollection masters,
        MediaStore mediaStore,
        DocumentProperties properties,
        ProtectionInfo protection,
        SlideSize slideSize,
        CommentAuthorCollection? commentAuthors = null,
        SectionCollection? sections = null)
    {
        Slides = slides;
        Masters = masters;
        Media = mediaStore;
        Properties = properties;
        Protection = protection;
        SlideSize = slideSize;
        CommentAuthors = commentAuthors ?? new CommentAuthorCollection();
        Sections = sections ?? new SectionCollection();
    }

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

    // ── IDisposable / IAsyncDisposable ────────────────────────────────────────

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
