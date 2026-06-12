using Unchained.Ooxml.Media;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Media;

/// <summary>
/// The central store for all media assets (images, audio, and video) embedded in or
/// linked from a presentation. All shapes that reference media hold a reference to an
/// object in this store.
/// </summary>
public sealed class MediaStore
{
    private readonly List<EmbeddedImage> _images = [];
    private readonly List<EmbeddedAudio> _audioFiles = [];
    private readonly List<EmbeddedVideo> _videoFiles = [];
    private readonly List<EmbeddedFont> _fonts = [];

    /// <summary>All images currently embedded in the presentation.</summary>
    public IReadOnlyList<EmbeddedImage> Images => _images;

    /// <summary>All audio clips currently embedded in or linked from the presentation.</summary>
    public IReadOnlyList<EmbeddedAudio> AudioFiles => _audioFiles;

    /// <summary>All video clips currently embedded in or linked from the presentation.</summary>
    public IReadOnlyList<EmbeddedVideo> VideoFiles => _videoFiles;

    /// <summary>All fonts embedded in the presentation (from <c>&lt;p:embeddedFontLst&gt;</c>).</summary>
    public IReadOnlyList<EmbeddedFont> Fonts => _fonts;

    // ── Images ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds an image from raw bytes and returns the resulting <see cref="EmbeddedImage"/>.
    /// </summary>
    /// <param name="data">The raw image bytes.</param>
    /// <param name="contentType">MIME type (e.g. <c>"image/png"</c>).</param>
    internal EmbeddedImage AddImage(ReadOnlyMemory<byte> data, string contentType)
    {
        ArgumentNullException.ThrowIfNull(contentType);
        var image = new EmbeddedImage(contentType, data);
        _images.Add(image);
        return image;
    }

    /// <summary>Adds an already-constructed <see cref="EmbeddedImage"/> to the store.</summary>
    internal EmbeddedImage AddImage(EmbeddedImage image)
    {
        _images.Add(image);
        return image;
    }

    // ── Audio ────────────────────────────────────────────────────────────────

    /// <summary>Adds an audio clip to the store.</summary>
    internal EmbeddedAudio AddAudio(EmbeddedAudio audio)
    {
        _audioFiles.Add(audio);
        return audio;
    }

    // ── Video ─────────────────────────────────────────────────────────────────

    /// <summary>Adds a video clip to the store.</summary>
    internal EmbeddedVideo AddVideo(EmbeddedVideo video)
    {
        _videoFiles.Add(video);
        return video;
    }

    // ── Fonts ─────────────────────────────────────────────────────────────────

    /// <summary>Adds an embedded font to the store.</summary>
    internal EmbeddedFont AddFont(EmbeddedFont font)
    {
        _fonts.Add(font);
        return font;
    }

    /// <summary>
    /// Returns the embedded font bytes best matching <paramref name="typeface"/> and the
    /// requested style, or <see langword="null"/> when no embedded font matches. Falls back
    /// to the regular variant of the same typeface when the exact style is absent.
    /// </summary>
    internal ReadOnlyMemory<byte>? FindFontData(string typeface, EmbeddedFontStyle style)
    {
        if (string.IsNullOrEmpty(typeface) || _fonts.Count == 0)
            return null;

        EmbeddedFont? exact = null;
        EmbeddedFont? regular = null;
        EmbeddedFont? anyOfTypeface = null;

        foreach (var font in _fonts.Where(font => font.Typeface.Equals(typeface, StringComparison.OrdinalIgnoreCase)))
        {
            anyOfTypeface ??= font;
            if (font.Style == style)
                exact = font;
            if (font.Style == EmbeddedFontStyle.Regular)
                regular = font;
        }

        var chosen = exact ?? regular ?? anyOfTypeface;

        return chosen?.Data;
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Removes all media assets that are not referenced by any shape across
    /// all slides, masters, and layouts in the given <paramref name="slides"/> collection.
    /// Returns the total number of items removed.
    /// </summary>
    /// <param name="slides">
    /// The slide collection to scan for live references.
    /// Pass <see cref="Engine.PresentationDocument.Slides"/> to purge unreferenced media
    /// after removing shapes or slides.
    /// </param>
    internal int RemoveUnused(SlideCollection slides)
    {
        ArgumentNullException.ThrowIfNull(slides);

        // Collect all EmbeddedImage instances that are reachable from any shape.
        var usedImages = new HashSet<EmbeddedImage>(ReferenceEqualityComparer.Instance);
        foreach (var slide in slides)
            CollectImages(slide.Shapes, usedImages);

        // Remove images that are not reachable.
        var removedCount = _images.RemoveAll(img => !usedImages.Contains(img));

        // Audio and video are stored on AudioShape / VideoShape respectively.
        var usedAudio = new HashSet<EmbeddedAudio>(ReferenceEqualityComparer.Instance);
        var usedVideo = new HashSet<EmbeddedVideo>(ReferenceEqualityComparer.Instance);
        foreach (var slide in slides)
            CollectMedia(slide.Shapes, usedAudio, usedVideo);

        removedCount += _audioFiles.RemoveAll(a => !usedAudio.Contains(a));
        removedCount += _videoFiles.RemoveAll(v => !usedVideo.Contains(v));

        return removedCount;
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private static void CollectImages(ShapeCollection shapes, ISet<EmbeddedImage> used)
    {
        foreach (var shape in shapes)
        {
            switch (shape)
            {
                case PictureShape { Image: not null } pic:
                    used.Add(pic.Image);
                break;
                case GroupShape grp:
                    CollectImages(grp.Children, used);
                break;
            }
        }
    }

    private static void CollectMedia(ShapeCollection shapes, ISet<EmbeddedAudio> usedAudio, ISet<EmbeddedVideo> usedVideo)
    {
        foreach (var shape in shapes)
        {
            switch (shape)
            {
                case AudioShape { Audio: not null } audio:
                    usedAudio.Add(audio.Audio);
                break;
                case VideoShape { Video: not null } video:
                    usedVideo.Add(video.Video);
                break;
                case GroupShape grp:
                    CollectMedia(grp.Children, usedAudio, usedVideo);
                break;
            }
        }
    }
}
