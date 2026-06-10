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

    // ── Tiling patterns ────────────────────────────────────────────────────────

    [Fact]
    public void GetTilingPatterns_ParsesPatternCell()
    {
        var bytes = PdfFixtures.WithTilingPattern();
        using var doc = Processor.LoadAsync(new MemoryStream(bytes)).GetAwaiter().GetResult();
        var patterns = doc.Pages[1].GetTilingPatterns();
        patterns.ContainsKey("P1").ShouldBeTrue();
        var p = patterns["P1"];
        p.PaintType.ShouldBe(1);
        p.XStep.ShouldBe(10, 0.01);
        p.Operators.ShouldContain(o => o.Name == "re"); // cell draws a rectangle
    }

    [Fact]
    public async Task TilingPattern_RendersRedInk()
    {
        SkipIfNoFreeType();

        await using var doc = await LoadAsync(PdfFixtures.WithTilingPattern(), ct: TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(Dpi: 96), ct: TestContext.Current.CancellationToken);
        var g = DecodeGray(png);

        // The red-square tiling should make the page substantially non-white (not grey-160,
        // not white). Count dark-ish pixels across the image.
        var h = g.GetLength(0); var w = g.GetLength(1);
        var nonWhite = 0;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                if (g[y, x] < 200) nonWhite++;
        nonWhite.ShouldBeGreaterThan(w * h / 10); // pattern covers a meaningful fraction
    }

    [Fact]
    public async Task TriangularClip_ExactPolygon_CornerPixelsAreWhite()
    {
        // Verifies that W (nonzero winding clip) clips to the actual polygon shape, not just
        // the axis-aligned bounding box. The fixture clips to the lower-right triangle of a
        // 100×100 page, then fills the whole page black. Exact clipping must leave the
        // top-left corner of the page white; bbox clipping would paint it black.
        SkipIfNoFreeType();

        await using var doc = await LoadAsync(
            PdfFixtures.WithTriangularClip(),
            ct: TestContext.Current.CancellationToken);

        var png = await Renderer!.RenderPageAsync(
            doc.Pages[1],
            new RenderOptions(Dpi: 72),
            ct: TestContext.Current.CancellationToken);

        var g = DecodeGray(png);
        var h = g.GetLength(0);
        var w = g.GetLength(1);

        // Top-left corner region must be white (outside the triangle).
        // Sample a 10×10 block near the top-left — all pixels should be ≥ 240 (near-white).
        var cornerSize = Math.Max(2, Math.Min(10, w / 10));
        for (var y = 0; y < cornerSize; y++)
        for (var x = 0; x < cornerSize; x++)
            g[y, x].ShouldBeGreaterThanOrEqualTo(
                230,
                $"pixel ({x},{y}) should be white (outside triangle clip) but was {g[y, x]}");

        // Bottom-right corner region must be black (inside the triangle).
        for (var y = h - cornerSize; y < h; y++)
        for (var x = w - cornerSize; x < w; x++)
            g[y, x].ShouldBeLessThan(
                50,
                $"pixel ({x},{y}) should be black (inside triangle clip) but was {g[y, x]}");
    }

    [
        Theory,
        InlineData("Multiply"),
        InlineData("Screen"),
        InlineData("Difference"),
        InlineData("Darken"),
        InlineData("Lighten"),
        InlineData("Overlay"),
        InlineData("ColorDodge"),
        InlineData("ColorBurn"),
        InlineData("HardLight"),
        InlineData("SoftLight"),
        InlineData("Exclusion")
    ]
    public async Task BlendMode_DoesNotCrash_AndProducesNonWhiteResult(string blendMode)
    {
        // Smoke test: every blend mode must (a) not throw and (b) produce some non-white
        // pixels (the grey rect was composited over the white backdrop).
        SkipIfNoFreeType();

        await using var doc = await LoadAsync(
            PdfFixtures.WithBlendMode(blendMode),
            ct: TestContext.Current.CancellationToken);

        var png = await Renderer!.RenderPageAsync(
            doc.Pages[1],
            new RenderOptions(Dpi: 72),
            ct: TestContext.Current.CancellationToken);

        var g = DecodeGray(png);
        var h = g.GetLength(0);
        var w = g.GetLength(1);

        var nonWhite = 0;
        var minVal = 255;
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            if (g[y, x] < minVal) minVal = g[y, x];
            if (g[y, x] < 253) nonWhite++;
        }

        nonWhite.ShouldBeGreaterThan(0, $"BlendMode {blendMode}: all pixels ≥ 253 (min={minVal}) — blend had no effect");
    }

    [Fact]
    public async Task BlendMode_Multiply_DarkensResult()
    {
        // Multiply(grey, white) = grey — result should be ~grey (~128), not white (255).
        SkipIfNoFreeType();

        await using var doc = await LoadAsync(
            PdfFixtures.WithBlendMode("Multiply"),
            ct: TestContext.Current.CancellationToken);

        var png = await Renderer!.RenderPageAsync(
            doc.Pages[1],
            new RenderOptions(Dpi: 72),
            ct: TestContext.Current.CancellationToken);

        var g = DecodeGray(png);
        var h = g.GetLength(0); var w = g.GetLength(1);
        var midMean = RowMean(g, h / 2);

        // Multiply(0.4, 0.5) = 0.2 → ~51. Should be darker than both inputs (~102, ~127).
        midMean.ShouldBeLessThan(90, $"Multiply blend should be darker than backdrop but mean={midMean:F1}");
        midMean.ShouldBeGreaterThan(10, $"Multiply blend result is too dark: mean={midMean:F1}");
    }

    [Fact]
    public async Task BlendMode_Screen_LightensResult()
    {
        // Screen(grey, white) = white + grey - grey*white ≈ white → near-white result.
        SkipIfNoFreeType();

        await using var doc = await LoadAsync(
            PdfFixtures.WithBlendMode("Screen"),
            ct: TestContext.Current.CancellationToken);

        var png = await Renderer!.RenderPageAsync(
            doc.Pages[1],
            new RenderOptions(Dpi: 72),
            ct: TestContext.Current.CancellationToken);

        var g = DecodeGray(png);
        var h = g.GetLength(0);
        var midMean = RowMean(g, h / 2);

        // Screen(0.4, 0.5) = 0.7 → ~178. Should be lighter than backdrop (~102).
        midMean.ShouldBeGreaterThan(150, $"Screen blend should lighten the result but mean={midMean:F1}");
    }

    [Fact]
    public async Task SoftMask_Alpha_MasksBlackRectWithCentreSquare()
    {
        // The fixture has a white-filled 50×50 centre square as the /SMask form.
        // After masking: pixels inside the square should be black (mask=opaque),
        // pixels in the page corners should remain white (mask=transparent).
        SkipIfNoFreeType();

        await using var doc = await LoadAsync(
            PdfFixtures.WithSoftMask(),
            ct: TestContext.Current.CancellationToken);

        var png = await Renderer!.RenderPageAsync(
            doc.Pages[1],
            new RenderOptions(Dpi: 72),
            ct: TestContext.Current.CancellationToken);

        var g = DecodeGray(png);
        var h = g.GetLength(0);
        var w = g.GetLength(1);

        // Centre region (inside the 50×50 mask square at 25–75% of page) should be dark.
        var centreX = w / 2;
        var centreY = h / 2;
        g[centreY, centreX].ShouldBeLessThan(
            50,
            $"Centre pixel should be black (inside soft mask) but was {g[centreY, centreX]}");

        // Corner pixels (outside the mask square) should be white.
        g[0, 0].ShouldBeGreaterThanOrEqualTo(
            230,
            $"Top-left corner should be white (outside soft mask) but was {g[0, 0]}");
        g[0, w - 1].ShouldBeGreaterThanOrEqualTo(
            230,
            $"Top-right corner should be white (outside soft mask) but was {g[0, w - 1]}");
    }
}
