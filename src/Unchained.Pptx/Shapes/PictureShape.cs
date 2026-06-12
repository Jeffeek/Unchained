using Unchained.Ooxml.Media;
using Unchained.Ooxml.Text;

namespace Unchained.Pptx.Shapes;

/// <summary>
///     A shape that displays a raster or vector image embedded in the presentation.
/// </summary>
public sealed class PictureShape : Shape
{
    /// <summary>
    ///     The image displayed by this shape.
    ///     <see langword="null" /> if the image could not be resolved during loading.
    /// </summary>
    public EmbeddedImage? Image { get; set; }

    /// <summary>
    ///     Optional caption text shown beneath or alongside the picture.
    ///     Present on picture-with-caption layout placeholders.
    /// </summary>
    public TextFrame? Caption { get; set; }
}
