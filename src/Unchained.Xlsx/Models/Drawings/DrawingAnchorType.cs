namespace Unchained.Xlsx.Models.Drawings;

/// <summary>How a drawing (image or chart) is anchored to the worksheet grid.</summary>
public enum DrawingAnchorType
{
    /// <summary>Anchored between two cells; the drawing moves and resizes with the cells.</summary>
    TwoCell,

    /// <summary>Anchored to one cell with a fixed size; moves but does not resize with cells.</summary>
    OneCell,

    /// <summary>Anchored to an absolute position and size, independent of cells.</summary>
    Absolute
}
