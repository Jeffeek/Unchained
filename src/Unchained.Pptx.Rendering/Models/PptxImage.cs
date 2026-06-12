namespace Unchained.Pptx.Rendering.Models;

/// <summary>
///     A rasterized slide image produced by <see cref="Engine.SlideRenderer" />.
/// </summary>
public sealed class PptxImage(
    int widthPx,
    int heightPx,
    RenderImageFormat format,
    ReadOnlyMemory<byte> data
)
{
    /// <summary>The width of the image in pixels.</summary>
    public int WidthPx { get; } = widthPx;

    /// <summary>The height of the image in pixels.</summary>
    public int HeightPx { get; } = heightPx;

    /// <summary>The encoding format of the image bytes in <see cref="Data" />.</summary>
    public RenderImageFormat Format { get; } = format;

    /// <summary>The raw encoded image bytes.</summary>
    public ReadOnlyMemory<byte> Data { get; } = data;

    /// <summary>
    ///     Writes the image bytes to a file at the given path, creating or overwriting it.
    /// </summary>
    /// <param name="path">The destination file path.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SaveAsync(string path, CancellationToken ct = default) =>
        await File.WriteAllBytesAsync(path, Data.ToArray(), ct).ConfigureAwait(false);
}
