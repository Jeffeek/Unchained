using Shouldly;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     Renders pages whose content streams exercise the path-painting, image-blitting, and
///     shading partials of <c>PageRenderer</c> (beyond the text-focused <c>RendererTests</c>).
///     Guarded by FreeType availability via <see cref="RendererTestBase" />.
/// </summary>
public sealed class RichRenderingTests : RendererTestBase
{
    private static byte[] PngStart() => [0x89, 0x50, 0x4E, 0x47];

    private async Task<byte[]> RenderRaw(string content)
    {
        await using var doc = await LoadAsync(PdfFixtures.WithRawContent(content), TestContext.Current.CancellationToken);
        return await Renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RenderPage_FilledRectangle_ProducesPng()
    {
        SkipIfNoFreeType();
        var png = await RenderRaw("1 0 0 rg 100 100 200 150 re f");
        png[..4].ShouldBe(PngStart());
    }

    [Fact]
    public async Task RenderPage_StrokedPath_ProducesPng()
    {
        SkipIfNoFreeType();
        var png = await RenderRaw("0 0 1 RG 5 w 100 100 m 400 400 l 400 100 l S");
        png[..4].ShouldBe(PngStart());
    }

    [Fact]
    public async Task RenderPage_BezierCurve_ProducesPng()
    {
        SkipIfNoFreeType();
        var png = await RenderRaw("0 0.5 0 rg 100 100 m 150 300 350 300 400 100 c f");
        png[..4].ShouldBe(PngStart());
    }

    [Fact]
    public async Task RenderPage_ClippedFill_ProducesPng()
    {
        SkipIfNoFreeType();
        var png = await RenderRaw("100 100 200 200 re W n 1 0 0 rg 0 0 600 600 re f");
        png[..4].ShouldBe(PngStart());
    }

    [Fact]
    public async Task RenderPage_DashedStroke_ProducesPng()
    {
        SkipIfNoFreeType();
        var png = await RenderRaw("[3 3] 0 d 2 w 100 100 m 400 100 l S");
        png[..4].ShouldBe(PngStart());
    }

    [Fact]
    public async Task RenderPage_CmykFill_ProducesPng()
    {
        SkipIfNoFreeType();
        var png = await RenderRaw("0 1 1 0 k 100 100 100 100 re f");
        png[..4].ShouldBe(PngStart());
    }

    [Fact]
    public async Task RenderPage_GraphicsStateStack_ProducesPng()
    {
        SkipIfNoFreeType();
        var png = await RenderRaw("q 1 0 0 rg 50 50 100 100 re f Q q 0 0 1 rg 200 200 100 100 re f Q");
        png[..4].ShouldBe(PngStart());
    }

    [Fact]
    public async Task RenderPage_WithImageXObject_ProducesPng()
    {
        SkipIfNoFreeType();
        var rgb = new byte[4 * 4 * 3];
        for (var i = 0; i < rgb.Length; i++) rgb[i] = (byte)(i % 256);
        await using var doc = await LoadAsync(PdfFixtures.WithImageXObject(4, 4, rgb), TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
        png[..4].ShouldBe(PngStart());
    }

    [Fact]
    public async Task RenderPage_WithAxialShading_ProducesPng()
    {
        SkipIfNoFreeType();
        await using var doc = await LoadAsync(PdfFixtures.WithAxialShading(), TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
        png[..4].ShouldBe(PngStart());
    }

    [Fact]
    public async Task RenderPage_WithTilingPattern_ProducesPng()
    {
        SkipIfNoFreeType();
        await using var doc = await LoadAsync(PdfFixtures.WithTilingPattern(), TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
        png[..4].ShouldBe(PngStart());
    }
}
