namespace Unchained.Pdf.Models;

/// <summary>
///     Options for converting Markdown content to a PDF document.
/// </summary>
/// <param name="BodyFontName">Standard 14 font for body text (default <c>Helvetica</c>).</param>
/// <param name="BodyFontSize">Body font size in points (default 11).</param>
/// <param name="CodeFontName">Monospace font for code blocks and inline code (default <c>Courier</c>).</param>
/// <param name="CodeFontSize">Code font size in points (default 10).</param>
/// <param name="LineSpacing">Line height as a multiple of <paramref name="BodyFontSize" /> (default 1.4).</param>
/// <param name="ParagraphSpacingPt">Extra vertical space between paragraphs in points (default 8).</param>
/// <param name="MarginPt">Uniform page margin in points (default 72 — 1 inch).</param>
/// <param name="PageWidthPt">Page width in points (default 595 — ISO A4).</param>
/// <param name="PageHeightPt">Page height in points (default 842 — ISO A4).</param>
/// <param name="Tagged">
///     When <see langword="true" />, the produced PDF includes a tagged structure tree
///     (<c>/MarkInfo /Marked true</c>, <c>/StructTreeRoot</c>) with semantic element types
///     (H1–H6, P, Code, L, LI, LBody) so that assistive technologies can navigate the document.
/// </param>
/// <param name="Language">
///     BCP 47 language tag written to the document catalog's <c>/Lang</c> entry
///     (e.g. <c>"en-US"</c>). Required for PDF/UA conformance when
///     <paramref name="Tagged" /> is <see langword="true" />.
/// </param>
public sealed record MdLoadOptions(
    string BodyFontName = PdfConstants.FontHelvetica,
    float BodyFontSize = 11f,
    string CodeFontName = "Courier",
    float CodeFontSize = 10f,
    float LineSpacing = 1.4f,
    float ParagraphSpacingPt = 8f,
    float MarginPt = 72f,
    float PageWidthPt = 595f,
    float PageHeightPt = 842f,
    bool Tagged = false,
    string? Language = null
)
{
    /// <summary>Default A4 settings.</summary>
    public static readonly MdLoadOptions Default = new();

    /// <summary>
    ///     Font size in points for a heading of the given <paramref name="level" /> (1–6),
    ///     scaled relative to <see cref="BodyFontSize" />: h1 is largest, h5 equals body size,
    ///     and any level outside 1–5 falls back to 0.9× body size.
    /// </summary>
    internal float HeadingFontSize(int level) => level switch
    {
        1 => BodyFontSize * 2.0f,
        2 => BodyFontSize * 1.6f,
        3 => BodyFontSize * 1.3f,
        4 => BodyFontSize * 1.1f,
        5 => BodyFontSize * 1.0f,
        _ => BodyFontSize * 0.9f
    };
}
