namespace Unchained.Pdf.Models;

/// <summary>
///     Options for converting SVG content to a PDF page.
/// </summary>
/// <param name="PageWidthPt">
///     Target page width in points. When <paramref name="FitToPage" /> is <see langword="true" />
///     the SVG is scaled uniformly to fit; when <see langword="false" /> 1 SVG user unit = 1 pt.
///     Default 595 (ISO A4 width).
/// </param>
/// <param name="PageHeightPt">Target page height in points (default 842 — ISO A4 height).</param>
/// <param name="FitToPage">
///     When <see langword="true" /> (default) the SVG is scaled uniformly to fill the page while
///     preserving its aspect ratio. When <see langword="false" /> the SVG is placed at 1:1 scale
///     (1 user unit = 1 pt) anchored at the top-left margin.
/// </param>
/// <param name="MarginPt">Page margin in points applied when fitting (default 36 — 0.5 inch).</param>
/// <param name="Tagged">
///     When <see langword="true" />, the produced PDF wraps the SVG content in a <c>/Figure</c>
///     structure element with the <paramref name="AltText" /> as an <c>/Alt</c> entry so that
///     assistive technologies can describe the image.
/// </param>
/// <param name="Language">
///     BCP 47 language tag written to the document catalog's <c>/Lang</c> entry
///     (e.g. <c>"en-US"</c>). Required for PDF/UA conformance when
///     <paramref name="Tagged" /> is <see langword="true" />.
/// </param>
/// <param name="AltText">
///     Alternative text description of the SVG image, written to the <c>/Figure</c> structure
///     element's <c>/Alt</c> entry. Used by screen readers when <paramref name="Tagged" /> is
///     <see langword="true" />. Defaults to an empty string when not supplied.
/// </param>
public sealed record SvgLoadOptions(
    float PageWidthPt = 595f,
    float PageHeightPt = 842f,
    bool FitToPage = true,
    float MarginPt = 36f,
    bool Tagged = false,
    string? Language = null,
    string AltText = ""
)
{
    /// <summary>Default A4 settings with uniform fit.</summary>
    public static readonly SvgLoadOptions Default = new();
}
