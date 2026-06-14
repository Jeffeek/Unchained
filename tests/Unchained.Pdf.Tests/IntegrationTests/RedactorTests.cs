using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class RedactorTests : PdfTestBase
{
    private static readonly Redactor Redactor = new();

    // WithTextContent places "Hello Unchained" at user-space (100, 700), 12pt.

    [Fact]
    public async Task Redact_TextInsideRegion_RemovesTextFromStream()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithTextContent("SecretValue"));
        doc.Pages[1].ExtractText().ShouldContain("SecretValue");

        // Region covering the text origin at (100, 700).
        await Redactor.RedactAsync(
            doc,
            [new RedactionRegion(1, 80, 690, 200, 30)],
            TestContext.Current.CancellationToken
        );

        doc.Pages[1].ExtractText().ShouldNotContain("SecretValue");
    }

    [Fact]
    public async Task Redact_TextOutsideRegion_TextRemains()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithTextContent("KeepMe"));
        // Region far from the text at (100,700).
        await Redactor.RedactAsync(
            doc,
            [new RedactionRegion(1, 0, 0, 50, 50)],
            TestContext.Current.CancellationToken
        );

        doc.Pages[1].ExtractText().ShouldContain("KeepMe");
    }

    [Fact]
    public async Task Redact_RemovalSurvivesSaveReload()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithTextContent("TopSecret"));
        await Redactor.RedactAsync(
            doc,
            [new RedactionRegion(1, 80, 690, 200, 30)],
            TestContext.Current.CancellationToken
        );

        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);
        reloaded.Pages[1].ExtractText().ShouldNotContain("TopSecret");
    }

    [Fact]
    public async Task Redact_PaintsCoverRectangle_ProducesFillOperator()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithTextContent("Cover"));
        await Redactor.RedactAsync(
            doc,
            [new RedactionRegion(1, 80, 690, 200, 30)],
            TestContext.Current.CancellationToken
        );

        // The rebuilt content stream must contain a rectangle fill ('re' + 'f').
        var ops = doc.Pages[1].GetContentOperators();
        ops.ShouldContain(static o => o.Name == "re");
        ops.ShouldContain(static o => o.Name == "f");
    }

    [Fact]
    public async Task Redact_Image_RemovesDoOperator()
    {
        var rgb = new byte[8 * 8 * 3];
        for (var i = 0; i < rgb.Length; i++) rgb[i] = 128;
        await using var doc = await LoadAsync(PdfFixtures.WithImageXObject(8, 8, rgb));

        // Fixture draws the image with cm "(w*10) 0 0 (h*10) 0 0" → unit square maps to
        // [0,0]..[80,80]; centre ≈ (40,40). Redact a region covering it.
        var before = doc.Pages[1].GetContentOperators();
        before.ShouldContain(static o => o.Name == "Do");

        await Redactor.RedactAsync(
            doc,
            [new RedactionRegion(1, 0, 0, 80, 80)],
            TestContext.Current.CancellationToken
        );

        doc.Pages[1].GetContentOperators().ShouldNotContain(static o => o.Name == "Do");
    }

    [Fact]
    public async Task Redact_EmptyRegions_NoOp()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithTextContent("Untouched"));
        await Redactor.RedactAsync(doc, [], TestContext.Current.CancellationToken);
        doc.Pages[1].ExtractText().ShouldContain("Untouched");
    }

    [Fact]
    public Task Redact_PageOutOfRange_Throws() =>
        Should.ThrowAsync<ArgumentOutOfRangeException>(static async () =>
            {
                await using var doc = await LoadAsync(PdfFixtures.WithTextContent("X"));
                await Redactor.RedactAsync(doc, [new RedactionRegion(5, 0, 0, 10, 10)]);
            }
        );
}
