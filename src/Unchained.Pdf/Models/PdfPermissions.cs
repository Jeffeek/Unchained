namespace Unchained.Pdf.Models;

/// <summary>
/// Controls which operations are permitted when a PDF is opened with the user password.
/// Owner-password access always grants full permissions.
/// Corresponds to the /P bit-field in the PDF Standard Security Handler (ISO 32000-1 §7.6.3.2).
/// </summary>
[Flags]
public enum PdfPermissions
{
    /// <summary>No operations permitted.</summary>
    None = 0,

    /// <summary>Permit printing (low-quality / draft).</summary>
    Print = 1 << 2,

    /// <summary>Permit modifying the document (other than annotations and forms).</summary>
    Modify = 1 << 3,

    /// <summary>Permit copying or extracting text and graphics.</summary>
    Copy = 1 << 4,

    /// <summary>Permit adding or modifying annotations and filling form fields.</summary>
    AddAnnotations = 1 << 5,

    /// <summary>Permit filling in form fields (PDF 1.4+).</summary>
    FillForms = 1 << 8,

    /// <summary>Permit text access for screen-reader accessibility (PDF 1.4+).</summary>
    Accessibility = 1 << 9,

    /// <summary>Permit assembling the document (inserting, rotating, deleting pages; PDF 1.4+).</summary>
    Assemble = 1 << 10,

    /// <summary>Permit high-quality (faithful) printing (PDF 1.4+).</summary>
    PrintHighQuality = 1 << 11,

    /// <summary>All operations permitted.</summary>
    All = Print | Modify | Copy | AddAnnotations | FillForms | Accessibility | Assemble | PrintHighQuality
}
