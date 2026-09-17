using Unchained.Pdf.Models;

namespace Unchained.Pdf.Abstractions;

/// <summary>
///     Authors content on existing pages: inserts blank pages, draws text, and places images.
///     All operations mutate document in-place and follow the standard
///     full-rewrite serialization used across the library.
/// </summary>
// ReSharper disable once MemberCanBeInternal
public interface IPageContentEditor
{
    /// <summary>
    ///     Inserts a new blank page and returns its 1-based page number.
    /// </summary>
    /// <param name="document">The document to modify. Must not be disposed.</param>
    /// <param name="width">Page width in points (default 612 = US Letter).</param>
    /// <param name="height">Page height in points (default 792 = US Letter).</param>
    /// <param name="atPageNumber">
    ///     1-based position at which to insert the page. When <see langword="null" />, the page is
    ///     appended after the last page. Valid range is 1 to <c>PageCount + 1</c>.
    /// </param>
    /// <param name="ct">Token to cancel the operation.</param>
    /// <returns>The 1-based page number of the inserted page.</returns>
    Task<int> AddBlankPageAsync(
        IPdfDocument document,
        double width = 612,
        double height = 792,
        int? atPageNumber = null,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Draws a single line of text at the given position (PDF user space, origin bottom-left).
    /// </summary>
    /// <param name="document">The document to modify. Must not be disposed.</param>
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="text">The text to draw. Characters outside Latin-1 are replaced with '?'.</param>
    /// <param name="x">X coordinate of the text origin, in points.</param>
    /// <param name="y">Y coordinate of the text baseline, in points.</param>
    /// <param name="options">Font and colour styling; defaults to black 12&#8239;pt Helvetica.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task DrawTextAsync(
        IPdfDocument document,
        int pageNumber,
        string text,
        double x,
        double y,
        TextDrawOptions? options = null,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Places <paramref name="image" /> on a page, scaled to fit the given rectangle
    ///     (PDF user space, origin bottom-left).
    /// </summary>
    /// <param name="document">The document to modify. Must not be disposed.</param>
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="image">The raster image to embed.</param>
    /// <param name="x">X coordinate of the lower-left corner, in points.</param>
    /// <param name="y">Y coordinate of the lower-left corner, in points.</param>
    /// <param name="width">Placement width in points.</param>
    /// <param name="height">Placement height in points.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    /// <exception cref="ArgumentException">
    ///     Thrown when the image's pixel buffers do not match its declared dimensions.
    /// </exception>
    Task DrawImageAsync(
        IPdfDocument document,
        int pageNumber,
        ImageContent image,
        double x,
        double y,
        double width,
        double height,
        CancellationToken ct = default
    );
}
