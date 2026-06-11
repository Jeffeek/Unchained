using Shouldly;
using System.IO.Compression;
using Unchained.Drawing.Constants;
using Unchained.Drawing.Encoders;
using Unchained.Ooxml;
using Unchained.Pptx.Rendering;
using Unchained.Pptx.Rendering.Engine;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
/// Verifies that glyph bitmaps actually blit to the raster (the Windows-x64 SharpFont
/// offset bug previously left text invisible). Decodes the PNG and counts dark pixels.
/// </summary>
public sealed class GlyphRenderTests : PptxTestBase
{
    [Fact]
    public async Task TextBox_ProducesDarkTextPixels()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(
            Emu.FromInches(0.5), Emu.FromInches(0.5),
            Emu.FromInches(8), Emu.FromInches(2),
            "Rendering Works Now");

        var image = await SlideRenderer.RenderAsync(
            doc.Slides[0], doc.SlideSize,
            new RenderOptions { WidthPx = 640, HeightPx = 360 });

        var darkPixels = CountDarkPixels(image.Data.ToArray(), 640, 360);

        // Glyph strokes are near-black on a white slide. Before the fix this was ~0.
        darkPixels.ShouldBeGreaterThan(50);
    }

    [Fact]
    public async Task BlankSlide_HasNoDarkPixels()
    {
        var doc = PptxFixtures.WithSlides(1);

        var image = await SlideRenderer.RenderAsync(
            doc.Slides[0], doc.SlideSize,
            new RenderOptions { WidthPx = 640, HeightPx = 360 });

        CountDarkPixels(image.Data.ToArray(), 640, 360).ShouldBe(0);
    }

    [Fact]
    public async Task EmbeddedPngPicture_RendersImagePixels()
    {
        // Build a solid magenta 8×8 PNG, embed it as a picture filling part of the slide.
        var src = new Unchained.Drawing.RasterBuffer(8, 8);
        src.Clear(255, 0, 255); // magenta — not a default text/background colour

        var pngBytes = PngEncoder.Encode(src);

        var doc = PptxFixtures.WithSlides(1);
        var image = doc.Media.AddImage(pngBytes, "image/png");
        doc.Slides[0].Shapes.AddPicture(
            image,
            Emu.FromInches(1), Emu.FromInches(1),
            Emu.FromInches(4), Emu.FromInches(3));

        var rendered = await SlideRenderer.RenderAsync(
            doc.Slides[0], doc.SlideSize,
            new RenderOptions { WidthPx = 640, HeightPx = 360 });

        var magenta = CountMagentaPixels(rendered.Data.ToArray(), 640, 360);
        magenta.ShouldBeGreaterThan(100);
    }

    // Decodes a filter-None RGBA PNG produced by PngEncoder and counts near-black pixels.
    private static int CountDarkPixels(byte[] png, int width, int height)
    {
        var idat = ExtractIdat(png);
        using var input = new MemoryStream(idat);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        var bytes = raw.ToArray();

        var stride = 1 + (width * 4);
        var dark = 0;
        for (var y = 0; y < height; y++)
        {
            var rowStart = (y * stride) + 1; // skip filter byte
            for (var x = 0; x < width; x++)
            {
                var p = rowStart + (x * 4);
                if (bytes[p] < 80 && bytes[p + 1] < 80 && bytes[p + 2] < 80)
                    dark++;
            }
        }
        return dark;
    }

    private static int CountMagentaPixels(byte[] png, int width, int height)
    {
        var idat = ExtractIdat(png);
        using var input = new MemoryStream(idat);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        var bytes = raw.ToArray();

        var stride = 1 + (width * 4);
        var count = 0;
        for (var y = 0; y < height; y++)
        {
            var rowStart = (y * stride) + 1;
            for (var x = 0; x < width; x++)
            {
                var p = rowStart + (x * 4);
                // Magenta: high R, low G, high B.
                if (bytes[p] > 200 && bytes[p + 1] < 80 && bytes[p + 2] > 200)
                    count++;
            }
        }
        return count;
    }

    private static byte[] ExtractIdat(byte[] png)
    {
        using var output = new MemoryStream();
        var pos = 8; // skip signature
        while (pos + 8 <= png.Length)
        {
            var len = (png[pos] << 24) | (png[pos + 1] << 16) | (png[pos + 2] << 8) | png[pos + 3];
            var type = System.Text.Encoding.ASCII.GetString(png, pos + 4, 4);
            var dataStart = pos + 8;
            if (type == PngConstants.IDAT)
                output.Write(png, dataStart, len);
            pos = dataStart + len + 4; // data + CRC
            if (type == PngConstants.IEND) break;
        }
        return output.ToArray();
    }
}
