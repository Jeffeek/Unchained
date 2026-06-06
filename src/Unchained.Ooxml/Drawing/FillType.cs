namespace Unchained.Ooxml.Drawing;

/// <summary>Specifies how a shape's interior is filled.</summary>
public enum FillType
{
    /// <summary>The shape has no fill; the background shows through.</summary>
    None,

    /// <summary>The shape is filled with a single, uniform colour.</summary>
    Solid,

    /// <summary>The shape is filled with a smooth colour gradient.</summary>
    Gradient,

    /// <summary>The shape is filled with a repeating pattern of two colours.</summary>
    Pattern,

    /// <summary>The shape is filled with a raster image.</summary>
    Picture,

    /// <summary>The shape inherits its fill from the containing group shape.</summary>
    Group
}
