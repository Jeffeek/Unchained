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

    /// <summary>All images currently embedded in the presentation.</summary>
    public IReadOnlyList<EmbeddedImage> Images => _images;

    /// <summary>All audio clips currently embedded in or linked from the presentation.</summary>
    public IReadOnlyList<EmbeddedAudio> AudioFiles => _audioFiles;

    /// <summary>All video clips currently embedded in or linked from the presentation.</summary>
    public IReadOnlyList<EmbeddedVideo> VideoFiles => _videoFiles;

    // ── Images ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds an image from raw bytes and returns the resulting <see cref="EmbeddedImage"/>.
    /// </summary>
    /// <param name="data">The raw image bytes.</param>
    /// <param name="contentType">MIME type (e.g. <c>"image/png"</c>).</param>
    public EmbeddedImage AddImage(ReadOnlyMemory<byte> data, string contentType)
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

    // ── Cleanup ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Removes media assets that are not referenced by any shape in the presentation
    /// and returns the number of items removed.
    /// </summary>
    /// <remarks>
    /// This method is a no-op in M1–M4 because reference tracking across the full
    /// shape graph is deferred. Call it after removing shapes or slides to reclaim space.
    /// </remarks>
    public int RemoveUnused() => 0; // Full implementation in a later milestone.
}
