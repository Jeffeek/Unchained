using System.IO.Compression;
using Shouldly;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class ShadingRenderingTests : RendererTestBase
{
    // Decodes a PdfPngEncoder PNG (RGBA, filter=None) into a grayscale [h][w] array.
    private static int[,] DecodeGray(byte[] png)
    {
        var width = (int)ReadU32(png, 16);
        var height = (int)ReadU32(png, 20);
        var idatLen = (int)ReadU32(png, 33);
        var idat = png.AsSpan(33 + 8, idatLen).ToArray();
        using var comp = new MemoryStream(idat);
        using var dec = new MemoryStream();
        using (var z = new ZLibStream(comp, CompressionMode.Decompress)) z.CopyTo(dec);
        var raw = dec.ToArray();
        var stride = 1 + (width * 4);
        var gray = new int[height, width];
        for (var y = 0; y < height; y++)
        {
            var row = y * stride + 1;
            for (var x = 0; x < width; x++)
            {
                var r = raw[row + (x * 4)];
                var g = raw[row + (x * 4) + 1];
                var b = raw[row + (x * 4) + 2];
                gray[y, x] = (r + g + b) / 3;
            }
        }
        return gray;
    }

    private static uint ReadU32(byte[] d, int o) =>
        ((uint)d[o] << 24) | ((uint)d[o + 1] << 16) | ((uint)d[o + 2] << 8) | d[o + 3];

    private static double RowMean(int[,] g, int y)
    {
        var w = g.GetLength(1);
        long sum = 0;
        for (var x = 0; x < w; x++) sum += g[y, x];
        return (double)sum / w;
    }

    [Fact]
    public async Task AxialShading_RendersVerticalGradient()
    {
        SkipIfNoFreeType();

        await using var doc = await LoadAsync(PdfFixtures.WithAxialShading(), ct: TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(Dpi: 96), ct: TestContext.Current.CancellationToken);
        var g = DecodeGray(png);

        var h = g.GetLength(0);
        var topMean = RowMean(g, 2);            // near page top (Coords y=100 → white)
        var midMean = RowMean(g, h / 2);
        var bottomMean = RowMean(g, h - 3);     // near page bottom (Coords y=0 → black)

        // Device Y is flipped: PDF y=100 (white) is at the top of the image.
        topMean.ShouldBeGreaterThan(midMean + 20, $"top={topMean}, mid={midMean}");
        midMean.ShouldBeGreaterThan(bottomMean + 20, $"mid={midMean}, bottom={bottomMean}");
    }

    [Fact]
    public async Task AxialShading_SpansFullToneRange()
    {
        SkipIfNoFreeType();

        await using var doc = await LoadAsync(PdfFixtures.WithAxialShading(), ct: TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(Dpi: 96), ct: TestContext.Current.CancellationToken);
        var g = DecodeGray(png);
        var h = g.GetLength(0);

        RowMean(g, 2).ShouldBeGreaterThan(200);       // near-white at top
        RowMean(g, h - 3).ShouldBeLessThan(60);       // near-black at bottom
    }

    [Fact]
    public void GetShadings_ParsesAxialShadingWithRamp()
    {
        // Verify the adapter exposes the shading with a populated colour ramp.
        var bytes = PdfFixtures.WithAxialShading();
        using var doc = Processor.LoadAsync(new MemoryStream(bytes)).GetAwaiter().GetResult();
        var shadings = doc.Pages[1].GetShadings();
        shadings.ContainsKey("Sh1").ShouldBeTrue();
        var sh = shadings["Sh1"];
        sh.ShadingType.ShouldBe(2);
        sh.ColorAt(0.0).R.ShouldBeLessThan((byte)30);    // black end
        sh.ColorAt(1.0).R.ShouldBeGreaterThan((byte)225); // white end
    }
}
