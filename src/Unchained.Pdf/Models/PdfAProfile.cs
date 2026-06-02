namespace Unchained.Pdf.Models;

/// <summary>
/// PDF/A conformance profiles supported for validation and conversion.
/// </summary>
public enum PdfAProfile
{
    /// <summary>ISO 19005-1, Level B (basic). Most widely deployed archival standard.</summary>
    PdfA1B,

    /// <summary>ISO 19005-1, Level A (accessible). Adds tagging and logical structure requirements.</summary>
    PdfA1A,

    /// <summary>ISO 19005-2, Level B. Allows JPEG 2000, transparency, and optional content.</summary>
    PdfA2B,

    /// <summary>ISO 19005-2, Level U. Adds Unicode text requirements.</summary>
    PdfA2U,

    /// <summary>ISO 19005-3, Level B. Allows embedded file attachments.</summary>
    PdfA3B
}
