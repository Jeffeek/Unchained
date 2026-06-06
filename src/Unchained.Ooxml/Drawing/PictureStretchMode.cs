namespace Unchained.Ooxml.Drawing;

/// <summary>How an image is stretched or tiled inside a picture fill.</summary>
public enum PictureStretchMode
{
    /// <summary>The image is stretched to fill the entire shape bounding box.</summary>
    Fill,
    /// <summary>
    /// The image is tiled (repeated) across the shape area.
    /// The tile size and alignment are controlled by the fill's tile settings.
    /// </summary>
    Tile
}
