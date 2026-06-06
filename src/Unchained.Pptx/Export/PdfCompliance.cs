namespace Unchained.Pptx.Export;

/// <summary>
/// The PDF/A or PDF conformance level for PPTX-to-PDF export.
/// </summary>
public enum PdfCompliance
{
    /// <summary>Standard PDF 1.5 — broadest viewer compatibility.</summary>
    Pdf15,
    /// <summary>Standard PDF 1.6.</summary>
    Pdf16,
    /// <summary>Standard PDF 1.7 (ISO 32000-1). Default.</summary>
    Pdf17,
    /// <summary>PDF/A-1b — long-term archiving; no encryption or transparency.</summary>
    PdfA1b,
    /// <summary>PDF/A-2b — extends A-1b with transparency and JPEG 2000 support.</summary>
    PdfA2b,
    /// <summary>PDF/A-3b — same as A-2b but permits embedded files of any type.</summary>
    PdfA3b,
}
