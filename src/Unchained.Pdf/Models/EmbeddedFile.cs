namespace Unchained.Pdf.Models;

/// <summary>
/// Represents a file attachment embedded inside a PDF document
/// via the <c>/Names /EmbeddedFiles</c> name tree (ISO 32000-1 §7.11.4).
/// </summary>
/// <param name="Name">
/// The unique name used as the key in the <c>/EmbeddedFiles</c> name tree.
/// This is the identifier used to reference the file from within the PDF.
/// </param>
/// <param name="FileName">
/// The display file name shown in PDF readers (e.g. <c>"report.xlsx"</c>).
/// </param>
/// <param name="Description">
/// Optional human-readable description of the file's contents.
/// </param>
/// <param name="MimeType">
/// MIME type of the embedded file (e.g. <c>"application/vnd.ms-excel"</c>).
/// Pass <see langword="null"/> to omit the <c>/Subtype</c> entry.
/// </param>
/// <param name="Data">Raw bytes of the embedded file.</param>
public sealed record EmbeddedFile(
    string Name,
    string FileName,
    string? Description,
    string? MimeType,
    byte[] Data
);
