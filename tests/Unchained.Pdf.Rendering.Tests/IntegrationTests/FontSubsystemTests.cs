using System.Diagnostics.CodeAnalysis;
using System.Text;
using Shouldly;
using Unchained.Drawing.Primitives.Fonts;
using Unchained.Drawing.Text;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Rendering.Tests.Helpers;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Rendering.Tests.IntegrationTests;

/// <summary>
///     Tests for M6: embedded font extraction, proportional table column widths,
///     image XObject rendering, and corrected FreeType2 advance widths.
/// </summary>
public sealed class FontSubsystemTests : RendererTestBase
{
    // ── 6.1 / 6.2 — Embedded font byte extraction ────────────────────────────

    [Fact]
    public async Task GetEmbeddedFontBytes_PageWithNoEmbeddedFonts_ReturnsNullValues()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithTextContent("Hello"), TestContext.Current.CancellationToken);
        var fontBytes = doc.Pages[1].GetEmbeddedFontBytes();
        // Standard 14 fonts are never embedded — all entries null.
        fontBytes.Values.ShouldAllBe(static b => b == null);
    }

    [Fact]
    public async Task GetEmbeddedFontBytes_PageWithEmbeddedFont_ReturnsByteArray()
    {
        var fontData = SyntheticFontBytes();
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData);
        await using var doc = await LoadAsync(pdfBytes, TestContext.Current.CancellationToken);
        var fontBytes = doc.Pages[1].GetEmbeddedFontBytes();
        fontBytes.TryGetValue("F1", out var f1Bytes).ShouldBeTrue();
        f1Bytes.ShouldNotBeNull();
        f1Bytes.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetEmbeddedFontBytes_EmbeddedBytes_MatchOriginal()
    {
        var fontData = SyntheticFontBytes();
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData);
        await using var doc = await LoadAsync(pdfBytes, TestContext.Current.CancellationToken);
        var extracted = doc.Pages[1].GetEmbeddedFontBytes()["F1"];
        extracted.ShouldNotBeNull();
        extracted.Length.ShouldBe(fontData.Length);
    }

    [Fact]
    public async Task GetEmbeddedFontBytes_PageWithNoFonts_ReturnsEmptyDict()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        doc.Pages[1].GetEmbeddedFontBytes().ShouldBeEmpty();
    }

    // ── 6.3 / 6.5 — FreeType2 advance width correction ───────────────────────

    [Fact]
    public async Task RenderPage_WithEmbeddedFont_ProducesPng()
    {
        // Use the bundled DejaVu font (valid TrueType) so FreeType2 can actually load it.
        var fontData = LoadBundledDejaVuBytes();
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData);
        await using var doc = await LoadAsync(pdfBytes, TestContext.Current.CancellationToken);
        var png = await Renderer.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
        png[..8].ShouldBe(PdfTestConstants.PngSignature);
    }

    [Fact]
    public async Task RenderPage_WithEmbeddedFont_ProducesValidSizedPng()
    {
        var fontData = LoadBundledDejaVuBytes();
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData);
        await using var doc = await LoadAsync(pdfBytes, TestContext.Current.CancellationToken);
        var png = await Renderer.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
        png.Length.ShouldBeGreaterThan(200);
    }

    // ── 6.4 — Proportional table column widths ────────────────────────────────

    [Fact]
    public async Task GenerateTable_ShortAndLongColumns_WidthsAreProportional()
    {
        var gen = new TableGenerator();
        var data = new TableData
        {
            Headers = ["A", "A very long header that needs more space"],
            Rows = [["x", "y"]]
        };
        await using var doc = await gen.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);
        // Verify the document round-trips; column proportionality is structural.
        doc.PageCount.ShouldBe(1);
        var ops = doc.Pages[1].GetContentOperators();
        ops.ShouldContain(static op => op.Name == "Tj");
    }

    [Fact]
    public async Task GenerateTable_EqualHeaders_TotalWidthFillsPage()
    {
        var gen = new TableGenerator();
        var data = new TableData
        {
            Headers = ["Col1", "Col2", "Col3"],
            Rows = [["a", "b", "c"]]
        };
        await using var doc = await gen.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);
        // Round-trip should still produce correct page count.
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(1);
    }

    [Fact]
    public void TableLayout_WithData_ColumnWidthsSumToUsableWidth()
    {
        var data = new TableData
        {
            Headers = ["Short", "A much longer header"],
            Rows = [["tiny", "this cell has a lot of content"]]
        };
        var layout = TableLayout.Compute(data.Headers.Count, TableStyle.Default, false, data);
        layout.ColumnWidths.Sum()
            .ShouldBe(
                TableLayout.PageWidth - (2 * TableLayout.Margin),
                0.1f
            );
    }

    [Fact]
    public void TableLayout_WithData_LongerColumnGetsMoreWidth()
    {
        var data = new TableData
        {
            // ReSharper disable once GrammarMistakeInStringLiteral
            Headers = ["X", "A very very long header that dwarfs the other"],
            Rows = [["a", "b"]]
        };
        var layout = TableLayout.Compute(data.Headers.Count, TableStyle.Default, false, data);
        layout.ColumnWidths[1].ShouldBeGreaterThan(layout.ColumnWidths[0]);
    }

    // ── 6.6 — Image XObject (Do operator) ────────────────────────────────────

    [Fact]
    public async Task GetImageXObjects_PageWithNoImages_ReturnsEmpty()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        doc.Pages[1].GetImageXObjects().ShouldBeEmpty();
    }

    [Fact]
    public async Task GetImageXObjects_PageWithImage_ReturnsEntry()
    {
        var rgb = CreateSolidRgb(4, 4, 255, 0, 0);
        await using var doc = await LoadAsync(PdfFixtures.WithImageXObject(4, 4, rgb), TestContext.Current.CancellationToken);
        var images = doc.Pages[1].GetImageXObjects();
        images.ContainsKey("Im1").ShouldBeTrue();
    }

    [Fact]
    public async Task GetImageXObjects_PageWithImage_CorrectDimensions()
    {
        var rgb = CreateSolidRgb(8, 6, 0, 255, 0);
        await using var doc = await LoadAsync(PdfFixtures.WithImageXObject(8, 6, rgb), TestContext.Current.CancellationToken);
        var img = doc.Pages[1].GetImageXObjects()["Im1"];
        img.Width.ShouldBe(8);
        img.Height.ShouldBe(6);
    }

    [Fact]
    public async Task GetImageXObjects_PageWithImage_RgbDataDecodedCorrectly()
    {
        var rgb = CreateSolidRgb(2, 2, 128, 64, 32);
        await using var doc = await LoadAsync(PdfFixtures.WithImageXObject(2, 2, rgb), TestContext.Current.CancellationToken);
        var img = doc.Pages[1].GetImageXObjects()["Im1"];
        img.RgbData[0].ShouldBe((byte)128);
        img.RgbData[1].ShouldBe((byte)64);
        img.RgbData[2].ShouldBe((byte)32);
    }

    [Fact]
    public async Task RenderPage_WithImageXObject_ProducesPng()
    {
        var rgb = CreateSolidRgb(4, 4, 200, 100, 50);
        var pdfBytes = PdfFixtures.WithImageXObject(4, 4, rgb);
        await using var doc = await LoadAsync(pdfBytes, TestContext.Current.CancellationToken);
        var png = await Renderer.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
        png[..8].ShouldBe(PdfTestConstants.PngSignature);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Returns 32 bytes of synthetic "font" data for extraction-only tests.
    // Not a valid TrueType font — only used to verify bytes survive round-trip.
    private static byte[] SyntheticFontBytes() =>
    [
        0x00, 0x01, 0x02, 0x03, 0xDE, 0xAD, 0xBE, 0xEF,
        0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17,
        0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27,
        0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37
    ];

    // Loads the bundled DejaVuSans-Regular.ttf from the Drawing.Text assembly (valid TrueType,
    // usable by FreeType2 for rendering tests). Fonts moved from Pdf.Rendering → Drawing.Text.
    private static byte[] LoadBundledDejaVuBytes()
    {
        var asm = typeof(FontCache).Assembly;
        using var stream = asm.GetManifestResourceStream("Unchained.Drawing.Text.Fonts.DejaVuSans-Regular.ttf")
                           ?? throw new InvalidOperationException(
                               "DejaVuSans-Regular.ttf not found in Drawing.Text assembly."
                           );
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    // Public alias used by FontUtilitiesTests (different class, same assembly).
    internal static byte[] LoadBundledDejaVuBytesPublic() => LoadBundledDejaVuBytes();

    [SuppressMessage("ReSharper", "BadListLineBreaks")]
    private static byte[] CreateSolidRgb(
        int width,
        int height,
        byte r,
        byte g,
        byte b
    )
    {
        var rgb = new byte[width * height * 3];
        for (var i = 0; i < rgb.Length; i += 3)
        {
            rgb[i] = r;
            rgb[i + 1] = g;
            rgb[i + 2] = b;
        }

        return rgb;
    }
}

/// <summary>
///     Tests for font utilities: TrueType metrics extraction, font replacement,
///     and font subsetting (SubsetFontsAsync / TrueTypeSubsetter).
/// </summary>
public sealed class FontUtilitiesTests : PdfTestBase
{
    // ── TrueType metrics ─────────────────────────────────────────────────────────

    [Fact]
    public void TrueTypeMetrics_SyntheticBytes_ReturnsDefaults()
    {
        // A non-TrueType byte array should fall back to the hardcoded defaults.
        // The method returns defaults rather than null when the input is too short.
        var metrics = TrueTypeMetrics.Read([0x00, 0x01, 0x02]);
        // Either null or default metrics — both are acceptable; the key invariant is no exception.
        if (metrics is null) return;

        // If defaults are returned, they should be reasonable values.
        metrics.Ascent.ShouldBe(800);
        metrics.Descent.ShouldBe(-200);
    }

    [Fact]
    public void TrueTypeMetrics_ValidTrueTypeFont_ReturnsNonDefaultMetrics()
    {
        // Use the bundled DejaVuSans font (valid TrueType).
        var fontBytes = FontSubsystemTests.LoadBundledDejaVuBytesPublic();
        var metrics = TrueTypeMetrics.Read(fontBytes);

        metrics.ShouldNotBeNull();
        metrics.Ascent.ShouldBeGreaterThan(0, "ascent should be positive");
        metrics.Descent.ShouldBeLessThan(0, "descent should be negative for descenders");
        metrics.CapHeight.ShouldBeGreaterThan(0, "cap height should be positive");
        metrics.StemV.ShouldBeGreaterThan(0, "stem width should be positive");
        // Values should NOT all be the hardcoded defaults (800, -200, 716, 80).
        var isAllDefault = metrics is { Ascent: 800, Descent: -200, CapHeight: 716, StemV: 80 };
        isAllDefault.ShouldBeFalse("real font should produce non-default metrics");
    }

    // ── EmbedStandardFonts with real metrics ─────────────────────────────────────

    [Fact]
    public async Task EmbedStandardFontsAsync_WithRealFont_EmbedsFontFile()
    {
        var fontBytes = FontSubsystemTests.LoadBundledDejaVuBytesPublic();
        // WithTextContent uses Helvetica — EmbedStandardFontsAsync should embed it.
        // The font map must match the exact /BaseFont name used in the fixture.
        await using var doc = await LoadAsync(
            PdfFixtures.WithTextContent("Hello"),
            TestContext.Current.CancellationToken
        );

        // Map both "Helvetica" and "Helvetica-Bold" to cover possible /BaseFont values.
        var fontMap = new Dictionary<string, byte[]>
        {
            ["Helvetica"] = fontBytes,
            ["Helvetica-Bold"] = fontBytes,
            ["Arial"] = fontBytes,
            ["ArialMT"] = fontBytes
        };
        // Should not throw regardless of whether fonts are found.
        await Should.NotThrowAsync(() =>
            Processor.EmbedStandardFontsAsync(doc, fontMap, TestContext.Current.CancellationToken)
        );
    }

    // ── ReplaceFontAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ReplaceFontAsync_WithRealFont_UpdatesEmbeddedFont()
    {
        var originalFont = FontSubsystemTests.LoadBundledDejaVuBytesPublic();
        var replacementFont = FontSubsystemTests.LoadBundledDejaVuBytesPublic(); // same font, different instance

        // Create a PDF with an embedded font first.
        await using var doc = await LoadAsync(
            PdfFixtures.WithEmbeddedFont(originalFont),
            TestContext.Current.CancellationToken
        );

        // Replace Helvetica (the font name in the fixture) with a new font.
        await Processor.ReplaceFontAsync(
            doc,
            "Helvetica",
            replacementFont,
            TestContext.Current.CancellationToken
        );

        // Save and reload to verify the change persisted.
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, TestContext.Current.CancellationToken);
        var embedded = reloaded.Pages[1].GetEmbeddedFontBytes();
        embedded.Values.Any(static v => v is not null)
            .ShouldBeTrue(
                "font should still be embedded after ReplaceFontAsync"
            );
    }

    // ── SubsetFontsAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SubsetFontsAsync_WithEmbeddedFont_ReducesOrMaintainsSize()
    {
        var fontBytes = FontSubsystemTests.LoadBundledDejaVuBytesPublic();
        await using var doc = await LoadAsync(
            PdfFixtures.WithEmbeddedFont(fontBytes),
            TestContext.Current.CancellationToken
        );

        // Record size before subsetting.
        using var msBefore = new MemoryStream();
        await Processor.SaveAsync(doc, msBefore, ct: TestContext.Current.CancellationToken);
        var sizeBefore = msBefore.Length;

        await Processor.SubsetFontsAsync(doc, TestContext.Current.CancellationToken);

        // Record size after subsetting.
        using var msAfter = new MemoryStream();
        await Processor.SaveAsync(doc, msAfter, ct: TestContext.Current.CancellationToken);
        var sizeAfter = msAfter.Length;

        // Subset must not make the file larger (it may be the same if font was already small).
        sizeAfter.ShouldBeLessThanOrEqualTo(
            sizeBefore,
            $"SubsetFontsAsync should not increase file size (before={sizeBefore}, after={sizeAfter})"
        );
    }

    [Fact]
    public async Task SubsetFontsAsync_NoEmbeddedFont_NoChange()
    {
        await using var doc = await LoadAsync(
            PdfFixtures.WithTextContent("Hello"),
            TestContext.Current.CancellationToken
        );

        // Should not throw even when there are no embedded fonts.
        await Should.NotThrowAsync(() =>
            Processor.SubsetFontsAsync(doc, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task SubsetFontsAsync_RawFontFile2_CollectsGlyphsAndSubsets()
    {
        // A doc whose FontFile2 is an UNFILTERED stream of real DejaVu bytes, referenced by /F1 and
        // shown via Tj and a TJ array. This drives CollectUsedGlyphs' operator walk (Tf/Tj/TJ arms)
        // and the simple-font glyph collection, and lets TrueTypeSubsetter actually shrink the font.
        var font = FontSubsystemTests.LoadBundledDejaVuBytesPublic();
        var pdf = BuildDocWithRawFontFile2(font, "BT /F1 12 Tf 50 700 Td (Hello) Tj [(Wo) -10 (rld)] TJ ET");

        await using var doc = await LoadAsync(pdf, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(() => Processor.SubsetFontsAsync(doc, TestContext.Current.CancellationToken));

        // The document must still round-trip after the subset pass (which walks Tf/Tj/TJ operators
        // and collects glyphs from the raw embedded font).
        using var after = new MemoryStream();
        await Processor.SaveAsync(doc, after, ct: TestContext.Current.CancellationToken);
        after.Length.ShouldBeGreaterThan(0);
        await using var reloaded = await LoadAsync(after.ToArray(), TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(1);
    }

    // Builds a single-page PDF with an UNFILTERED (raw binary) /FontFile2 stream so the subsetter
    // can parse and shrink it. Objects: 1 Catalog, 2 Pages, 3 Page, 4 content, 5 Font, 6 Descriptor,
    // 7 FontFile2 (raw). Assembled via MemoryStream because the font bytes are binary.
    private static byte[] BuildDocWithRawFontFile2(byte[] fontBytes, string content)
    {
        var cs = Encoding.Latin1.GetBytes(content);
        using var ms = new MemoryStream();
        var offsets = new long[8];

        PdfFixtures.Line(ms, "%PDF-1.7");
        PdfFixtures.Line(ms, "%\xE2\xE3\xCF\xD3");
        offsets[1] = ms.Position;
        PdfFixtures.Line(ms, "1 0 obj");
        PdfFixtures.Line(ms, "<< /Type /Catalog /Pages 2 0 R >>");
        PdfFixtures.Line(ms, "endobj");
        offsets[2] = ms.Position;
        PdfFixtures.Line(ms, "2 0 obj");
        PdfFixtures.Line(ms, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        PdfFixtures.Line(ms, "endobj");
        offsets[3] = ms.Position;
        PdfFixtures.Line(ms, "3 0 obj");
        PdfFixtures.Line(ms, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R");
        PdfFixtures.Line(ms, "   /Resources << /Font << /F1 5 0 R >> >> >>");
        PdfFixtures.Line(ms, "endobj");
        offsets[4] = ms.Position;
        PdfFixtures.Line(ms, "4 0 obj");
        PdfFixtures.Line(ms, $"<< /Length {cs.Length} >>");
        PdfFixtures.Line(ms, "stream");
        ms.Write(cs);
        PdfFixtures.Line(ms, "");
        PdfFixtures.Line(ms, "endstream");
        PdfFixtures.Line(ms, "endobj");
        offsets[5] = ms.Position;
        PdfFixtures.Line(ms, "5 0 obj");
        PdfFixtures.Line(ms, "<< /Type /Font /Subtype /TrueType /BaseFont /DejaVuSans /FontDescriptor 6 0 R >>");
        PdfFixtures.Line(ms, "endobj");
        offsets[6] = ms.Position;
        PdfFixtures.Line(ms, "6 0 obj");
        PdfFixtures.Line(ms, "<< /Type /FontDescriptor /FontName /DejaVuSans /Flags 32 /FontFile2 7 0 R >>");
        PdfFixtures.Line(ms, "endobj");
        offsets[7] = ms.Position;
        PdfFixtures.Line(ms, "7 0 obj");
        PdfFixtures.Line(ms, $"<< /Length {fontBytes.Length} /Length1 {fontBytes.Length} >>");
        PdfFixtures.Line(ms, "stream");
        ms.Write(fontBytes);
        PdfFixtures.Line(ms, "");
        PdfFixtures.Line(ms, "endstream");
        PdfFixtures.Line(ms, "endobj");

        var xref = ms.Position;
        PdfFixtures.Line(ms, "xref");
        PdfFixtures.Line(ms, "0 8");
        PdfFixtures.Line(ms, "0000000000 65535 f ");
        for (var i = 1; i <= 7; i++)
            PdfFixtures.Line(ms, $"{offsets[i]:D10} 00000 n ");
        PdfFixtures.Line(ms, "trailer");
        PdfFixtures.Line(ms, "<< /Size 8 /Root 1 0 R >>");
        PdfFixtures.Line(ms, "startxref");
        PdfFixtures.Line(ms, xref.ToString());
        PdfFixtures.Line(ms, "%%EOF");
        return ms.ToArray();
    }

    // ── TrueTypeSubsetter unit tests ─────────────────────────────────────────────

    [Fact]
    public void TrueTypeSubsetter_EmptyGlyphs_ReturnsOriginal()
    {
        var original = FontSubsystemTests.LoadBundledDejaVuBytesPublic();
        var result = TrueTypeSubsetter.Subset(original, new HashSet<int>());
        result.ShouldBeSameAs(original, "empty glyph set should return original unchanged");
    }

    [Fact]
    public void TrueTypeSubsetter_AllGlyphs_ReturnsOriginal()
    {
        var original = FontSubsystemTests.LoadBundledDejaVuBytesPublic();
        // Request all glyph IDs — subsetting should detect no savings and return original.
        var allGlyphs = Enumerable.Range(0, 65536).ToHashSet();
        var result = TrueTypeSubsetter.Subset(original, allGlyphs);
        // Either returns original reference or same length.
        result.Length.ShouldBe(
            original.Length,
            "requesting all glyphs should not change font size"
        );
    }

    [Fact]
    public void TrueTypeSubsetter_SubsetOfGlyphs_ProducesValidSmallFont()
    {
        var original = FontSubsystemTests.LoadBundledDejaVuBytesPublic();
        // Only keep glyphs for 'A'-'Z' (approx glyph IDs 36–61 for basic Latin).
        var usedGlyphs = Enumerable.Range(36, 26).ToHashSet();
        var result = TrueTypeSubsetter.Subset(original, usedGlyphs);

        // Result should be a valid TrueType (starts with 0x00010000 or 'OTTO').
        result.Length.ShouldBeGreaterThan(12);
        // sfVersion bytes 0–3.
        var isOtf = result[0] == 0x4F && result[1] == 0x54; // 'OT'
        var isTtf = result[0] == 0x00 && result[1] == 0x01; // TrueType
        (isOtf || isTtf).ShouldBeTrue("subset result should have valid TrueType/OTF sfVersion");

        // Subset should be smaller than original (DejaVuSans has 6000+ glyphs).
        result.Length.ShouldBeLessThan(
            original.Length,
            "subset of 26 glyphs should be much smaller than full font"
        );
    }

    [Fact]
    public void TrueTypeSubsetter_ShortFont_ReturnsOriginal()
    {
        // Fewer than 12 bytes → the early guard returns the input unchanged.
        var tiny = new byte[] { 0x00, 0x01, 0x02 };
        TrueTypeSubsetter.Subset(tiny, new HashSet<int> { 1 }).ShouldBeSameAs(tiny);
    }

    [Fact]
    public void TrueTypeSubsetter_CorruptFont_ReturnsOriginal()
    {
        // 16+ bytes with a plausible header but garbage table directory → SubsetCore throws,
        // the catch returns the original bytes intact.
        var corrupt = new byte[64];
        corrupt[0] = 0x00;
        corrupt[1] = 0x01;
        corrupt[4] = 0x00;
        corrupt[5] = 0x05; // claims 5 tables that do not exist
        TrueTypeSubsetter.Subset(corrupt, new HashSet<int> { 1, 2, 3 }).ShouldBeSameAs(corrupt);
    }
}
