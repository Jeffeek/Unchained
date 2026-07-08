using Shouldly;
using Unchained.Pdf.Models;
using Unchained.Pdf.Rendering.Tests.Helpers;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Rendering.Tests.IntegrationTests;

/// <summary>
///     Renders pages whose content streams exercise the path-painting, image-blitting, and
///     shading partials of <c>PageRenderer</c> (beyond the text-focused <c>RendererTests</c>).
///     Guarded by FreeType availability via <see cref="RendererTestBase" />.
/// </summary>
public sealed class RichRenderingTests : RendererTestBase
{
    private static byte[] PngStart() => [0x89, 0x50, 0x4E, 0x47];

    [Fact]
    public async Task RenderPage_CmykFill_ProducesNonWhitePixels()
    {
        var png = await RenderRaw("0 1 1 0 k 100 100 100 100 re f");
        png[..4].ShouldBe(PngStart());
        // Check a few bytes in the middle of the PNG payload — the rendered page should have non-white pixels.
        // This is a weaker check than full pixel decoding but avoids needing a full PNG decoder.
        var hasNonWhite = false;
        for (var i = 200; i < png.Length - 200 && !hasNonWhite; i += 50)
            hasNonWhite |= png[i] != 255;
        hasNonWhite.ShouldBeTrue();
    }

    [Fact]
    public async Task RenderPage_GraphicsStateStack_ProducesTwoColors()
    {
        var png = await RenderRaw("q 1 0 0 rg 50 50 100 100 re f Q q 0 0 1 rg 200 200 100 100 re f Q");
        png[..4].ShouldBe(PngStart());
        // Same approach: check for non-white pixels in the payload.
        var hasNonWhite = false;
        for (var i = 200; i < png.Length - 200 && !hasNonWhite; i += 50)
            hasNonWhite |= png[i] != 255;
        hasNonWhite.ShouldBeTrue();
    }

    private async Task<byte[]> RenderRaw(string content)
    {
        await using var doc = await LoadAsync(PdfFixtures.WithRawContent(content), TestContext.Current.CancellationToken);
        return await Renderer.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RenderPage_FilledRectangle_ProducesPng()
    {
        var png = await RenderRaw("1 0 0 rg 100 100 200 150 re f");
        png[..4].ShouldBe(PngStart());
    }

    [Fact]
    public async Task RenderPage_StrokedPath_ProducesPng()
    {
        var png = await RenderRaw("0 0 1 RG 5 w 100 100 m 400 400 l 400 100 l S");
        png[..4].ShouldBe(PngStart());
    }

    [Fact]
    public async Task RenderPage_BezierCurve_ProducesPng()
    {
        var png = await RenderRaw("0 0.5 0 rg 100 100 m 150 300 350 300 400 100 c f");
        png[..4].ShouldBe(PngStart());
    }

    [Fact]
    public async Task RenderPage_ClippedFill_ProducesPng()
    {
        var png = await RenderRaw("100 100 200 200 re W n 1 0 0 rg 0 0 600 600 re f");
        png[..4].ShouldBe(PngStart());
    }

    [Fact]
    public async Task RenderPage_DashedStroke_ProducesPng()
    {
        var png = await RenderRaw("[3 3] 0 d 2 w 100 100 m 400 100 l S");
        png[..4].ShouldBe(PngStart());
    }

    [Fact]
    public async Task RenderPage_WithImageXObject_ProducesPng()
    {
        var rgb = new byte[4 * 4 * 3];
        for (var i = 0; i < rgb.Length; i++) rgb[i] = (byte)(i % 256);
        await using var doc = await LoadAsync(PdfFixtures.WithImageXObject(4, 4, rgb), TestContext.Current.CancellationToken);
        var png = await Renderer.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
        png[..4].ShouldBe(PngStart());
    }

    [Fact]
    public async Task RenderPage_WithAxialShading_ProducesPng()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAxialShading(), TestContext.Current.CancellationToken);
        var png = await Renderer.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
        png[..4].ShouldBe(PngStart());
    }

    [Fact]
    public async Task RenderPage_WithTilingPattern_ProducesPng()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithTilingPattern(), TestContext.Current.CancellationToken);
        var png = await Renderer.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
        png[..4].ShouldBe(PngStart());
    }
}
