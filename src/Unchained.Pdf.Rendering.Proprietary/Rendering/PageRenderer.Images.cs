using Unchained.Drawing;
using Unchained.Pdf.Core;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Rendering.Proprietary.Rendering;

// Image XObject (`Do`) and inline-image (`BI…EI`) blitting with area-averaged downscaling,
// plus soft-mask rendering and per-pixel soft-mask modulation.
internal sealed partial class PageRenderer
{
    private void PaintInlineImage(PdfInlineImage img)
    {
        // Inline images (BI…EI) fill the rectangle [0, 0, UserWidth, UserHeight]
        // in user space, unlike XObject images (Do) which fill the unit square.
        // The current CTM then maps this user-space rect to pixel space.
        var uw = img.UserWidth;
        var uh = img.UserHeight;

        var (x0, y0) = UToPixel(0, 0);
        var (x1, y1) = UToPixel(uw, uh);

        var dstX = (int)Math.Min(x0, x1);
        var dstY = (int)Math.Min(y0, y1);
        var dstW = (int)Math.Abs(x1 - x0);
        var dstH = (int)Math.Abs(y1 - y0);

        if (dstW <= 0 || dstH <= 0) return;

        BlitScaledImage(
            img.RgbData,
            img.Width,
            img.Height,
            dstX,
            dstY,
            dstW,
            dstH
        );
    }

    private void PaintXObject(string resourceName)
    {
        if (imageXObjects is null) return;
        if (!imageXObjects.TryGetValue(resourceName, out var img)) return;

        // The Do operator places the image in the unit square [0,0]→[1,1] in user
        // space, transformed by the current CTM.
        var (x0, y0) = UToPixel(0, 0);
        var (x1, y1) = UToPixel(1, 1);

        var dstX = (int)Math.Min(x0, x1);
        var dstY = (int)Math.Min(y0, y1);
        var dstW = (int)Math.Abs(x1 - x0);
        var dstH = (int)Math.Abs(y1 - y0);

        if (dstW <= 0 || dstH <= 0) return;

        BlitScaledImage(
            img.RgbData,
            img.Width,
            img.Height,
            dstX,
            dstY,
            dstW,
            dstH,
            img.Alpha
        );
    }

    // Scales an RGB image into the destination rectangle. When the image is downscaled
    // (more source than destination pixels) each destination pixel averages the source
    // box it covers, matching the area-averaging that Pdfium uses — nearest-neighbour
    // alone produces harsh aliasing and large pixel differences on small/scaled images.
    // When upscaling, falls back to nearest-neighbour sampling. When an alpha channel is
    // supplied (from an /SMask), pixels are composited over the background using it.
    private void BlitScaledImage(
        IReadOnlyList<byte> rgb,
        int srcW,
        int srcH,
        int dstX,
        int dstY,
        int dstW,
        int dstH,
        byte[]? alpha = null
    )
    {
        if (srcW <= 0 || srcH <= 0) return;

        var downscale = srcW > dstW || srcH > dstH;

        for (var py = 0; py < dstH; py++)
        for (var px = 0; px < dstW; px++)
        {
            byte r, g, b;
            int a;
            if (downscale)
            {
                // Average the source box [sx0,sx1)×[sy0,sy1) covered by this dest pixel.
                var sx0 = px * srcW / dstW;
                var sx1 = Math.Max(sx0 + 1, (px + 1) * srcW / dstW);
                var sy0 = py * srcH / dstH;
                var sy1 = Math.Max(sy0 + 1, (py + 1) * srcH / dstH);
                long sr = 0, sg = 0, sb = 0, sa = 0;
                var n = 0;
                for (var sy = sy0; sy < sy1 && sy < srcH; sy++)
                for (var sx = sx0; sx < sx1 && sx < srcW; sx++)
                {
                    var idx = (sy * srcW) + sx;
                    var o = idx * 3;
                    sr += rgb[o];
                    sg += rgb[o + 1];
                    sb += rgb[o + 2];
                    sa += alpha is not null ? alpha[idx] : 255;
                    n++;
                }

                if (n == 0) continue;

                r = (byte)(sr / n);
                g = (byte)(sg / n);
                b = (byte)(sb / n);
                a = (int)(sa / n);
            }
            else
            {
                var sx = px * srcW / dstW;
                var sy = py * srcH / dstH;
                var idx = (sy * srcW) + sx;
                var o = idx * 3;
                r = rgb[o];
                g = rgb[o + 1];
                b = rgb[o + 2];
                a = alpha is not null ? alpha[idx] : 255;
            }

            switch (a)
            {
                case <= 0:
                    continue;
                case >= 255:
                    buffer.BlitImagePixel(dstX + px, dstY + py, r, g, b);
                break;
                default:
                    buffer.BlendPixel(
                        dstX + px,
                        dstY + py,
                        r,
                        g,
                        b,
                        (byte)a,
                        _gs.BlendMode
                    );
                break;
            }
        }
    }

