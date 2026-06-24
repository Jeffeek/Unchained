using System.Text;
using Shouldly;
using Unchained.Drawing.Text;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     Tests for M6 steps 6.7 (HarfBuzz text shaping) and 6.8 (NotoSans fallback font).
/// </summary>
public sealed class HarfBuzzShapingTests : RendererTestBase
{
    // ── 6.7 — HarfBuzz shaping ────────────────────────────────────────────────

    [Fact]
    public async Task RenderPage_LigatureFontText_ProducesPng()
    {
        // Any text through a shaped font should produce a valid PNG.
        SkipIfNoFreeType();

        var fontData = LoadBundledDejaVuBytes();
        const string cs = "BT /F1 14 Tf 50 700 Td (fi) Tj ET"; // "fi" should form a ligature
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData, cs);
        await using var doc = await LoadAsync(pdfBytes, TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
        png[..8].ShouldBe(PdfTestConstants.PngSignature);
    }

    [Fact]
    public async Task RenderPage_MultiCharText_ProducesNonTrivialPng()
    {
        SkipIfNoFreeType();

        // Multi-character shaped text should produce a substantially-sized PNG.
        var fontData = LoadBundledDejaVuBytes();
        const string cs = "BT /F1 12 Tf 50 700 Td (Hello, World!) Tj ET";
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData, cs);
        await using var doc = await LoadAsync(pdfBytes, TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
        png.Length.ShouldBeGreaterThan(500);
    }

