using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Models;
using Unchained.Pdf.Rendering.Rendering;

namespace Unchained.Pdf.Rendering.Engine;

/// <summary>
/// Default <see cref="IRenderer"/> implementation backed by FreeType2 (via SharpFont).
/// <para>
/// Requires the FreeType2 native library to be present at runtime:
/// <c>freetype6.dll</c> on Windows, <c>libfreetype.so.6</c> on Linux,
/// <c>libfreetype.6.dylib</c> on macOS. On Windows the DLL is bundled with the package.
/// </para>
/// <para>
/// Reference the <c>Unchained.Pdf.Rendering</c> package to use this class;
/// the core <c>Unchained.Pdf</c> package only exposes the <see cref="IRenderer"/> interface.
/// </para>
/// </summary>
// ReSharper disable once MemberCanBeInternal
public sealed class PdfRenderer : IRenderer
{
    private readonly FontCache _fonts;
    private int _disposed;

    /// <summary>
    /// Creates a new <see cref="PdfRenderer"/> and initialises the FreeType2 library.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the FreeType2 native library cannot be loaded.
    /// </exception>
    public PdfRenderer()
    {
        try
        {
            _fonts = new FontCache();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new InvalidOperationException(
                "Could not initialise FreeType2. Ensure 'freetype6.dll' (Windows) or " +
                "'libfreetype.so.6' (Linux) is present in the application output directory. " +
                $"Inner: {ex.Message}",
                ex);
        }
    }

    /// <inheritdoc />
    public Task<byte[]> RenderPageAsync(
        IPdfPage page,
        RenderOptions options,
        CancellationToken ct = default
    ) => Task.Run(() => RenderPage(page, options), ct);

    /// <inheritdoc />
    public Task<IReadOnlyList<byte[]>> RenderDocumentAsync(
        IPdfDocument document,
        RenderOptions options,
        CancellationToken ct = default
    ) => Task.Run(() =>
    {
        var results = new List<byte[]>(document.PageCount);
        for (var i = 1; i <= document.PageCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            results.Add(RenderPage(document.Pages[i], options));
        }
        return (IReadOnlyList<byte[]>)results;
    }, ct);

    private byte[] RenderPage(IPdfPage page, RenderOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed == 1, this);

        var scale = options.Dpi / 72.0;
        var pixW = (int)Math.Ceiling(page.Width * scale);
        var pixH = (int)Math.Ceiling(page.Height * scale);

        var buffer = new RasterBuffer(pixW, pixH);
        buffer.Clear(r: 255, g: 255, b: 255);

        var fontMap = page.GetFontNameMap();
        var embeddedFontBytes = page.GetEmbeddedFontBytes();
        var imageXObjects = page.GetImageXObjects();
        // ReSharper disable once BadListLineBreaks
        var renderer = new PageRenderer(buffer, _fonts, scale, page.Height, embeddedFontBytes, imageXObjects);
        renderer.Render(page.GetContentOperators(), fontMap);

        return PdfPngEncoder.Encode(buffer);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _fonts.Dispose();
    }
}
