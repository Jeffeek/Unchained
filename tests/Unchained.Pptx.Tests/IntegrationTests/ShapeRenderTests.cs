using System.IO.Compression;
using System.Text;
using Shouldly;
using Unchained.Drawing.Constants;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Rendering.Engine;
using Unchained.Pptx.Rendering.Models;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

// Verifies the rasterizer now draws groups and tables (previously only AutoShape + Picture).
public sealed class ShapeRenderTests : PptxTestBase
{
    [Fact]
    public async Task Table_RendersCellFillsAndGridLines()
    {
        var doc = PptxFixtures.WithSlides(1);
        var table = doc.Slides[0].Shapes.AddTable(
            Emu.FromInches(1),
            Emu.FromInches(1),
            [Emu.FromInches(2), Emu.FromInches(2)],
            [Emu.FromInches(1), Emu.FromInches(1)]);
        table[0, 0].Fill.SetSolid(ColorSpec.FromRgb(0, 112, 192));

        var image = await SlideRenderer.RenderAsync(
            doc.Slides[0],
            doc.SlideSize,
            new RenderOptions { WidthPx = 640, HeightPx = 360 });

        // The blue cell fill must appear (non-background ink present).
        CountColoredPixels(image.Data.ToArray(), 640, 360).ShouldBeGreaterThan(100);
    }

    [Fact]
    public async Task Group_RendersChildShapes()
    {
        var doc = PptxFixtures.WithSlides(1);
        var group = doc.Slides[0].Shapes.AddGroup();
        var child = new AutoShape
        {
            ShapeType = AutoShapeType.Rectangle,
            X = Emu.FromInches(1),
            Y = Emu.FromInches(1),
            Width = Emu.FromInches(3),
            Height = Emu.FromInches(2)
        };
        child.Fill.SetSolid(ColorSpec.FromRgb(0, 200, 0));
        group.Children.AddParsed(child);

        var image = await SlideRenderer.RenderAsync(
            doc.Slides[0],
            doc.SlideSize,
            new RenderOptions { WidthPx = 640, HeightPx = 360 });

        CountColoredPixels(image.Data.ToArray(), 640, 360).ShouldBeGreaterThan(100);
    }

    private static int CountColoredPixels(byte[] png, int width, int height)
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
                // Non-white, non-near-white pixel.
                if (bytes[p] < 240 || bytes[p + 1] < 240 || bytes[p + 2] < 240)
                    count++;
            }
        }

        return count;
    }

    private static byte[] ExtractIdat(byte[] png)
    {
        using var output = new MemoryStream();
        var pos = 8;
        while (pos + 8 <= png.Length)
        {
            var len = (png[pos] << 24) | (png[pos + 1] << 16) | (png[pos + 2] << 8) | png[pos + 3];
            var type = Encoding.ASCII.GetString(png, pos + 4, 4);
            var dataStart = pos + 8;
            if (type == PngConstants.IDAT) output.Write(png, dataStart, len);
            pos = dataStart + len + 4;
            if (type == PngConstants.IEND) break;
        }

        return output.ToArray();
    }
}
