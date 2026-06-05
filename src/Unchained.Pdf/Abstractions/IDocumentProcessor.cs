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
}