    // Modulates a source alpha by the active soft mask at device pixel (x, y).
    // Returns the original alpha when no soft mask is active or (x,y) is out of range.
    private byte SoftMaskAlpha(int x, int y, byte a)
    {
        if (_gs.SoftMask is not { } mask) return a;
        if ((uint)x >= (uint)_gs.SoftMaskWidth || (uint)y >= (uint)_gs.SoftMaskHeight) return 0;

        var maskA = mask[(y * _gs.SoftMaskWidth) + x];
        return (byte)(a * maskA / 255);
    }

    // Fills a rectangle applying the soft mask per-pixel (used when HasSoftMask is true).
    private void FillRectSoftMasked(
        int px,
        int py,
        int pw,
        int ph,
        byte r,
        byte g,
        byte b,
        byte baseAlpha,
        string blendMode
    )
    {
        var x2 = px + pw;
        var y2 = py + ph;
        for (var y = py; y < y2; y++)
        for (var x = px; x < x2; x++)
        {
            buffer.SetPixel(
                x,
                y,
                r,
                g,
                b,
                SoftMaskAlpha(x, y, baseAlpha),
                blendMode
            );
        }
    }

    // Fills a scanline span applying the soft mask per-pixel.
    private void FillSpanSoftMasked(
        int y,
        int x0,
        int x1,
        byte r,
        byte g,
        byte b,
        byte baseAlpha,
        string blendMode
    )
    {
        for (var x = x0; x <= x1; x++)
        {
            buffer.SetPixel(
                x,
                y,
                r,
                g,
                b,
                SoftMaskAlpha(x, y, baseAlpha),
                blendMode
            );
        }
    }

    // Renders a soft mask Form XObject into a per-pixel alpha array (device space).
    // The mask form is rendered into a temporary black-backdrop RasterBuffer at the same
    // device dimensions as the main page buffer. For /Alpha masks the rendered brightness
    // is taken as opacity (black=transparent, white=opaque). For /Luminosity masks the
    // standard luminance coefficients are applied. ISO 32000-1 §11.6.5.
    private byte[] RenderSoftMask(SoftMaskInfo smInfo)
    {
        try
        {
            var maskBuf = new RasterBuffer(smInfo.WidthPx, smInfo.HeightPx);
            maskBuf.Clear(0, 0, 0);

            var bbox = smInfo.BBox;
            var m = smInfo.Matrix;
            var formW = bbox[2] - bbox[0];
            var formH = bbox[3] - bbox[1];
            var sx = formW > 0 ? smInfo.WidthPx / formW : 1.0;
            var sy = formH > 0 ? smInfo.HeightPx / formH : 1.0;
            var s = Math.Min(sx, sy);

            double[] ctm =
            [
                m[0] * s, m[1] * s,
                m[2] * s, m[3] * s,
                (m[4] - bbox[0]) * s,
                (m[5] - bbox[1]) * s
            ];

            var formPage = smInfo.FormPage;
            var maskRenderer = new PageRenderer(
                maskBuf,
                fonts,
                s,
                formH > 0 ? formH : pageHeightPt,
                formPage.GetEmbeddedFontBytes(),
                formPage.GetImageXObjects(),
                ctm,
                formPage.GetToUnicodeMaps(),
                formPage.GetCompositeFonts(),
                formPage.GetExtGStateAlphas(),
                formPage.GetShadings(),
                formPage.GetTilingPatterns(),
                null,
                (formPage as PdfPageAdapter)?.GetColorSpaces()
            );

            maskRenderer.Render(smInfo.Operators, formPage.GetFontNameMap());

            var pixels = maskBuf.ToArgbBytes();
            var alpha = new byte[smInfo.WidthPx * smInfo.HeightPx];
            for (var y = 0; y < smInfo.HeightPx; y++)
            for (var x = 0; x < smInfo.WidthPx; x++)
            {
                var o = ((y * smInfo.WidthPx) + x) * 4;
                alpha[(y * smInfo.WidthPx) + x] = smInfo.MaskType == RenderingConstants.SoftMaskLuminosity
                    ? (byte)(((pixels[o] * RenderingConstants.LumaR)
                              + (pixels[o + 1] * RenderingConstants.LumaG)
                              + (pixels[o + 2] * RenderingConstants.LumaB)) >> RenderingConstants.LumaShift)
                    : pixels[o];
            }

            return alpha;
        }
        // ReSharper disable once EmptyGeneralCatchClause
        catch
        {
            // On failure, return a fully-opaque mask so content is not incorrectly hidden.
            var fallback = new byte[smInfo.WidthPx * smInfo.HeightPx];
            Array.Fill(fallback, RenderingConstants.OpaqueAlpha);
            return fallback;
        }
    }
}
