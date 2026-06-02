namespace Unchained.Pdf.Models;

/// <summary>
/// Options for converting SVG content to a PDF page.
/// </summary>
/// <param name="PageWidthPt">
/// Target page width in points. When <paramref name="FitToPage"/> is <see langword="true"/>
/// the SVG is scaled uniformly to fit; when <see langword="false"/> 1 SVG user unit = 1 pt.
/// Default 595 (ISO A4 width).
/// </param>
/// <param name="PageHeightPt">Target page height in points (default 842 — ISO A4 height).</param>
/// <param name="FitToPage">
/// When <see langword="true"/> (default) the SVG is scaled uniformly to fill the page while
/// preserving its aspect ratio. When <see langword="false"/> the SVG is placed at 1:1 scale
/// (1 user unit = 1 pt) anchored at the top-left margin.
/// </param>
/// <param name="MarginPt">Page margin in points applied when fitting (default 36 — 0.5 inch).</param>
public sealed record SvgLoadOptions(
    float PageWidthPt = 595f,
    float PageHeightPt = 842f,
    bool FitToPage = true,
    float MarginPt = 36f
)
{
    /// <summary>Default A4 settings with uniform fit.</summary>
    public static readonly SvgLoadOptions Default = new();
}
