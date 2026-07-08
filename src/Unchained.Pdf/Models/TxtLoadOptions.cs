namespace Unchained.Pdf.Models;

/// <summary>
///     Options for converting plain-text content to a PDF document.
/// </summary>
/// <param name="FontName">Standard 14 font used for all text (default <c>Helvetica</c>).</param>
/// <param name="FontSize">Body font size in points (default 12).</param>
/// <param name="LineSpacing">Line height as a multiple of <paramref name="FontSize" /> (default 1.2).</param>
/// <param name="MarginPt">Uniform page margin in points — 72 pt = 1 inch (default 72).</param>
/// <param name="PageWidthPt">Page width in points (default 595 — ISO A4).</param>
/// <param name="PageHeightPt">Page height in points (default 842 — ISO A4).</param>
/// <param name="Tagged">
///     When <see langword="true" />, the produced PDF includes a tagged structure tree
///     (<c>/MarkInfo /Marked true</c>, <c>/StructTreeRoot</c>) so that assistive
///     technologies can navigate the document content.
/// </param>
/// <param name="Language">
///     BCP 47 language tag written to the document catalog's <c>/Lang</c> entry
///     (e.g. <c>"en-US"</c>). Required for PDF/UA conformance when
///     <paramref name="Tagged" /> is <see langword="true" />.
/// </param>
public sealed record TxtLoadOptions(
    string FontName = PdfConstants.FontHelvetica,
    float FontSize = 12f,
    float LineSpacing = 1.2f,
    float MarginPt = 72f,
    float PageWidthPt = 595f,
    float PageHeightPt = 842f,
    bool Tagged = false,
    string? Language = null
)
{
    /// <summary>Default A4 settings.</summary>
    public static readonly TxtLoadOptions Default = new();
}
