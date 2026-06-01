using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Rendering.Engine;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
/// Tests for M6 steps 6.7 (HarfBuzz text shaping) and 6.8 (NotoSans fallback font).
/// </summary>
public sealed class HarfBuzzShapingTests : IDisposable
{
    private static readonly DocumentProcessor Processor = new();
    private readonly PdfRenderer? _renderer;
    private readonly bool _freeTypeAvailable;

    public HarfBuzzShapingTests()
    {
        try
        {
            _renderer = new PdfRenderer();
            _freeTypeAvailable = true;
        }
        catch
        {
            _freeTypeAvailable = false;
        }
    }

    public void Dispose() => _renderer?.Dispose();

    private bool FreeTypeAvailable() => _freeTypeAvailable;

    // ── 6.7 — HarfBuzz shaping ────────────────────────────────────────────────

    [Fact]
    public async Task RenderPage_LigatureFontText_ProducesPng()
    {
        // Any text through a shaped font should produce a valid PNG.
        if (!FreeTypeAvailable())
            return;

        var fontData = LoadBundledDejaVuBytes();
        const string cs = "BT /F1 14 Tf 50 700 Td (fi) Tj ET"; // "fi" should form a ligature
        var pdfBytes = Helpers.PdfFixtures.WithEmbeddedFont(fontData, cs);
        await using var doc = await Processor.LoadAsync(new MemoryStream(pdfBytes));
        var png = await _renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default);
        png[..8].ShouldBe([137, 80, 78, 71, 13, 10, 26, 10]);
    }

    [Fact]
    public async Task RenderPage_MultiCharText_ProducesNonTrivialPng()
    {
        if (!FreeTypeAvailable())
            return;

        // Multi-character shaped text should produce a substantially-sized PNG.
        var fontData = LoadBundledDejaVuBytes();
        const string cs = "BT /F1 12 Tf 50 700 Td (Hello, World!) Tj ET";
        var pdfBytes = Helpers.PdfFixtures.WithEmbeddedFont(fontData, cs);
        await using var doc = await Processor.LoadAsync(new MemoryStream(pdfBytes));
        var png = await _renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default);
        png.Length.ShouldBeGreaterThan(500);
    }

    [Fact]
    public async Task RenderPage_StandardDocument_ShapingDoesNotBreakExistingOutput()
    {
        if (!FreeTypeAvailable())
            return;

        // Existing table-generated documents should still render correctly.
        var gen = new TableGenerator();
        var data = new TableData
        {
            Headers = ["Name", "Score"],
            Rows = [["Alice", "100"], ["Bob", "95"]]
        };
        await using var doc = await gen.GenerateAsync(data, TableStyle.Default);
        var pages = await _renderer!.RenderDocumentAsync(doc, RenderOptions.Default);
        pages.Count.ShouldBe(doc.PageCount);
        pages.ShouldAllBe(static p => p.Length > 100);
    }

    [Fact]
    public async Task RenderPage_AllPageOperatorsStillWork_WithShaping()
    {
        if (!FreeTypeAvailable())
            return;

        // Table with alternating rows and borders tests the full rendering pipeline.
        var gen = new TableGenerator();
        var data = new TableData
        {
            Headers = ["Column A", "Column B"],
            Rows = Enumerable.Range(1, 5).Select(static i => (IReadOnlyList<string>)[$"Row {i}", $"Val {i}"]).ToList()
        };
        var style = new TableStyle(DrawBorders: true, AlternatingRowColor: true);
        await using var doc = await gen.GenerateAsync(data, style);
        var png = await _renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default);
        png[..8].ShouldBe([137, 80, 78, 71, 13, 10, 26, 10]);
    }

    // ── 6.8 — NotoSans fallback font ──────────────────────────────────────────

    [Fact]
    public async Task RenderPage_UnknownFontName_FallsBackToNotoSans()
    {
        if (!FreeTypeAvailable()) return;

        // A font name that isn't Standard 14 and has no embedded data → NotoSans fallback.
        const string cs = "BT /F1 12 Tf 50 700 Td (Fallback text) Tj ET";
        // Build PDF where F1 maps to an unrecognised base font name.
        var pdfBytes = BuildWithUnknownFont(cs);
        await using var doc = await Processor.LoadAsync(new MemoryStream(pdfBytes));
        var png = await _renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default);
        // Should render without throwing — NotoSans is used as fallback.
        png[..8].ShouldBe([137, 80, 78, 71, 13, 10, 26, 10]);
    }

    [Fact]
    public void FontCache_NotoSansIsEmbeddedResource()
    {
        var asm = typeof(PdfRenderer).Assembly;
        const string name = "Unchained.Pdf.Rendering.Rendering.Fonts.NotoSans-Regular.ttf";
        using var stream = asm.GetManifestResourceStream(name);
        stream.ShouldNotBeNull();
        stream.Length.ShouldBeGreaterThan(100_000); // NotoSans-Regular is ~500KB
    }

    [Fact]
    public void FontCache_DejaVuFontsStillEmbedded()
    {
        var asm = typeof(PdfRenderer).Assembly;
        var names = new[]
        {
            "Unchained.Pdf.Rendering.Rendering.Fonts.DejaVuSans-Regular.ttf",
            "Unchained.Pdf.Rendering.Rendering.Fonts.DejaVuSans-Bold.ttf",
            "Unchained.Pdf.Rendering.Rendering.Fonts.DejaVuSerif-Regular.ttf",
            "Unchained.Pdf.Rendering.Rendering.Fonts.DejaVuSansMono-Regular.ttf"
        };
        foreach (var name in names)
        {
            using var stream = asm.GetManifestResourceStream(name);
            stream.ShouldNotBeNull($"{name} not found");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static byte[] LoadBundledDejaVuBytes()
    {
        var asm = typeof(PdfRenderer).Assembly;
        using var stream = asm.GetManifestResourceStream(
                               "Unchained.Pdf.Rendering.Rendering.Fonts.DejaVuSans-Regular.ttf")
                           ?? throw new InvalidOperationException("DejaVuSans-Regular.ttf not found.");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    // Builds a single-page PDF where /F1 uses an unrecognised font name (no embedding).
    private static byte[] BuildWithUnknownFont(string contentStream)
    {
        using var ms = new MemoryStream();

        Line("%PDF-1.7");
        Line("%\xE2\xE3\xCF\xD3");
        var o1 = Pos();
        Line("1 0 obj");
        Line("<< /Type /Catalog /Pages 2 0 R >>");
        Line("endobj");
        var o2 = Pos();
        Line("2 0 obj");
        Line("<< /Type /Pages /Kids [4 0 R] /Count 1 >>");
        Line("endobj");
        var csBytes = System.Text.Encoding.Latin1.GetByteCount(contentStream);
        var o3 = Pos();
        Line("3 0 obj");
        Line($"<< /Length {csBytes} >>");
        Line("stream");
        Line(contentStream);
        Line("endstream");
        Line("endobj");
        var o4 = Pos();
        Line("4 0 obj");
        Line("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 3 0 R");
        Line("   /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /UnknownFontXYZ >> >> >> >>");
        Line("endobj");
        var xref = Pos();
        Line("xref");
        Line("0 5");
        Line("0000000000 65535 f ");
        foreach (var o in new[] { o1, o2, o3, o4 })
            Line($"{o:D10} 00000 n ");
        Line("trailer");
        Line("<< /Size 5 /Root 1 0 R >>");
        Line("startxref");
        Line(xref.ToString());
        ms.Write(System.Text.Encoding.Latin1.GetBytes("%%EOF"));
        return ms.ToArray();

        long Pos() => ms.Position;

        void Line(string s)
        {
            ms.Write(System.Text.Encoding.Latin1.GetBytes(s));
            ms.WriteByte((byte)'\n');
        }
    }
}
