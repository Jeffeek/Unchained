using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class PageContentEditorTests : PdfTestBase
{
    private static readonly PageContentEditor Editor = new();

    private static ImageContent SolidImage(int width, int height, byte r, byte g, byte b, bool withAlpha = false)
    {
        var rgb = new byte[width * height * 3];
        for (var i = 0; i < width * height; i++)
        {
            rgb[(i * 3) + 0] = r;
            rgb[(i * 3) + 1] = g;
            rgb[(i * 3) + 2] = b;
        }

        byte[]? alpha = null;
        if (!withAlpha)
            return new ImageContent(width, height, rgb, alpha);

        alpha = new byte[width * height];
        Array.Fill(alpha, (byte)128);

        return new ImageContent(width, height, rgb, alpha);
    }

    // ── AddBlankPageAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task AddBlankPageAsync_Append_IncreasesPageCount()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(2), TestContext.Current.CancellationToken);
        var pageNumber = await Editor.AddBlankPageAsync(doc, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(3);
        pageNumber.ShouldBe(3);
    }

    [Fact]
    public async Task AddBlankPageAsync_Insert_AtPosition()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(3), TestContext.Current.CancellationToken);
        var pageNumber = await Editor.AddBlankPageAsync(doc, atPageNumber: 2, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(4);
        pageNumber.ShouldBe(2);
    }

    [Fact]
    public async Task AddBlankPageAsync_CustomSize_SetsMediaBox()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.AddBlankPageAsync(doc, 400, 300, ct: TestContext.Current.CancellationToken);
        doc.Pages[2].Width.ShouldBe(400, 0.01);
        doc.Pages[2].Height.ShouldBe(300, 0.01);
    }

    [Fact]
    public async Task AddBlankPageAsync_OutOfRange_Throws()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => Editor.AddBlankPageAsync(doc, atPageNumber: 5, ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddBlankPageAsync_RoundTrip_Parseable()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.AddBlankPageAsync(doc, ct: TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, cancellationToken: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(2);
    }

    // ── DrawTextAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DrawTextAsync_TextIsExtractable()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.DrawTextAsync(doc, 1, "HelloWorld", 100, 700, ct: TestContext.Current.CancellationToken);
        doc.Pages[1].ExtractText().ShouldContain("HelloWorld");
    }

    [Fact]
    public async Task DrawTextAsync_AddsFontResource()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.DrawTextAsync(doc, 1, "Test", 50, 500, ct: TestContext.Current.CancellationToken);
        doc.Pages[1].GetFontNameMap().Values.ShouldContain("Helvetica");
    }

    [Fact]
    public async Task DrawTextAsync_TwoDraws_BothExtractable()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.DrawTextAsync(doc, 1, "First", 100, 700, ct: TestContext.Current.CancellationToken);
        await Editor.DrawTextAsync(doc, 1, "Second", 100, 650, new TextDrawOptions("Times-Roman", 18), TestContext.Current.CancellationToken);
        var text = doc.Pages[1].ExtractText();
        text.ShouldContain("First");
        text.ShouldContain("Second");
    }

    [Fact]
    public async Task DrawTextAsync_RoundTrip_TextPersists()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.DrawTextAsync(doc, 1, "Persisted", 72, 720, new TextDrawOptions(Color: (1f, 0f, 0f)), TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, cancellationToken: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, TestContext.Current.CancellationToken);
        reloaded.Pages[1].ExtractText().ShouldContain("Persisted");
    }

    [Fact]
    public async Task DrawTextAsync_OnBlankPage_Works()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var page = await Editor.AddBlankPageAsync(doc, ct: TestContext.Current.CancellationToken);
        await Editor.DrawTextAsync(doc, page, "OnBlank", 100, 400, ct: TestContext.Current.CancellationToken);
        doc.Pages[page].ExtractText().ShouldContain("OnBlank");
    }

    // ── DrawImageAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task DrawImageAsync_AddsImageXObject()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.DrawImageAsync(doc, 1, SolidImage(4, 3, 255, 0, 0), 100, 500, 200, 150, TestContext.Current.CancellationToken);

        var images = doc.Pages[1].GetImageXObjects();
        images.Count.ShouldBe(1);
        var image = images.Values.Single();
        image.Width.ShouldBe(4);
        image.Height.ShouldBe(3);
    }

    [Fact]
    public async Task DrawImageAsync_PixelDataRoundTrips()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.DrawImageAsync(doc, 1, SolidImage(2, 2, 10, 20, 30), 0, 0, 50, 50, TestContext.Current.CancellationToken);

        var image = doc.Pages[1].GetImageXObjects().Values.Single();
        image.RgbData[0].ShouldBe((byte)10);
        image.RgbData[1].ShouldBe((byte)20);
        image.RgbData[2].ShouldBe((byte)30);
    }

    [Fact]
    public async Task DrawImageAsync_WithAlpha_Succeeds()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.DrawImageAsync(doc, 1, SolidImage(4, 4, 0, 128, 255, withAlpha: true), 10, 10, 100, 100, TestContext.Current.CancellationToken);
        doc.Pages[1].GetImageXObjects().Count.ShouldBe(1);
    }

    [Fact]
    public async Task DrawImageAsync_RoundTrip_Parseable()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.DrawImageAsync(doc, 1, SolidImage(8, 8, 200, 100, 50), 20, 20, 80, 80, TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, cancellationToken: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, TestContext.Current.CancellationToken);
        reloaded.Pages[1].GetImageXObjects().Count.ShouldBe(1);
    }

    [Fact]
    public async Task DrawImageAsync_BadRgbLength_Throws()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var badImage = new ImageContent(4, 4, new byte[10]);
        await Should.ThrowAsync<ArgumentException>(() => Editor.DrawImageAsync(doc, 1, badImage, 0, 0, 40, 40, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DrawImageAsync_BadAlphaLength_Throws()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var badImage = new ImageContent(2, 2, new byte[12], new byte[3]);
        await Should.ThrowAsync<ArgumentException>(() => Editor.DrawImageAsync(doc, 1, badImage, 0, 0, 40, 40, TestContext.Current.CancellationToken));
    }
}
