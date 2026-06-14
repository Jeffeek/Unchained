using Unchained.Pdf.Models;

namespace Unchained.Pdf.Abstractions;

/// <summary>
///     Reads and writes file attachments embedded in a PDF document via the
///     <c>/Names /EmbeddedFiles</c> name tree (ISO 32000-1 §7.11.4).
///     Also manages the <c>/Collection</c> entry that marks the document as a PDF Portfolio.
/// </summary>
public interface IEmbeddedFileEditor
{
    /// <summary>
    ///     Returns all embedded files from the document's <c>/Names /EmbeddedFiles</c> name tree.
    ///     Returns an empty list when no embedded files exist.
    /// </summary>
    IReadOnlyList<EmbeddedFile> GetEmbeddedFiles(IPdfDocument document);

    /// <summary>
    ///     Embeds <paramref name="file" /> into the document's <c>/Names /EmbeddedFiles</c> name tree.
    ///     If a file with the same <see cref="EmbeddedFile.Name" /> already exists it is replaced.
    /// </summary>
    /// <param name="document">The document to update.</param>
    /// <param name="file">The file to embed.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task AddEmbeddedFileAsync(
        IPdfDocument document,
        EmbeddedFile file,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Removes the embedded file with the given <paramref name="name" /> from the document.
    ///     Does nothing if no file with that name exists.
    /// </summary>
    /// <param name="document">The document to update.</param>
    /// <param name="name">The name key of the file to remove.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task RemoveEmbeddedFileAsync(
        IPdfDocument document,
        string name,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Adds a <c>/Collection</c> dictionary to the document catalog, marking it as a
    ///     PDF Portfolio. PDF readers display an attachment panel for portfolios.
    ///     Has no effect when a <c>/Collection</c> entry already exists.
    /// </summary>
    /// <param name="document">The document to update.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task EnablePortfolioModeAsync(
        IPdfDocument document,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Removes the <c>/Collection</c> entry from the document catalog,
    ///     reverting portfolio mode.
    /// </summary>
    /// <param name="document">The document to update.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task DisablePortfolioModeAsync(
        IPdfDocument document,
        CancellationToken ct = default
    );
}
