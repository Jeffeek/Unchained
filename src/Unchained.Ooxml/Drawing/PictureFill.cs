using Unchained.Ooxml.Media;

namespace Unchained.Ooxml.Drawing;

/// <summary>
///     A fill that tiles or stretches a raster image across the shape area.
/// </summary>
public sealed class PictureFill
{
    /// <summary>The image used as the fill source.</summary>
    public EmbeddedImage? Image { get; set; }

    /// <summary>
    ///     Controls how the image is sized and positioned within the shape.
    /// </summary>
    public PictureStretchMode StretchMode { get; set; } = PictureStretchMode.Fill;

    /// <summary>
    ///     The OPC relationship ID (<c>r:embed</c>) pointing to the image part.
    ///     Used internally during the second-pass image resolution in <c>SlideParser</c>.
    /// </summary>
    internal string? RelationshipId { get; set; }
}
