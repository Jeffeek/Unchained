using Unchained.Pdf.Models;

namespace Unchained.Pdf.Abstractions;

/// <summary>
/// Rasterizes PDF pages to PNG (and later JPEG) byte arrays.
/// <para>
/// Implementations own a FreeType2 library handle and font cache.
/// Dispose when rendering is complete to release native resources.
/// </para>
/// <para>
/// Requires the FreeType2 native library (<c>freetype6.dll</c> on Windows,
/// <c>libfreetype.so.6</c> on Linux, <c>libfreetype.6.dylib</c> on macOS).
/// </para>
/// </summary>
public interface IRenderer : IDisposable
{
    /// <summary>
    /// Rasterizes a single page to a byte array in the format specified by
    /// <paramref name="options"/>.
    /// </summary>
    Task<byte[]> RenderPageAsync(
        IPdfPage page,
        RenderOptions options,
        CancellationToken ct = default
    );

    /// <summary>
    /// Rasterizes every page of <paramref name="document"/> and returns one
    /// byte array per page in document order.
    /// </summary>
    Task<IReadOnlyList<byte[]>> RenderDocumentAsync(
        IPdfDocument document,
        RenderOptions options,
        CancellationToken ct = default
    );
}
