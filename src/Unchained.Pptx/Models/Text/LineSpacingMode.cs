namespace Unchained.Pptx.Models.Text;

/// <summary>
/// Specifies whether a line-spacing value is expressed in points or as a percentage
/// of the font size.
/// </summary>
public enum LineSpacingMode
{
    /// <summary>The spacing value is an absolute measurement in points.</summary>
    Points,
    /// <summary>The spacing value is a percentage of the current font size (e.g. 150 = 150%).</summary>
    Percent
}
