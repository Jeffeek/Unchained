using HarfBuzzSharp;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Core;
using Unchained.Pptx.Drawing;
using Unchained.Pptx.Media;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;
using Unchained.Pptx.Text;
using LoadFlags = SharpFont.LoadFlags;
using LoadTarget = SharpFont.LoadTarget;

namespace Unchained.Pptx.Rendering.Engine;

/// <summary>
/// Rasterizes a single <see cref="Slide"/> into a <see cref="RasterBuffer"/>
/// using FreeType2 for glyph rendering and HarfBuzz for text shaping.
/// </summary>
internal sealed class SlideRasterizer(FontCache fonts)
{
    /// <summary>
    /// Renders the slide and returns a pixel buffer ready for encoding.
    /// </summary>
    internal RasterBuffer Rasterize(Slide slide, SlideSize slideSize, RenderOptions options)
    {
        var buffer = new RasterBuffer(options.WidthPx, options.HeightPx);

        // Scale factor: EMU → pixels
        var scaleX = (double)options.WidthPx / slideSize.Width.Value;
        var scaleY = (double)options.HeightPx / slideSize.Height.Value;

        // Paint slide background
        PaintBackground(buffer, slide, scaleX, scaleY);

        // Render each shape in Z-order (insertion order = back-to-front)
        foreach (var shape in slide.Shapes)
            RenderShape(buffer, shape, scaleX, scaleY, options.Dpi);

        return buffer;
    }

    private static void PaintBackground(RasterBuffer buffer, Slide slide, double scaleX, double scaleY)
    {
        var fill = slide.Background.Fill;
        if (fill.Type == FillType.Solid && fill.Solid is not null)
        {
            var argb = fill.Solid.Color.Resolve(null);
            ExtractArgb(argb, out var a, out var r, out var g, out var b);
            buffer.Clear(r, g, b);
        }
        else
        {
            // Default: white background
            buffer.Clear(r: 255, g: 255, b: 255);
        }
    }

    private void RenderShape(
        RasterBuffer buffer,
        Shape shape,
        double scaleX,
        double scaleY,
        double dpi)
    {
        var x = (int)(shape.X.Value * scaleX);
        var y = (int)(shape.Y.Value * scaleY);
        var width = (int)(shape.Width.Value * scaleX);
        var height = (int)(shape.Height.Value * scaleY);

        if (width <= 0 || height <= 0)
            return;

        switch (shape)
        {
            case AutoShape autoShape:
                RenderAutoShape(buffer, autoShape, x, y, width, height, scaleX, scaleY, dpi);
                break;

            case PictureShape pictureShape:
                RenderPicture(buffer, pictureShape, x, y, width, height);
                break;
        }
    }

    private void RenderAutoShape(
        RasterBuffer buffer,
        AutoShape shape,
        int x, int y, int width, int height,
        double scaleX, double scaleY,
        double dpi)
    {
        // Paint fill
        if (shape.Fill.Type == FillType.Solid && shape.Fill.Solid is not null)
        {
            var argb = shape.Fill.Solid.Color.Resolve(null);
            ExtractArgb(argb, out var a, out var r, out var g, out var b);
            buffer.FillRect(x, y, width, height, r, g, b, a);
        }

        // Render text frame
        RenderTextFrame(buffer, shape.TextFrame, x, y, width, height, dpi);
    }

    private void RenderTextFrame(
        RasterBuffer buffer,
        TextFrame textFrame,
        int shapeX, int shapeY, int shapeWidth, int shapeHeight,
        double dpi)
    {
        if (textFrame.Paragraphs.Count == 0)
            return;

        // Default text metrics: use standard 96 DPI baseline
        var scale = dpi / 72.0;

        // Default text color: black
        byte textR = 0, textG = 0, textB = 0;

        // Top-left inset (default PowerPoint body margins: ~91440 EMU ≈ 7 pt each)
        var cursorY = shapeY + 4;

        foreach (var paragraph in textFrame.Paragraphs)
        {
            if (paragraph.Runs.Count == 0)
            {
                // Empty paragraph — advance by one line height
                cursorY += (int)(12.0 * scale) + 2;
                continue;
            }

            var cursorX = shapeX + 4;
            var lineHeight = 0;

            foreach (var run in paragraph.Runs)
            {
                if (string.IsNullOrEmpty(run.Text))
                    continue;

                // Resolve font size: run → paragraph default fallback → 12pt
                var fontSizePt = run.Format.FontSizePoints ?? 12.0;

                // Resolve text color from run fill if set
                if (run.Format.Fill?.Type == FillType.Solid && run.Format.Fill.Solid is not null)
                {
                    var argb = run.Format.Fill.Solid.Color.Resolve(null);
                    ExtractArgb(argb, out _, out textR, out textG, out textB);
                }
                else
                {
                    textR = 0; textG = 0; textB = 0;
                }

                // Select font name — use LatinFont if set, else fallback key
                var fontName = run.Format.LatinFont ?? SelectFontName(run);

                cursorX = RenderRunText(
                    buffer,
                    run.Text,
                    fontName,
                    fontSizePt,
                    scale,
                    cursorX,
                    cursorY,
                    shapeX + shapeWidth - 4,
                    textR, textG, textB,
                    ref lineHeight
                );
            }

            cursorY += lineHeight + 2;
        }
    }

