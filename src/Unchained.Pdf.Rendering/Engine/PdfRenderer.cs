using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Models;
using Unchained.Pdf.Rendering.Rendering;
using Unchained.Drawing;
using Unchained.Drawing.Text;

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
    private readonly SemaphoreSlim _lock = new(1, 1);
    private int _disposed;

    /// <summary>
    /// Number of text operators (Tj / TJ / ' / ") that threw an exception during
    /// the most recent render. 0 = healthy. &gt;0 = font loading or shaping issue.
    /// Set after every <see cref="RenderPageAsync"/> call; thread-safe because
    /// renders are serialised by <c>_lock</c>.
    /// </summary>
    // ReSharper disable once MemberCanBePrivate.Global
    internal int LastTextErrors { get; private set; }

    /// <summary>Exposes the font cache for diagnostic tests.</summary>
    internal FontCache FontsForDiagnostics => _fonts;


    /// <summary>Glyph bitmaps successfully passed to BlitGlyphBitmap in the last render.</summary>
    internal int LastGlyphsAttempted { get; private set; }

    /// <summary>Glyph bitmaps skipped because LoadGlyph threw in the last render.</summary>
    internal int LastGlyphsSkipped { get; private set; }


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
    public async Task<byte[]> RenderPageAsync(
        IPdfPage page,
        RenderOptions options,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => RenderPage(page, options), ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<byte[]>> RenderDocumentAsync(
        IPdfDocument document,
        RenderOptions options,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await Task.Run(IReadOnlyList<byte[]> () =>
                {
                    var results = new List<byte[]>(document.PageCount);
                    for (var i = 1; i <= document.PageCount; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        results.Add(RenderPage(document.Pages[i], options));
                    }

                    return results;
                },
                ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private byte[] RenderPage(IPdfPage page, RenderOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed == 1, this);

        var scale  = options.Dpi / 72.0;
        var rotate = page.Rotate; // 0 / 90 / 180 / 270

        // For rotated pages the pixel canvas dimensions are swapped.
        // Width/Height on IPdfPage already account for rotation (logical dimensions).
        var pixW = Math.Max(1, (int)Math.Ceiling(page.Width  * scale));
        var pixH = Math.Max(1, (int)Math.Ceiling(page.Height * scale));

        var buffer = new RasterBuffer(pixW, pixH);
        buffer.Clear(r: 255, g: 255, b: 255);

        // UToPixel in PageRenderer does:  px = ctm_x * scale,  py = (pageHeightPt - ctm_y) * scale
        // We choose pageHeightPt and the initial CTM so that content rendered in the
        // unrotated coordinate space lands at the correct pixel.
        //
        // CropBox visible dimensions (before rotation swap).
        // Width/Height are already CropBox-based; we also need the origin offset
        // to translate content-stream coordinates (MediaBox space) into CropBox space.
        var cropLlx = page.CropOriginX;
        var cropLly = page.CropOriginY;

        var rawW = rotate is 90 or 270 ? page.Height : page.Width;
        var rawH = rotate is 90 or 270 ? page.Width  : page.Height;

        // ReSharper disable BadListLineBreaks
        // The initial CTM maps content-stream points to the pixel canvas.
        // For each rotation, the CropBox lower-left corner (cropLlx, cropLly) is
        // subtracted so that point (cropLlx, cropLly) maps to pixel (0, 0).
        double[] initialCtm = rotate switch
        {
            // 90° CW: (x,y) → pixel (y-cropLly, rawW-(x-cropLlx))
            //   = [0,1,1,0,-cropLly, rawW+cropLlx]? Simplification: shift before rotate
            90  => [0, 1, 1, 0, -cropLly, cropLlx],

            // 180°: (x,y) → pixel (rawW-(x-cropLlx), rawH-(y-cropLly))
            180 => [-1, 0, 0, 1, rawW + cropLlx, cropLly],

            // 270° CW (= 90° CCW)
            270 => [0, -1, -1, 0, rawH + cropLly, rawW - cropLlx],

            // 0°: translate by -cropLlx, -cropLly so crop origin maps to pixel (0,0)
            _   => [1, 0, 0, 1, -cropLlx, -cropLly]
        };
        // ReSharper restore BadListLineBreaks

        // pageHeightPt is the Y reference used by UToPixel for the Y-flip.
        // For rotation 0/180 this is the CropBox height; for 90/270 it's the CropBox width.
        var pageHeightPt = rotate is 90 or 270 ? rawW : rawH;

        var fontMap          = page.GetFontNameMap();
        var embeddedFontBytes = page.GetEmbeddedFontBytes();
        var imageXObjects    = page.GetImageXObjects();
        var toUnicodeMaps    = page.GetToUnicodeMaps();

        var renderer = new PageRenderer(
            buffer, _fonts, scale, pageHeightPt,
            embeddedFontBytes, imageXObjects, initialCtm, toUnicodeMaps);
        renderer.Render(page.GetContentOperators(), fontMap);

        LastTextErrors      = renderer.TextErrorCount;
        LastGlyphsAttempted = renderer.GlyphsAttempted;
        LastGlyphsSkipped   = renderer.GlyphsSkipped;

        return PngEncoder.Encode(buffer);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _fonts.Dispose();
            _lock.Dispose();
        }
    }
}
