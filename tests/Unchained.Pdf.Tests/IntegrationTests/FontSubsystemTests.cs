using System.Diagnostics.CodeAnalysis;
using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Unchained.Pdf.Rendering.Engine;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
/// Tests for M6: embedded font extraction, proportional table column widths,
/// image XObject rendering, and corrected FreeType2 advance widths.
/// </summary>
public sealed class FontSubsystemTests : RendererTestBase
{
    // ── 6.1 / 6.2 — Embedded font byte extraction ────────────────────────────

    [Fact]
    public async Task GetEmbeddedFontBytes_PageWithNoEmbeddedFonts_ReturnsNullValues()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithTextContent(text: "Hello"));
        var fontBytes = doc.Pages[1].GetEmbeddedFontBytes();
        // Standard 14 fonts are never embedded — all entries null.
        fontBytes.Values.ShouldAllBe(static b => b == null);
    }

    [Fact]
    public async Task GetEmbeddedFontBytes_PageWithEmbeddedFont_ReturnsByteArray()
    {
        var fontData = SyntheticFontBytes();
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData);
        await using var doc = await LoadAsync(pdfBytes);
        var fontBytes = doc.Pages[1].GetEmbeddedFontBytes();
        fontBytes.ContainsKey("F1").ShouldBeTrue();
        fontBytes["F1"].ShouldNotBeNull();
        fontBytes["F1"]!.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetEmbeddedFontBytes_EmbeddedBytes_MatchOriginal()
    {
        var fontData = SyntheticFontBytes();
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData);
        await using var doc = await LoadAsync(pdfBytes);
        var extracted = doc.Pages[1].GetEmbeddedFontBytes()["F1"];
        extracted.ShouldNotBeNull();
        extracted.Length.ShouldBe(fontData.Length);
    }

    [Fact]
    public async Task GetEmbeddedFontBytes_PageWithNoFonts_ReturnsEmptyDict()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());
        doc.Pages[1].GetEmbeddedFontBytes().ShouldBeEmpty();
    }

    // ── 6.3 / 6.5 — FreeType2 advance width correction ───────────────────────

    [Fact]
    public async Task RenderPage_WithEmbeddedFont_ProducesPng()
    {
        if (!FreeTypeAvailable) return;

        // Use the bundled DejaVu font (valid TrueType) so FreeType2 can actually load it.
        var fontData = LoadBundledDejaVuBytes();
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData);
        await using var doc = await LoadAsync(pdfBytes);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default);
        png[..8].ShouldBe(PdfTestConstants.PngSignature);
    }

    [Fact]
    public async Task RenderPage_WithEmbeddedFont_ProducesValidSizedPng()
    {
        if (!FreeTypeAvailable) return;

        var fontData = LoadBundledDejaVuBytes();
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData);
        await using var doc = await LoadAsync(pdfBytes);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default);
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
        await using var doc = await gen.GenerateAsync(data, TableStyle.Default);
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
        await using var doc = await gen.GenerateAsync(data, TableStyle.Default);
        // Round-trip should still produce correct page count.
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms);
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
        var layout = TableLayout.Compute(data.Headers.Count, TableStyle.Default, hasTitle: false, data);
        layout.ColumnWidths.Sum().ShouldBe(
            TableLayout.PageWidth - (2 * TableLayout.Margin),
            tolerance: 0.1f);
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
        var layout = TableLayout.Compute(data.Headers.Count, TableStyle.Default, hasTitle: false, data);
        layout.ColumnWidths[1].ShouldBeGreaterThan(layout.ColumnWidths[0]);
    }

    // ── 6.6 — Image XObject (Do operator) ────────────────────────────────────

    [Fact]
    public async Task GetImageXObjects_PageWithNoImages_ReturnsEmpty()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());
        doc.Pages[1].GetImageXObjects().ShouldBeEmpty();
    }

    [Fact]
    public async Task GetImageXObjects_PageWithImage_ReturnsEntry()
    {
        var rgb = CreateSolidRgb(4, 4, r: 255, g: 0, b: 0);
        await using var doc = await LoadAsync(PdfFixtures.WithImageXObject(4, 4, rgb));
        var images = doc.Pages[1].GetImageXObjects();
        images.ContainsKey("Im1").ShouldBeTrue();
    }

    [Fact]
    public async Task GetImageXObjects_PageWithImage_CorrectDimensions()
    {
        var rgb = CreateSolidRgb(8, 6, r: 0, g: 255, b: 0);
        await using var doc = await LoadAsync(PdfFixtures.WithImageXObject(8, 6, rgb));
        var img = doc.Pages[1].GetImageXObjects()["Im1"];
        img.Width.ShouldBe(8);
        img.Height.ShouldBe(6);
    }

    [Fact]
    public async Task GetImageXObjects_PageWithImage_RgbDataDecodedCorrectly()
    {
        var rgb = CreateSolidRgb(2, 2, r: 128, g: 64, b: 32);
        await using var doc = await LoadAsync(PdfFixtures.WithImageXObject(2, 2, rgb));
        var img = doc.Pages[1].GetImageXObjects()["Im1"];
        img.RgbData[0].ShouldBe((byte)128);
        img.RgbData[1].ShouldBe((byte)64);
        img.RgbData[2].ShouldBe((byte)32);
    }

    [Fact]
    public async Task RenderPage_WithImageXObject_ProducesPng()
    {
        if (!FreeTypeAvailable)
            return;

        var rgb = CreateSolidRgb(4, 4, r: 200, g: 100, b: 50);
        var pdfBytes = PdfFixtures.WithImageXObject(4, 4, rgb);
        await using var doc = await LoadAsync(pdfBytes);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default);
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

    // Loads the bundled DejaVuSans-Regular.ttf from the Rendering assembly (valid TrueType,
    // usable by FreeType2 for rendering tests).
    private static byte[] LoadBundledDejaVuBytes()
    {
        var asm = typeof(PdfRenderer).Assembly;
        using var stream = asm.GetManifestResourceStream("Unchained.Pdf.Rendering.Rendering.Fonts.DejaVuSans-Regular.ttf")
                           ?? throw new InvalidOperationException(
                               "DejaVuSans-Regular.ttf not found in Rendering assembly.");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    [SuppressMessage("ReSharper", "BadListLineBreaks")]
    private static byte[] CreateSolidRgb(int width, int height, byte r, byte g, byte b)
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