    [Fact]
    public async Task RenderPage_StandardDocument_ShapingDoesNotBreakExistingOutput()
    {
        SkipIfNoFreeType();

        // Existing table-generated documents should still render correctly.
        var gen = new TableGenerator();
        var data = new TableData
        {
            Headers = ["Name", "Score"],
            Rows = [["Alice", "100"], ["Bob", "95"]]
        };
        await using var doc = await gen.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);
        var pages = await Renderer!.RenderDocumentAsync(doc, RenderOptions.Default, TestContext.Current.CancellationToken);
        pages.Count.ShouldBe(doc.PageCount);
        pages.ShouldAllBe(static p => p.Length > 100);
    }

    [Fact]
    public async Task RenderPage_AllPageOperatorsStillWork_WithShaping()
    {
        SkipIfNoFreeType();

        // Table with alternating rows and borders tests the full rendering pipeline.
        var gen = new TableGenerator();
        var data = new TableData
        {
            Headers = ["Column A", "Column B"],
            Rows = Enumerable.Range(1, 5).Select(static i => (IReadOnlyList<string>)[$"Row {i}", $"Val {i}"]).ToList()
        };
        var style = new TableStyle(DrawBorders: true, AlternatingRowColor: true);
        await using var doc = await gen.GenerateAsync(data, style, TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
        png[..8].ShouldBe(PdfTestConstants.PngSignature);
    }

    // ── 6.8 — NotoSans fallback font ──────────────────────────────────────────

    [Fact]
    public async Task RenderPage_UnknownFontName_FallsBackToNotoSans()
    {
        SkipIfNoFreeType();

        // A font name that isn't Standard 14 and has no embedded data → NotoSans fallback.
        const string cs = "BT /F1 12 Tf 50 700 Td (Fallback text) Tj ET";
        // Build PDF where F1 maps to an unrecognised base font name.
        var pdfBytes = BuildWithUnknownFont(cs);
        await using var doc = await LoadAsync(pdfBytes, TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
        // Should render without throwing — NotoSans is used as fallback.
        png[..8].ShouldBe(PdfTestConstants.PngSignature);
    }

    [Fact]
    public void FontCache_NotoSansIsEmbeddedResource()
    {
        // Fonts are now bundled in Unchained.Drawing.Text
        var asm = typeof(FontCache).Assembly;
        const string name = "Unchained.Drawing.Text.Fonts.NotoSans-Regular.ttf";
        using var stream = asm.GetManifestResourceStream(name);
        stream.ShouldNotBeNull($"Expected '{name}' in {asm.GetName().Name}");
        stream.Length.ShouldBeGreaterThan(100_000); // NotoSans-Regular is ~500KB
    }

    [Fact]
    public void FontCache_DejaVuFontsStillEmbedded()
    {
        // Fonts are now bundled in Unchained.Drawing.Text
        var asm = typeof(FontCache).Assembly;
        var names = new[]
        {
            "Unchained.Drawing.Text.Fonts.DejaVuSans-Regular.ttf",
            "Unchained.Drawing.Text.Fonts.DejaVuSans-Bold.ttf",
            "Unchained.Drawing.Text.Fonts.DejaVuSerif-Regular.ttf",
            "Unchained.Drawing.Text.Fonts.DejaVuSansMono-Regular.ttf"
        };
        foreach (var name in names)
        {
            using var stream = asm.GetManifestResourceStream(name);
            stream.ShouldNotBeNull($"{name} not found in {asm.GetName().Name}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static byte[] LoadBundledDejaVuBytes()
    {
        var asm = typeof(FontCache).Assembly; // fonts are in Unchained.Drawing.Text
        using var stream = asm.GetManifestResourceStream(
                               "Unchained.Drawing.Text.Fonts.DejaVuSans-Regular.ttf"
                           )
                           ?? throw new InvalidOperationException("DejaVuSans-Regular.ttf not found.");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    // Builds a single-page PDF where /F1 uses an unrecognised font name (no embedding).
    private static byte[] BuildWithUnknownFont(string contentStream)
    {
        using var ms = new MemoryStream();

        PdfFixtures.Line(ms, "%PDF-1.7");
        PdfFixtures.Line(ms, "%\xE2\xE3\xCF\xD3");
        var o1 = PdfFixtures.Pos(ms);
        PdfFixtures.Line(ms, "1 0 obj");
        PdfFixtures.Line(ms, "<< /Type /Catalog /Pages 2 0 R >>");
        PdfFixtures.Line(ms, "endobj");
        var o2 = PdfFixtures.Pos(ms);
        PdfFixtures.Line(ms, "2 0 obj");
        PdfFixtures.Line(ms, "<< /Type /Pages /Kids [4 0 R] /Count 1 >>");
        PdfFixtures.Line(ms, "endobj");
        var csBytes = Encoding.Latin1.GetByteCount(contentStream);
        var o3 = PdfFixtures.Pos(ms);
        PdfFixtures.Line(ms, "3 0 obj");
        PdfFixtures.Line(ms, $"<< /Length {csBytes} >>");
        PdfFixtures.Line(ms, "stream");
        PdfFixtures.Line(ms, contentStream);
        PdfFixtures.Line(ms, "endstream");
        PdfFixtures.Line(ms, "endobj");
        var o4 = PdfFixtures.Pos(ms);
        PdfFixtures.Line(ms, "4 0 obj");
        PdfFixtures.Line(ms, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 3 0 R");
        PdfFixtures.Line(ms, "   /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /UnknownFontXYZ >> >> >> >>");
        PdfFixtures.Line(ms, "endobj");
        var xref = PdfFixtures.Pos(ms);
        PdfFixtures.Line(ms, "xref");
        PdfFixtures.Line(ms, "0 5");
        PdfFixtures.Line(ms, "0000000000 65535 f ");
        foreach (var o in new[] { o1, o2, o3, o4 })
            PdfFixtures.Line(ms, $"{o:D10} 00000 n ");
        PdfFixtures.Line(ms, "trailer");
        PdfFixtures.Line(ms, "<< /Size 5 /Root 1 0 R >>");
        PdfFixtures.Line(ms, "startxref");
        PdfFixtures.Line(ms, xref.ToString());
        ms.Write(Encoding.Latin1.GetBytes("%%EOF"));
        return ms.ToArray();
    }
}
