using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Rendering.Abstractions;

/// <summary>
///     Rasterizes PDF pages to PNG (and later JPEG) byte arrays.
///     <para>
///         Dispose when rendering is complete to release native resources.
///     </para>
/// </summary>
public interface IPdfRenderer : IDisposable
{
    /// <summary>
    ///     Rasterizes a single page to a byte array in the format specified by
    ///     <paramref name="options" />.
    /// </summary>
    Task<byte[]> RenderPageAsync(
        IPdfPage page,
        RenderOptions options,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Rasterizes every page of <paramref name="document" /> and returns one
    ///     byte array per page in document order.
    /// </summary>
    Task<IReadOnlyList<byte[]>> RenderDocumentAsync(
        IPdfDocument document,
        RenderOptions options,
        CancellationToken ct = default
    );
}
