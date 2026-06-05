using Unchained.Pptx.Core;
using Unchained.Pptx.Models.Text;
using Unchained.Pptx.Themes;

namespace Unchained.Pptx.Text;

/// <summary>
/// Immutable line-spacing value that is either an absolute point measurement or a
/// percentage of the current font size.
/// </summary>
/// <param name="Value">
/// The numeric magnitude. When <see cref="Mode"/> is <see cref="LineSpacingMode.Points"/>,
/// this is a point measurement. When it is <see cref="LineSpacingMode.Percent"/>, this is
/// a percentage (e.g. 150.0 = 150%).
/// </param>
/// <param name="Mode">Whether <see cref="Value"/> represents points or a percentage.</param>
public readonly record struct LineSpacing(double Value, LineSpacingMode Mode)
{
    /// <summary>Creates a line spacing value expressed in typographic points.</summary>
    public static LineSpacing FromPoints(double points) => new(points, LineSpacingMode.Points);

    /// <summary>Creates a line spacing value expressed as a percentage of the font size.</summary>
    public static LineSpacing FromPercent(double percent) => new(percent, LineSpacingMode.Percent);

    /// <summary>Single-spaced (100%).</summary>
    public static readonly LineSpacing Single = FromPercent(100);

    /// <summary>One-and-a-half-spaced (150%).</summary>
    public static readonly LineSpacing OneAndAHalf = FromPercent(150);

    /// <summary>Double-spaced (200%).</summary>
    public static readonly LineSpacing Double = FromPercent(200);
}
