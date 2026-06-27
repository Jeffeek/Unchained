using Unchained.Ooxml.Media;

namespace Unchained.Xlsx.Drawings;

/// <summary>A picture (embedded image) anchored on a worksheet's drawing layer.</summary>
public sealed class PictureDrawing : WorksheetDrawing
{
    /// <summary>Initialises a picture from an embedded image.</summary>
    public PictureDrawing(EmbeddedImage image) => Image = image;

    /// <summary>The embedded image this picture displays.</summary>
    public EmbeddedImage Image { get; set; }

    /// <summary>The OPC part URI of the backing image in <c>xl/media/</c> (assigned on write).</summary>
    internal string MediaPartUri { get; set; } = string.Empty;
}
