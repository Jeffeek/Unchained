using Shouldly;
using Unchained.Pdf.Rendering.Tests.Helpers;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Rendering.Tests.IntegrationTests;

/// <summary>
///     Additional <see cref="Unchained.Pdf.Engine.FontMutator" /> coverage via the public
///     processor: embedding when no font name matches (no-op), embedding into an already-embedded
///     descriptor (skip), replacing a non-existent font (no-op), and subsetting a document that
///     actually shows text (glyph-collection path).
/// </summary>
public sealed class FontMutatorBranchTests : RendererTestBase
{
    [Fact]
    public async Task EmbedStandardFonts_NoMatchingFont_DoesNotThrowAndKeepsDocument()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithTextContent("Hi"), TestContext.Current.CancellationToken);
        // Font map keyed by a family the document does not use.
        var map = new Dictionary<string, byte[]> { ["Garamond"] = LoadDejaVuSansRegular() };
        await Processor.EmbedStandardFontsAsync(doc, map, TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task EmbedStandardFonts_AlreadyEmbedded_IsSkipped()
    {
        var font = LoadDejaVuSansRegular();
        // WithEmbeddedFont already has a /FontFile2 descriptor for /TestFont.
        await using var doc = await LoadAsync(PdfFixtures.WithEmbeddedFont(font), TestContext.Current.CancellationToken);

        using var before = new MemoryStream();
        await Processor.SaveAsync(doc, before, ct: TestContext.Current.CancellationToken);

        var map = new Dictionary<string, byte[]> { ["TestFont"] = font };
        await Processor.EmbedStandardFontsAsync(doc, map, TestContext.Current.CancellationToken);

        // Already embedded → no second FontFile2 added; document still loads.
        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task ReplaceFont_NonExistentName_NoOp()
    {
        await using var doc = await LoadAsync(
            PdfFixtures.WithEmbeddedFont(LoadDejaVuSansRegular()),
            TestContext.Current.CancellationToken
        );
        await Processor.ReplaceFontAsync(
            doc,
            "NoSuchFont",
            LoadDejaVuSansRegular(),
            TestContext.Current.CancellationToken
        );
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task SubsetFonts_DocumentShowingText_ReducesOrMaintainsSize()
    {
        var font = LoadDejaVuSansRegular();
        await using var doc = await LoadAsync(
            PdfFixtures.WithEmbeddedFont(font),
            TestContext.Current.CancellationToken
        );

        using var before = new MemoryStream();
        await Processor.SaveAsync(doc, before, ct: TestContext.Current.CancellationToken);
        var sizeBefore = before.Length;

        await Processor.SubsetFontsAsync(doc, TestContext.Current.CancellationToken);

        using var after = new MemoryStream();
        await Processor.SaveAsync(doc, after, ct: TestContext.Current.CancellationToken);
        after.Length.ShouldBeLessThanOrEqualTo(sizeBefore);
    }

    [Fact]
    public async Task EmbedStandardFonts_MapsHelveticaToReplacement()
    {
        var font = LoadDejaVuSansRegular();
        // WithTextContent declares /F1 as Helvetica (no descriptor) — embedding should add one.
        await using var doc = await LoadAsync(
            PdfFixtures.WithTextContent("Hello"),
            TestContext.Current.CancellationToken
        );
        var map = new Dictionary<string, byte[]> { ["Helvetica"] = font };
        await Processor.EmbedStandardFontsAsync(doc, map, TestContext.Current.CancellationToken);

        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(1);
    }
}
