using Unchained.Pdf.Models;

namespace Unchained.Pdf.Abstractions;

/// <summary>
/// Provides async loading and saving of PDF documents.
/// The default implementation is <see cref="Unchained.Pdf.Engine.DocumentProcessor"/>.
/// <para>
/// Implementations must be thread-safe: multiple callers may invoke
/// <see cref="LoadAsync(string,CancellationToken)"/> concurrently.
/// Concurrency is bounded internally to avoid over-subscribing the thread-pool.
/// </para>
/// </summary>
public interface IDocumentProcessor : IDisposable
{
    /// <summary>
    /// Reads and parses the PDF file at <paramref name="filePath"/>.
    /// The returned document is caller-owned; dispose it when done.
    /// </summary>
    /// <param name="filePath">Absolute or relative path to the PDF file.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    /// <returns>The loaded <see cref="IPdfDocument"/>.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="Unchained.Pdf.Core.PdfException">Thrown when the file is not a valid PDF.</exception>
    Task<IPdfDocument> LoadAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Opens a password-protected PDF at <paramref name="filePath"/> using <paramref name="password"/>.
    /// </summary>
    /// <param name="filePath">Path to the encrypted PDF file.</param>
    /// <param name="password">User or owner password. Pass an empty string for no-password encrypted PDFs.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    /// <exception cref="Core.PdfEncryptedException">Thrown when the password is incorrect.</exception>
    Task<IPdfDocument> LoadAsync(string filePath, string password, CancellationToken ct = default);

    /// <summary>
    /// Opens a password-protected PDF from <paramref name="stream"/> using <paramref name="password"/>.
    /// </summary>
    /// <param name="stream">A readable stream containing encrypted PDF data.</param>
    /// <param name="password">User or owner password.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    /// <exception cref="Core.PdfEncryptedException">Thrown when the password is incorrect.</exception>
    Task<IPdfDocument> LoadAsync(Stream stream, string password, CancellationToken ct = default);

    /// <summary>
    /// Reads and parses PDF content from <paramref name="stream"/>.
    /// The stream does not need to be seekable; it is copied to an internal buffer first.
    /// The returned document is caller-owned; dispose it when done.
    /// </summary>
    /// <param name="stream">A readable stream containing PDF data.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    /// <returns>The loaded <see cref="IPdfDocument"/>.</returns>
    /// <exception cref="Unchained.Pdf.Core.PdfException">Thrown when the stream content is not a valid PDF.</exception>
    Task<IPdfDocument> LoadAsync(Stream stream, CancellationToken ct = default);