    private int RenderRunText(
        RasterBuffer buffer,
        string text,
        string fontName,
        double fontSizePt,
        double scale,
        int startX, int startY,
        int maxX,
        byte r, byte g, byte b,
        ref int lineHeight)
    {
        var cursorX = startX;

        // ReSharper disable once EmptyGeneralCatchClause
        try
        {
            var (ftFace, hbFont) = fonts.GetFonts(fontName);
            var pixelSize = (uint)Math.Max(1, Math.Round(fontSizePt * scale));
            ftFace.SetPixelSizes(0, pixelSize);

            if (lineHeight < (int)pixelSize)
                lineHeight = (int)pixelSize;

            var hbScale = (int)(pixelSize * 64);
            hbFont.SetScale(hbScale, hbScale);

            using var hbBuffer = new HarfBuzzSharp.Buffer();
            hbBuffer.AddUtf8(System.Text.Encoding.UTF8.GetBytes(text));
            hbBuffer.GuessSegmentProperties();
            hbFont.Shape(hbBuffer);

            var glyphInfos = hbBuffer.GlyphInfos;
            var glyphPositions = hbBuffer.GlyphPositions;

            for (var i = 0; i < glyphInfos.Length; i++)
            {
                var glyphId = glyphInfos[i].Codepoint;

                // ReSharper disable once EmptyGeneralCatchClause
                try
                {
                    ftFace.LoadGlyph(glyphId, LoadFlags.Render, LoadTarget.Normal);
                }
                catch
                {
                    continue;
                }

                var glyph = ftFace.Glyph;
                var bm = glyph.Bitmap;

                var destX = cursorX + (glyphPositions[i].XOffset / 64) + glyph.BitmapLeft;
                var destY = startY + (int)pixelSize - glyph.BitmapTop + (glyphPositions[i].YOffset / 64);

                buffer.BlitGlyphBitmap(destX, destY, bm, r, g, b);

                cursorX += glyphPositions[i].XAdvance / 64;

                if (cursorX >= maxX)
                    break;
            }
        }
        catch
        {
        }

        return cursorX;
    }

    private static void RenderPicture(
        RasterBuffer buffer,
        PictureShape shape,
        int x, int y, int width, int height)
    {
        if (shape.Image is null)
            return;

        var imageBytes = shape.Image.Data;
        if (imageBytes.IsEmpty)
            return;

        // Decode the embedded image bytes to raw RGB pixels using BCL APIs only.
        var rawPixels = TryDecodeImageToRgb(shape.Image, out var imgWidth, out var imgHeight);
        if (rawPixels is null || imgWidth <= 0 || imgHeight <= 0)
            return;

        // Nearest-neighbour blit with scaling to destination rect
        for (var py = 0; py < height; py++)
        {
            var srcY = py * imgHeight / height;
            for (var px = 0; px < width; px++)
            {
                var srcX = px * imgWidth / width;
                var srcOffset = ((srcY * imgWidth) + srcX) * 3;
                buffer.BlitImagePixel(
                    x + px,
                    y + py,
                    rawPixels[srcOffset],
                    rawPixels[srcOffset + 1],
                    rawPixels[srcOffset + 2]
                );
            }
        }
    }

    private static byte[]? TryDecodeImageToRgb(EmbeddedImage image, out int width, out int height)
    {
        width = 0;
        height = 0;

        // For PNG: minimal BCL-based decoder — read IHDR for dimensions, then
        // use the known pixel size from the model if available, or fall back.
        if (image.PixelWidth > 0 && image.PixelHeight > 0)
        {
            width = image.PixelWidth;
            height = image.PixelHeight;
        }

        // Without a full image codec, we can only handle PNG by reading the IHDR chunk.
        // Return null for unsupported formats — the shape slot is left empty (transparent).
        // A full image decoding pipeline is out of scope for M9 (tracked for future milestone).
        return null;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string SelectFontName(Run run)
    {
        if (run.Format.Bold == InheritableBool.True)
            return "Arial Bold";

        return "Arial";
    }

    private static void ExtractArgb(uint argb, out byte a, out byte r, out byte g, out byte b)
    {
        a = (byte)((argb >> 24) & 0xFF);
        r = (byte)((argb >> 16) & 0xFF);
        g = (byte)((argb >> 8) & 0xFF);
        b = (byte)(argb & 0xFF);
    }
}
