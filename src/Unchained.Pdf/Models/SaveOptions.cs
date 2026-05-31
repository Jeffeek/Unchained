using System.Diagnostics.CodeAnalysis;

namespace Unchained.Pdf.Models;

/// <summary>
/// The PDF specification version to declare in the file header.
/// Higher versions enable features such as transparency (1.4+) and object streams (1.5+),
/// but may reduce compatibility with older readers.
/// </summary>
[
    SuppressMessage("ReSharper", "UnusedMember.Global"),
    SuppressMessage("ReSharper", "InconsistentNaming")
]
public enum PdfVersion
{
    /// <summary>PDF 1.4 — adds transparency, patterns, and smooth shading.</summary>
    Pdf14,

    /// <summary>PDF 1.5 — adds object streams and cross-reference streams.</summary>
    Pdf15,

    /// <summary>PDF 1.7 — the base for ISO 32000-1; the default for new documents.</summary>
    Pdf17,

    /// <summary>PDF/A-1b — ISO 19005-1 archival subset at conformance level B.</summary>
    PdfA1b,

    /// <summary>PDF/A-2b — ISO 19005-2 archival subset at conformance level B; supports JPEG 2000.</summary>
    PdfA2b
}

/// <summary>
/// Options applied when serializing an <see cref="IPdfDocument"/> via
/// <see cref="IDocumentProcessor.SaveAsync(IPdfDocument,System.IO.Stream,SaveOptions?,System.Threading.CancellationToken)"/>.
/// </summary>
public sealed record SaveOptions(
    /// <summary>
    /// The PDF version string written into the file header (<c>%PDF-x.y</c>).
    /// Defaults to <see cref="PdfVersion.Pdf17"/>.
    /// </summary>
    PdfVersion Version = PdfVersion.Pdf17,

    /// <summary>
    /// When <see langword="true"/>, the output is linearized (web-optimized) so that
    /// PDF readers can render the first page before the full file is downloaded.
    /// Linearization increases write time and output size slightly.
    /// </summary>
    bool Linearize = false,

    /// <summary>
    /// When <see langword="true"/>, embedded images are re-compressed to reduce
    /// file size. May reduce image quality depending on the codec settings.
    /// </summary>
    bool OptimizeImages = false
)
{
    /// <summary>Default options: PDF 1.7, no linearization, no image optimization.</summary>
    public static readonly SaveOptions Default = new();

    /// <summary>Linearized output suitable for web delivery.</summary>
    public static readonly SaveOptions WebOptimized = new(Linearize: true);
}
