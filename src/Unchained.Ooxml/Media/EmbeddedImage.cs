namespace Unchained.Ooxml.Media;

/// <summary>
///     An image embedded in the presentation package, referenced by one or more picture shapes.
/// </summary>
public sealed class EmbeddedImage
{
    /// <summary>Initialises an embedded image with the given content type and raw byte data.</summary>
    /// <param name="contentType">MIME type (e.g. <c>"image/png"</c>, <c>"image/jpeg"</c>).</param>
    /// <param name="data">The raw image bytes.</param>
    public EmbeddedImage(string contentType, ReadOnlyMemory<byte> data)
    {
        ArgumentNullException.ThrowIfNull(contentType);
        ContentType = contentType;
        Data = data;
    }

    /// <summary>The MIME content type (e.g. <c>"image/png"</c>, <c>"image/jpeg"</c>).</summary>
    public string ContentType { get; }

    /// <summary>The raw image bytes.</summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>
    ///     The pixel width of the image, or 0 if not yet determined.
    /// </summary>
    public int PixelWidth { get; init; }

    /// <summary>
    ///     The pixel height of the image, or 0 if not yet determined.
    /// </summary>
    public int PixelHeight { get; init; }

    /// <summary>
    ///     The relationship ID of this image within its part.
    ///     This is set by the parser and used by the writer; consumers can ignore it.
    /// </summary>
    internal string RelationshipId { get; set; } = string.Empty;

    /// <summary>The OPC part URI for this image (e.g. <c>/ppt/media/image1.png</c>).</summary>
    internal string PartUri { get; set; } = string.Empty;
}
