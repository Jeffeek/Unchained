namespace Unchained.Pptx.Models.Shapes;

/// <summary>Specifies how the corners of a line are joined.</summary>
public enum LineJoinStyle
{
    /// <summary>Corners are joined with a sharp miter (the default for most shapes).</summary>
    Miter,
    /// <summary>Corners are joined with a bevelled (flat-cut) edge.</summary>
    Bevel,
    /// <summary>Corners are joined with a rounded curve.</summary>
    Round
}
