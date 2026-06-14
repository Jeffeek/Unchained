namespace Unchained.Pdf.Models;

/// <summary>
///     PDF/X conformance profile for print production (ISO 15930). PDF/X mandates an
///     <c>/OutputIntents</c> entry describing the target printing condition and a
///     <c>GTS_PDFXVersion</c> marker.
/// </summary>
public enum PdfXProfile
{
    /// <summary>PDF/X-1a:2001 (ISO 15930-1) — CMYK/spot only, all fonts embedded, blind exchange.</summary>
    PdfX1A2001,
    /// <summary>PDF/X-1a:2003 (ISO 15930-4).</summary>
    PdfX1A2003,
    /// <summary>PDF/X-3:2002 (ISO 15930-3) — allows colour-managed (ICC) workflows.</summary>
    PdfX32002,
    /// <summary>PDF/X-3:2003 (ISO 15930-6).</summary>
    PdfX32003,
    /// <summary>PDF/X-4 (ISO 15930-7) — allows transparency and optional content.</summary>
    PdfX4
}
