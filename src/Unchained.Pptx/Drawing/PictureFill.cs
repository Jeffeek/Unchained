using Unchained.Pptx.Media;
using Unchained.Pptx.Models.Drawing;

namespace Unchained.Pptx.Drawing;

/// <summary>
/// A fill that tiles or stretches a raster image across the shape area.
/// </summary>
public sealed class PictureFill
{
    /// <summary>The image used as the fill source.</summary>
    public EmbeddedImage? Image { get; set; }

    /// <summary>
    /// Controls how the image is sized and positioned within the shape.
    /// </summary>
    public PictureStretchMode StretchMode { get; set; } = PictureStretchMode.Fill;
}
