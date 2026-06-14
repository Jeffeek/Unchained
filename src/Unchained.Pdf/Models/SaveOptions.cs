using System.Diagnostics.CodeAnalysis;

namespace Unchained.Pdf.Models;

/// <summary>
///     The PDF specification version to declare in the file header.
///     Higher versions enable features such as transparency (1.4+) and object streams (1.5+),
///     but may reduce compatibility with older readers.
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

/// <summary>Options applied when serializing a PDF document to a byte stream.</summary>
/// <param name="Version">
///     The PDF version string written into the file header (<c>%PDF-x.y</c>).
///     Defaults to <see cref="PdfVersion.Pdf17" />.
/// </param>
/// <param name="Linearize">
///     When <see langword="true" />, the output is linearized (web-optimized) so that PDF readers
///     can render the first page before the full file is downloaded.
/// </param>
/// <param name="OptimizeImages">
///     When <see langword="true" />, embedded images are re-compressed to reduce file size.
/// </param>
/// <param name="Encryption">
///     Password-protection settings. When non-<see langword="null" />, the saved PDF is encrypted
///     using AES-256 (V=5, R=6) by default. Use <see cref="EncryptionOptions" /> to configure
///     the passwords, algorithm, and permission flags.
/// </param>
/// <param name="Tagged">
///     When <see langword="true" />, the saved PDF includes a structure tree (<c>/StructTreeRoot</c>)
///     and marked-content operators so that assistive technologies can navigate the document.
///     Has no effect on documents not produced by Unchained's own converters — use
///     <c>TxtLoadOptions</c>, <c>MdLoadOptions</c>, or <c>SvgLoadOptions</c>
///     with <c>Tagged = true</c> to generate tagged content.
/// </param>
/// <param name="Language">
///     BCP 47 language tag written to the document catalog's <c>/Lang</c> entry
///     (e.g. <c>"en-US"</c>). Required for PDF/UA conformance when
///     <paramref name="Tagged" /> is <see langword="true" />.
/// </param>
/// <param name="OptimizeSize">
///     When <see langword="true" />, automatically runs stream compression and object
///     deduplication on the document before serializing. Produces a smaller file at
///     the cost of slightly more CPU time.
/// </param>
/// <param name="AllowReusePageContent">
///     When <see langword="true" />, identical content stream byte arrays are deduplicated
///     during serialization — multiple pages that share the same content will reference a
///     single shared content stream object instead of storing identical bytes multiple times.
/// </param>
public sealed record SaveOptions(
    PdfVersion Version = PdfVersion.Pdf17,
    bool Linearize = false,
    bool OptimizeImages = false,
    EncryptionOptions? Encryption = null,
    bool Tagged = false,
    string? Language = null,
    bool OptimizeSize = false,
    bool AllowReusePageContent = false
)
{
    /// <summary>Default options: PDF 1.7, no linearization, no image optimization, no encryption.</summary>
    public static readonly SaveOptions Default = new();

    /// <summary>Linearized output suitable for web delivery.</summary>
    public static readonly SaveOptions WebOptimized = new(Linearize: true);

    /// <summary>Smallest possible output: linearized + stream compression + deduplication.</summary>
    public static readonly SaveOptions Compact = new(
        Linearize: true,
        OptimizeSize: true,
        AllowReusePageContent: true
    );
}
