namespace Unchained.Ooxml.Charts;

/// <summary>
/// Position of the chart legend relative to the plot area.
/// Maps to the OOXML <c>c:legendPos @val</c> attribute values.
/// </summary>
public enum ChartLegendPosition
{
    /// <summary>Legend below the plot area. OOXML: <c>b</c>.</summary>
    Bottom,
    /// <summary>Legend above the plot area. OOXML: <c>t</c>.</summary>
    Top,
    /// <summary>Legend to the left of the plot area. OOXML: <c>l</c>.</summary>
    Left,
    /// <summary>Legend to the right of the plot area. OOXML: <c>r</c>.</summary>
    Right,
    /// <summary>Legend in the top-right corner. OOXML: <c>tr</c>.</summary>
    TopRight,
}