    /// <summary>
    /// Converts plain text to a new PDF document, with automatic word-wrap and pagination.
    /// </summary>
    /// <param name="text">The source text content.</param>
    /// <param name="options">Layout options, or <see langword="null"/> to use <see cref="TxtLoadOptions.Default"/>.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task<IPdfDocument> LoadFromTxtAsync(string text, TxtLoadOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Converts Markdown text to a new PDF document.
    /// Supports headings, bold, italic, code blocks, lists, and thematic breaks.
    /// </summary>
    /// <param name="markdown">The source Markdown content.</param>
    /// <param name="options">Layout options, or <see langword="null"/> to use <see cref="MdLoadOptions.Default"/>.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task<IPdfDocument> LoadFromMarkdownAsync(string markdown, MdLoadOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Converts an SVG document to a single-page PDF.
    /// Supports common SVG shapes, paths, text, and group transforms.
    /// </summary>
    /// <param name="svgXml">The SVG source as an XML string.</param>
    /// <param name="options">Fit and page options, or <see langword="null"/> to use <see cref="SvgLoadOptions.Default"/>.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task<IPdfDocument> LoadFromSvgAsync(string svgXml, SvgLoadOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Validates <paramref name="pdfBytes"/> against the specified PDF/A conformance profile.
    /// Returns a <see cref="PdfAValidationResult"/> listing every violation found.
    /// An empty violation list means the document is fully conformant.
    /// </summary>
    /// <param name="pdfBytes">Raw bytes of the PDF to validate.</param>
    /// <param name="profile">PDF/A profile to check against (default PDF/A-1b).</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task<PdfAValidationResult> ValidatePdfAAsync(
        byte[] pdfBytes,
        PdfAProfile profile = PdfAProfile.PdfA1B,
        CancellationToken ct = default
    );

    /// <summary>
    /// Validates <paramref name="pdfBytes"/> against ISO 14289-1 (PDF/UA-1) accessibility requirements.
    /// Returns a <see cref="PdfUAValidationResult"/> listing every violation found.
    /// <para>
    /// Rules checked correspond to ISO 14289-1 clause numbers and cover: tagged PDF marker,
    /// document title, language, structure tree root, role map, alternate descriptions for figures,
    /// heading level sequence, table and list structure, untagged content detection, annotation
    /// accessible names, action restrictions, and XMP pdfuaid metadata.
    /// </para>
    /// <para>
    /// An empty violation list means the document passes all statically-verifiable PDF/UA-1 rules.
    /// Rules requiring human judgment (meaningful alt text content, logical reading order) are
    /// flagged as warnings where detectable.
    /// </para>
    /// </summary>
    /// <param name="pdfBytes">Raw bytes of the PDF to validate.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task<PdfUAValidationResult> ValidatePdfUAAsync(
        byte[] pdfBytes,
        CancellationToken ct = default
    );

    /// <summary>
    /// Attempts to load a PDF from <paramref name="bytes"/>, falling back to a
    /// byte-scanning recovery pass if the normal parse fails due to a corrupted
    /// or missing cross-reference table.
    /// </summary>
    /// <param name="bytes">Raw bytes of the PDF to repair.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task<IPdfDocument> RepairAsync(byte[] bytes, CancellationToken ct = default);

    /// <summary>
    /// Applies structural PDF/A conformance fixes to <paramref name="document"/> and
    /// writes the result to <paramref name="outputStream"/>.
    /// <para>
    /// Fixes applied: pdfaid XMP metadata, /ID in trailer, removal of /AA from catalog,
    /// Print flag on annotations. Does not embed fonts, add output intents, or remove
    /// transparency — validate after conversion to see any remaining violations.
    /// </para>
    /// </summary>
    /// <param name="document">Source document (must not be encrypted).</param>
    /// <param name="outputStream">Destination stream for the converted PDF.</param>
    /// <param name="profile">Target PDF/A profile (default PDF/A-1b).</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task ConvertToPdfAAsync(
        IPdfDocument document,
        Stream outputStream,
        PdfAProfile profile = PdfAProfile.PdfA1B,
        CancellationToken ct = default
    );

    /// <summary>
    /// Applies structural PDF/A fixes and writes the result to <paramref name="filePath"/>.
    /// </summary>
    Task ConvertToPdfAAsync(
        IPdfDocument document,
        string filePath,
        PdfAProfile profile = PdfAProfile.PdfA1B,
        CancellationToken ct = default
    );

    /// <summary>
    /// Adds the structural markers required for PDF/X (ISO 15930): an <c>/OutputIntents</c>
    /// entry with a <c>GTS_PDFX</c> intent referencing <paramref name="outputConditionIdentifier"/>
    /// (e.g. <c>"CGATS TR 001"</c>), a <c>GTS_PDFXVersion</c> marker, and pdfxid XMP metadata.
    /// Does not perform colour conversion (RGB→CMYK), which needs an ICC engine.
    /// </summary>
    /// <param name="document">Source document (must not be encrypted).</param>
    /// <param name="outputStream">Destination stream for the converted PDF.</param>
    /// <param name="profile">Target PDF/X profile (default PDF/X-1a:2001).</param>
    /// <param name="outputConditionIdentifier">Print-condition identifier for the output intent.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task ConvertToPdfXAsync(
        IPdfDocument document,
        Stream outputStream,
        PdfXProfile profile = PdfXProfile.PdfX1A2001,
        string outputConditionIdentifier = "CGATS TR 001",
        CancellationToken ct = default
    );

    /// <summary>
    /// Applies structural PDF/X markers and writes the result to <paramref name="filePath"/>.
    /// </summary>
    Task ConvertToPdfXAsync(
        IPdfDocument document,
        string filePath,
        PdfXProfile profile = PdfXProfile.PdfX1A2001,
        string outputConditionIdentifier = "CGATS TR 001",
        CancellationToken ct = default
    );

    /// <summary>
    /// Digitally signs <paramref name="document"/> with <paramref name="certificate"/> and
    /// writes the signed PDF to <paramref name="outputStream"/>.
    /// Uses PKCS#7 detached signature (<c>adbe.pkcs7.detached</c>, ISO 32000-1 §12.8.3).
    /// </summary>
    /// <param name="document">The source document (must not be encrypted).</param>
    /// <param name="certificate">Certificate with an associated private key used to sign.</param>
    /// <param name="outputStream">Destination for the signed PDF bytes.</param>
    /// <param name="options">Signing metadata (reason, location, etc.).</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task SignAsync(
        IPdfDocument document,
        System.Security.Cryptography.X509Certificates.X509Certificate2 certificate,
        Stream outputStream,
        SignatureOptions? options = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Digitally signs <paramref name="document"/> and writes the result to <paramref name="filePath"/>.
    /// </summary>
    Task SignAsync(
        IPdfDocument document,
        System.Security.Cryptography.X509Certificates.X509Certificate2 certificate,
        string filePath,
        SignatureOptions? options = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Reads and verifies all digital signatures in the given PDF bytes.
    /// Returns one <see cref="PdfSignatureInfo"/> per signature field found.
    /// An empty list means the document has no signatures (not that it is invalid).
    /// </summary>
    /// <param name="pdfBytes">The raw bytes of a PDF file (may be password-protected for structure, but signature verification is independent of encryption).</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task<IReadOnlyList<PdfSignatureInfo>> VerifySignaturesAsync(byte[] pdfBytes, CancellationToken ct = default);

    /// <summary>
    /// Re-encrypts <paramref name="document"/> with new passwords and writes the result to
    /// <paramref name="outputStream"/>. Pass empty strings for both passwords to remove encryption.
    /// The document must have been loaded (and decrypted) by this processor instance.
    /// </summary>
    /// <param name="document">The already-decrypted source document.</param>
    /// <param name="newUserPassword">New user password. Empty string = no password required to open.</param>
    /// <param name="newOwnerPassword">New owner password. Empty → defaults to <paramref name="newUserPassword"/>.</param>
    /// <param name="outputStream">Destination stream for the re-encrypted PDF.</param>
    /// <param name="algorithm">Encryption algorithm for the new file (default AES-256).</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task ChangePasswordsAsync(
        IPdfDocument document,
        string newUserPassword,
        string newOwnerPassword,
        Stream outputStream,
        PdfEncryptionAlgorithm algorithm = PdfEncryptionAlgorithm.Aes256,
        CancellationToken ct = default
    );

    /// <summary>
    /// Re-encrypts <paramref name="document"/> with new passwords and writes to <paramref name="filePath"/>.
    /// Pass empty strings for both passwords to remove encryption.
    /// </summary>
    Task ChangePasswordsAsync(
        IPdfDocument document,
        string newUserPassword,
        string newOwnerPassword,
        string filePath,
        PdfEncryptionAlgorithm algorithm = PdfEncryptionAlgorithm.Aes256,
        CancellationToken ct = default
    );

    /// <summary>
    /// Serializes <paramref name="document"/> and writes the result to <paramref name="filePath"/>.
    /// The document must have been produced by this processor instance.
    /// </summary>
    /// <param name="document">The document to serialize.</param>
    /// <param name="filePath">Destination file path. Created or overwritten.</param>
    /// <param name="options">Serialization options, or <see langword="null"/> to use <see cref="SaveOptions.Default"/>.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task SaveAsync(
        IPdfDocument document,
        string filePath,
        SaveOptions? options = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Serializes <paramref name="document"/> and writes the result to <paramref name="stream"/>.
    /// The document must have been produced by this processor instance.
    /// </summary>
    /// <param name="document">The document to serialize.</param>
    /// <param name="stream">A writable stream that receives the PDF bytes.</param>
    /// <param name="options">Serialization options, or <see langword="null"/> to use <see cref="SaveOptions.Default"/>.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task SaveAsync(
        IPdfDocument document,
        Stream stream,
        SaveOptions? options = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Writes <paramref name="metadata"/> to the document's information dictionary (<c>/Info</c>).
    /// Only non-<see langword="null"/> fields are written; <see langword="null"/> fields leave the
    /// existing value unchanged. To clear a field, pass an empty string.
    /// <para>
    /// The change is applied in-place to the in-memory document. Call
    /// <see cref="SaveAsync(IPdfDocument, string, SaveOptions?, CancellationToken)"/> afterwards to
    /// persist it.
    /// </para>
    /// </summary>
    /// <param name="document">The document to update. Must not be disposed.</param>
    /// <param name="metadata">Metadata fields to set. <see langword="null"/> fields are skipped.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task SetMetadataAsync(
        IPdfDocument document,
        DocumentMetadata metadata,
        CancellationToken ct = default
    );

    /// <summary>
    /// Returns the raw <see cref="Core.PdfObject"/> with the given indirect object number,
    /// or <see langword="null"/> when no such object exists.
    /// Useful for low-level inspection and debugging of PDF internals.
    /// </summary>
    /// <param name="document">The source document.</param>
    /// <param name="objectNumber">The 1-based indirect object number to resolve.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task<Core.PdfObject?> GetObjectByIdAsync(
        IPdfDocument document,
        int objectNumber,
        CancellationToken ct = default
    );

    /// <summary>
    /// Evicts all resolved objects from the document's in-memory cache.
    /// Subsequent object accesses will reparse from the source buffer.
    /// Call this after processing large pages to reduce memory pressure.
    /// </summary>
    /// <param name="document">The document whose cache should be trimmed.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task TrimCacheAsync(
        IPdfDocument document,
        CancellationToken ct = default
    );

    /// <summary>
    /// Sets the document's open action to navigate to <paramref name="pageNumber"/> when opened.
    /// Writes a <c>/OpenAction</c> GoTo action to the document catalog.
    /// </summary>
    /// <param name="document">The document to update.</param>
    /// <param name="pageNumber">1-based page number to navigate to on open.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task SetOpenActionAsync(
        IPdfDocument document,
        int pageNumber,
        CancellationToken ct = default
    );

    /// <summary>
    /// Sets the document's open action to the given action when opened.
    /// Supports GoTo (page navigation), URI (open URL), and Named (viewer command) actions.
    /// Use <see cref="Models.PdfOpenAction.GoTo"/>, <see cref="Models.PdfOpenAction.Uri"/>,
    /// or <see cref="Models.PdfOpenAction.Named"/> to construct the action.
    /// </summary>
    /// <param name="document">The document to update.</param>
    /// <param name="action">The open action to set.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task SetOpenActionAsync(
        IPdfDocument document,
        Models.PdfOpenAction action,
        CancellationToken ct = default
    );

    /// <summary>
    /// Strips PDF/A conformance metadata from <paramref name="document"/>:
    /// removes <c>/OutputIntents</c> from the catalog and deletes the
    /// <c>pdfaid:part</c> and <c>pdfaid:conformance</c> properties from the XMP stream.
    /// </summary>
    /// <param name="document">The document to modify.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task RemovePdfaComplianceAsync(
        IPdfDocument document,
        CancellationToken ct = default
    );

    /// <summary>
    /// Strips PDF/UA conformance metadata from <paramref name="document"/>:
    /// removes the <c>pdfuaid:part</c> XMP property and the <c>/MarkInfo</c>
    /// entry from the catalog.
    /// </summary>
    /// <param name="document">The document to modify.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task RemovePdfUaComplianceAsync(
        IPdfDocument document,
        CancellationToken ct = default
    );

    /// <summary>
    /// Replaces all unembedded Standard 14 font references in <paramref name="document"/>
    /// with embedded font programs from <paramref name="fontMap"/>.
    /// <para>
    /// Keys in <paramref name="fontMap"/> are base font names as they appear in the PDF
    /// (e.g. <c>"Helvetica"</c>, <c>"Times-Roman"</c>). Values are the raw TrueType or
    /// OpenType font bytes to embed as <c>/FontFile2</c> entries.
    /// </para>
    /// <para>
    /// Fonts not present in <paramref name="fontMap"/> are left unchanged.
    /// This is required for PDF/A conformance, which mandates that all fonts be embedded.
    /// </para>
    /// </summary>
    /// <param name="document">The document to update.</param>
    /// <param name="fontMap">
    /// Mapping from Standard 14 base font name to raw font bytes.
    /// Use <c>Unchained.Pdf.Rendering</c>'s <c>StandardFontEmbedder.DefaultFontMap</c>
    /// for the bundled DejaVu substitutions.
    /// </param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task EmbedStandardFontsAsync(
        IPdfDocument document,
        IReadOnlyDictionary<string, byte[]> fontMap,
        CancellationToken ct = default
    );

    /// <summary>
    /// Serializes the structure of <paramref name="document"/> to Unchained's document XML schema
    /// and returns the XML as a UTF-8 string.
    /// <para>
    /// The schema captures page dimensions, text spans (as <c>&lt;Paragraph&gt;</c> elements),
    /// annotations, and bookmarks. The output can be re-loaded with
    /// <see cref="LoadFromXmlAsync"/> to reconstruct a PDF.
    /// </para>
    /// </summary>
    /// <param name="document">The document to serialize.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task<string> SaveAsXmlAsync(
        IPdfDocument document,
        CancellationToken ct = default
    );

    /// <summary>
    /// Parses an Unchained document XML string and produces a new <see cref="IPdfDocument"/>.
    /// <para>
    /// Supported elements: <c>Document</c>, <c>Page</c>, <c>Paragraph</c>, <c>Heading</c>,
    /// <c>Table</c> (with <c>Header</c> and <c>Row</c>/<c>Cell</c> children), <c>Line</c>.
    /// </para>
    /// </summary>
    /// <param name="xmlContent">The Unchained document XML string to parse.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task<IPdfDocument> LoadFromXmlAsync(
        string xmlContent,
        CancellationToken ct = default
    );

    /// <summary>
    /// Replaces all occurrences of a named font in the document with new font bytes.
    /// Every /Font dictionary whose /BaseFont name matches <paramref name="fontName"/>
    /// (or its normalised base family, e.g. "Helvetica" matches "Helvetica-Bold") has its
    /// embedded font file updated and its /FontDescriptor metrics recalculated from the
    /// new font data. Use this to substitute non-embeddable fonts with licensed alternatives.
    /// </summary>
    /// <param name="document">The document to update.</param>
    /// <param name="fontName">Base font name to replace (e.g. "Helvetica", "Arial").</param>
    /// <param name="newFontBytes">Raw TrueType/OpenType font bytes for the replacement.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task ReplaceFontAsync(
        IPdfDocument document,
        string fontName,
        byte[] newFontBytes,
        CancellationToken ct = default
    );

    /// <summary>
    /// Subsets all embedded TrueType fonts in the document to the glyphs actually used,
    /// significantly reducing file size for large-glyph-set fonts (CJK, symbol fonts).
    /// Glyph 0 (.notdef) and composite-glyph components are always retained.
    /// Fonts that are not embedded, not TrueType, or already small are left unchanged.
    /// </summary>
    /// <param name="document">The document whose embedded fonts to subset.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task SubsetFontsAsync(
        IPdfDocument document,
        CancellationToken ct = default
    );
}
