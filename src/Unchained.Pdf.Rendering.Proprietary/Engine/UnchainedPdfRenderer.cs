using Unchained.Drawing;
using Unchained.Drawing.Encoders;
using Unchained.Drawing.Text;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Rendering.Abstractions;
using Unchained.Pdf.Rendering.Proprietary.Rendering;

namespace Unchained.Pdf.Rendering.Proprietary.Engine;

/// <summary>
///     <see cref="IPdfRenderer" /> implementation backed by FreeType2 (via FreeTypeSharp).
///     <para>
///         The FreeType2 native library ships with FreeTypeSharp for Windows, macOS, and
///         linux-x64; linux-arm64 is supplied by <c>Unchained.Drawing.Runtimes</c>. The
///         binding resolves the platform binary automatically and falls back to a
///         system-installed FreeType2 when no bundled copy is present.
///     </para>
///     <para>
///         Reference the <c>Unchained.Pdf.Rendering</c> package to use this class
///     </para>
/// </summary>
// ReSharper disable once MemberCanBeInternal
public sealed class UnchainedPdfRenderer : IPdfRenderer
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private int _disposed;


    /// <summary>
    ///     Creates a new <see cref="UnchainedPdfRenderer" /> and initialises the FreeType2 library.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the FreeType2 native library cannot be loaded.
    /// </exception>
    public UnchainedPdfRenderer()
    {
        try
        {
            FontsForDiagnostics = new FontCache();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new InvalidOperationException(
                "Could not initialise FreeType2. The native FreeType library should be " +
                "supplied automatically by FreeTypeSharp (Windows/macOS/linux-x64) or " +
                "Unchained.Drawing.Runtimes (linux-arm64); a system-installed FreeType2 also works. " +
                $"Inner: {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    ///     Number of text operators (Tj / TJ / ' / ") that threw an exception during
    ///     the most recent render. 0 = healthy. &gt;0 = font loading or shaping issue.
    ///     Set after every <see cref="RenderPageAsync" /> call; thread-safe because
    ///     renders are serialised by <c>_lock</c>.
    /// </summary>
    // ReSharper disable once MemberCanBePrivate.Global
    internal int LastTextErrors { get; private set; }

    /// <summary>Exposes the font cache for diagnostic tests.</summary>
    internal FontCache FontsForDiagnostics { get; }


    /// <summary>Glyph bitmaps successfully passed to BlitGlyphBitmap in the last render.</summary>
    internal int LastGlyphsAttempted { get; private set; }

    /// <summary>Glyph bitmaps skipped because LoadGlyph threw in the last render.</summary>
    internal int LastGlyphsSkipped { get; private set; }

    /// <inheritdoc />
    public async Task<byte[]> RenderPageAsync(
        IPdfPage page,
        RenderOptions options,
        CancellationToken ct = default
    )
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
        CancellationToken ct = default
    )
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                    IReadOnlyList<byte[]> () =>
                    {
                        var results = new List<byte[]>(document.PageCount);
                        for (var i = 1; i <= document.PageCount; i++)
                        {
                            ct.ThrowIfCancellationRequested();
                            results.Add(RenderPage(document.Pages[i], options));
                        }

                        return results;
                    },
                    ct
                )
                .ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        FontsForDiagnostics.Dispose();
        _lock.Dispose();
    }

    private byte[] RenderPage(IPdfPage page, RenderOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed == 1, this);

        var scale = options.Dpi / 72.0;
        var rotate = page.Rotate; // 0 / 90 / 180 / 270

        // For rotated pages the pixel canvas dimensions are swapped.
        // Width/Height on IPdfPage already account for rotation (logical dimensions).
        // Use rounding consistent with common rasterizers (Pdfium): the device pixel
        // count is the truncated point×scale product, so a 3.8pt page at 96 DPI yields
        // 5 px, not 6. Ceiling would add a stray white row/column that mismatches.
        var pixW = Math.Max(1, (int)(page.Width * scale));
        var pixH = Math.Max(1, (int)(page.Height * scale));

        var buffer = new RasterBuffer(pixW, pixH);
        buffer.Clear();

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
        var rawH = rotate is 90 or 270 ? page.Width : page.Height;

        // ReSharper disable BadListLineBreaks
        // The initial CTM maps content-stream points to the pixel canvas.
        // For each rotation, the CropBox lower-left corner (cropLlx, cropLly) is
        // subtracted so that point (cropLlx, cropLly) maps to pixel (0, 0).
        double[] initialCtm = rotate switch
        {
            // 90° CW: (x,y) → pixel (y-cropLly, rawW-(x-cropLlx))
            //   = [0,1,1,0,-cropLly, rawW+cropLlx]? Simplification: shift before rotate
            90 => [0, 1, 1, 0, -cropLly, cropLlx],

            // 180°: flip both axes around crop origin, then shift to pixel origin
            180 => [-1, 0, 0, -1, rawW + cropLlx, rawH + cropLly],

            // 270° CW (= 90° CCW)
            270 => [0, -1, -1, 0, rawH + cropLly, rawW - cropLlx],

            // 0°: translate by -cropLlx, -cropLly so crop origin maps to pixel (0,0)
            _ => [1, 0, 0, 1, -cropLlx, -cropLly]
        };
        // ReSharper restore BadListLineBreaks

        // pageHeightPt is the Y reference used by UToPixel for the Y-flip.
        // For rotation 0/180 this is the CropBox height; for 90/270 it's the CropBox width.
        var pageHeightPt = rotate is 90 or 270 ? rawW : rawH;

        var fontMap = page.GetFontNameMap();
        var embeddedFontBytes = page.GetEmbeddedFontBytes();
        var imageXObjects = page.GetImageXObjects();
        var toUnicodeMaps = page.GetToUnicodeMaps();
        var compositeFonts = page.GetCompositeFonts();
        var extGStateAlphas = page.GetExtGStateAlphas();
        var shadings = page.GetShadings();
        var tilingPatterns = page.GetTilingPatterns();
        var softMasks = page.GetSoftMasks(pixW, pixH);
        // GetColorSpaces() is an internal method on PdfPageAdapter — access via cast.
        var colorSpaces = (page as PdfPageAdapter)?.GetColorSpaces()
                          ?? new Dictionary<string, ColorSpaceInfo>();
        var type3Fonts = (page as PdfPageAdapter)?.GetType3Fonts()
                         ?? new Dictionary<string, Type3FontInfo>();

        var renderer = new PageRenderer(
            buffer,
            FontsForDiagnostics,
            scale,
            pageHeightPt,
            embeddedFontBytes,
            imageXObjects,
            initialCtm,
            toUnicodeMaps,
            compositeFonts,
            extGStateAlphas,
            shadings,
            tilingPatterns,
            softMasks,
            colorSpaces,
            type3Fonts
        );
        renderer.Render(page.GetContentOperators(), fontMap);

        LastTextErrors = renderer.TextErrorCount;
        LastGlyphsAttempted = renderer.GlyphsAttempted;
        LastGlyphsSkipped = renderer.GlyphsSkipped;

        return options.Format switch
        {
            OutputFormat.Jpeg => JpegEncoder.Encode(buffer, options.JpegQuality),
            OutputFormat.Bmp => BmpEncoder.Encode(buffer),
            _ => PngEncoder.Encode(buffer)
        };
    }
}
