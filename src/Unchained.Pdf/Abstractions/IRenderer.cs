using Unchained.Pdf.Models;

namespace Unchained.Pdf.Abstractions;

/// <summary>
///     Rasterizes PDF pages to PNG (and later JPEG) byte arrays.
///     <para>
///         Implementations own a FreeType2 library handle and font cache.
///         Dispose when rendering is complete to release native resources.
///     </para>
///     <para>
///         Requires the FreeType2 native library, which the rendering package supplies
///         automatically per platform (via FreeTypeSharp, with linux-arm64 from
///         Unchained.Drawing.Runtimes); a system-installed FreeType2 also works.
///     </para>
/// </summary>
public interface IRenderer : IDisposable
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
