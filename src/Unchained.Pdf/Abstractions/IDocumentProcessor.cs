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
}
