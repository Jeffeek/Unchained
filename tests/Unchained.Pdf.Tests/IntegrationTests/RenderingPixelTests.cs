using System.IO.Compression;
using System.Text;
using Shouldly;
using Unchained.Drawing.Text;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     Pixel-level rendering tests that verify actual content appears in the rendered PNG.
///     Previous tests only checked PNG validity/size; those pass even for blank output.
///     These tests decode the PNG and count non-white pixels to confirm content is visible.
/// </summary>
public sealed class RenderingPixelTests : RendererTestBase
{
    // ── Pixel helpers ─────────────────────────────────────────────────────────

    /// <summary>
    ///     Decodes a PNG produced by PdfPngEncoder (RGBA, filter=None, ZLib IDAT)
    ///     and returns the count of pixels that are not pure white.
    /// </summary>
    private static int CountNonWhitePixels(byte[] png)
    {
        // PNG: 8-byte signature + chunks
        // IHDR: 4(len)+4("IHDR")+13(data)+4(crc) = 25 bytes
        // IDAT: next chunk

        // Read width and height from IHDR
        var width = (int)ReadUInt32BigEndian(png, 16);
        var height = (int)ReadUInt32BigEndian(png, 20);

        // Find IDAT chunk — it starts right after IHDR (offset 33)
        // Chunk format: 4(length) + 4(type) + data + 4(crc)
        var idatLength = (int)ReadUInt32BigEndian(png, 33);
        var idatData = png.AsSpan(33 + 8, idatLength).ToArray();

        // Decompress ZLib (ZLibStream handles the ZLib header)
        using var compressed = new MemoryStream(idatData);
        using var decompressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionMode.Decompress))
            zlib.CopyTo(decompressed);

        var raw = decompressed.ToArray();

        // PdfPngEncoder uses color type 6 (RGBA), filter=0 (None)
        // Scanline layout: 1 filter byte + width * 4 bytes (R,G,B,A)
        var stride = 1 + (width * 4);
        var nonWhite = 0;

        for (var y = 0; y < height; y++)
        {
            var rowStart = (y * stride) + 1; // +1 to skip filter byte
            for (var x = 0; x < width; x++)
            {
                var r = raw[rowStart + (x * 4)];
                var g = raw[rowStart + (x * 4) + 1];
                var b = raw[rowStart + (x * 4) + 2];
                if (r < 255 || g < 255 || b < 255)
                    nonWhite++;
            }
        }

        return nonWhite;
    }

    private static uint ReadUInt32BigEndian(byte[] data, int offset) =>
        ((uint)data[offset] << 24) |
        ((uint)data[offset + 1] << 16) |
        ((uint)data[offset + 2] << 8) |
        data[offset + 3];

    // ── Blank-page baseline ───────────────────────────────────────────────────

    [Fact]
    public async Task BlankPage_HasZeroNonWhitePixels()
    {
        SkipIfNoFreeType();

        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(96), TestContext.Current.CancellationToken);

        var nonWhite = CountNonWhitePixels(png);
        nonWhite.ShouldBe(0, "blank page should have zero non-white pixels");
    }

    // ── Text rendering ────────────────────────────────────────────────────────

    [Fact]
    public async Task TextWithEmbeddedFont_HasNonWhitePixels()
    {
        SkipIfNoFreeType();

        // "H" rendered with embedded DejaVu font — should produce visible dark pixels.
        var fontData = LoadDejaVuBytes();
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData, "BT /F1 24 Tf 100 600 Td (H) Tj ET");
        await using var doc = await LoadAsync(pdfBytes, TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(96), TestContext.Current.CancellationToken);

        var nonWhite = CountNonWhitePixels(png);
        nonWhite.ShouldBeGreaterThan(50,
            $"rendering 'H' with embedded DejaVu at 24pt should produce at least 50 non-white pixels; got {nonWhite}");
    }

    [Fact]
    public async Task TextWithEmbeddedFont_MorePixelsThanBlankPage()
    {
        SkipIfNoFreeType();

        // Compare blank page vs. text page pixel counts.
        await using var blank = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var blankPng = await Renderer!.RenderPageAsync(blank.Pages[1], new RenderOptions(96), TestContext.Current.CancellationToken);

        var fontData = LoadDejaVuBytes();
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData,
            "BT /F1 14 Tf 50 700 Td (Hello, World!) Tj ET");
        await using var textDoc = await LoadAsync(pdfBytes, TestContext.Current.CancellationToken);
        var textPng = await Renderer!.RenderPageAsync(textDoc.Pages[1], new RenderOptions(96), TestContext.Current.CancellationToken);

        var blankNonWhite = CountNonWhitePixels(blankPng);
        var textNonWhite = CountNonWhitePixels(textPng);

        blankNonWhite.ShouldBe(0, "blank page should have zero non-white pixels");
        textNonWhite.ShouldBeGreaterThan(blankNonWhite + 100,
            $"text page should have significantly more non-white pixels than blank. " +
            $"blank={blankNonWhite}, text={textNonWhite}");
    }

    [Fact]
    public async Task TextFallbackFont_Helvetica_HasNonWhitePixels()
    {
        SkipIfNoFreeType();

        // Helvetica is a Standard 14 font — not embedded, substituted with DejaVu.
        // This tests the substitute-font path for real-world PDFs.
        const string cs = "BT /F1 18 Tf 72 720 Td (Hello) Tj ET";
        var pdfBytes = BuildPageWithHelvetica(cs);
        await using var doc = await LoadAsync(pdfBytes, TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(96), TestContext.Current.CancellationToken);

        var nonWhite = CountNonWhitePixels(png);
        nonWhite.ShouldBeGreaterThan(50,
            $"Standard 14 font (Helvetica) substituted with DejaVu should render visible text. " +
            $"Got {nonWhite} non-white pixels — expected > 50");
    }

    [Fact]
    public async Task TextFallbackFont_TimesRoman_HasNonWhitePixels()
    {
        SkipIfNoFreeType();

        const string cs = "BT /F1 16 Tf 72 700 Td (World) Tj ET";
        var pdfBytes = BuildPageWithTimesRoman(cs);
        await using var doc = await LoadAsync(pdfBytes, TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(96), TestContext.Current.CancellationToken);

        var nonWhite = CountNonWhitePixels(png);
        nonWhite.ShouldBeGreaterThan(50,
            $"Times-Roman should substitute to DejaVuSerif and produce visible text. " +
            $"Got {nonWhite} non-white pixels");
    }

    // ── Image rendering (control — already known to work) ─────────────────────

    [Fact]
    public async Task ImageXObject_HasNonWhitePixels()
    {
        SkipIfNoFreeType();

        // A red 10×10 image — should produce non-white pixels.
        var rgb = new byte[10 * 10 * 3];
        for (var i = 0; i < rgb.Length; i += 3)
        {
            rgb[i] = 255;
            rgb[i + 1] = 0;
            rgb[i + 2] = 0;
        }

        var pdfBytes = PdfFixtures.WithImageXObject(10, 10, rgb);
        await using var doc = await LoadAsync(pdfBytes, TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(96), TestContext.Current.CancellationToken);

        var nonWhite = CountNonWhitePixels(png);
        nonWhite.ShouldBeGreaterThan(0,
            "image XObject with red pixels should produce non-white output");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static byte[] LoadDejaVuBytes()
    {
        var asm = typeof(FontCache).Assembly;
        using var stream = asm.GetManifestResourceStream(
                               "Unchained.Drawing.Text.Fonts.DejaVuSans-Regular.ttf")
                           ?? throw new InvalidOperationException("DejaVuSans-Regular not found");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>Builds a page with Helvetica declared in the font dict (not embedded).</summary>
    private static byte[] BuildPageWithHelvetica(string contentStream) =>
        BuildPageWithFont("Helvetica", "Type1", contentStream);

    /// <summary>Builds a page with Times-Roman declared in the font dict (not embedded).</summary>
    private static byte[] BuildPageWithTimesRoman(string contentStream) =>
        BuildPageWithFont("Times-Roman", "Type1", contentStream);

    private static byte[] BuildPageWithFont(string baseFontName, string subtype, string contentStream)
    {
        using var ms = new MemoryStream();

        byte[] L(string s) => Encoding.Latin1.GetBytes(s + "\n");
        long Pos() => ms.Position;

        ms.Write(L("%PDF-1.7"));
        ms.Write(L("%\xE2\xE3\xCF\xD3"));

        var o1 = Pos();
        ms.Write(L("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj"));

        var o2 = Pos();
        ms.Write(L("2 0 obj << /Type /Pages /Kids [5 0 R] /Count 1 >> endobj"));

        var csBytes = Encoding.Latin1.GetBytes(contentStream);
        var o3 = Pos();
        ms.Write(L("3 0 obj"));
        ms.Write(L($"<< /Length {csBytes.Length} >>"));
        ms.Write(L("stream"));
        ms.Write(csBytes);
        ms.Write(L("\nendstream endobj"));

        var o4 = Pos();
        ms.Write(L("4 0 obj"));
        ms.Write(L($"<< /Type /Font /Subtype /{subtype} /BaseFont /{baseFontName} >>"));
        ms.Write(L("endobj"));

        var o5 = Pos();
        ms.Write(L("5 0 obj"));
        ms.Write(L("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 3 0 R"));
        ms.Write(L("   /Resources << /Font << /F1 4 0 R >> >> >>"));
        ms.Write(L("endobj"));

        var xref = Pos();
        ms.Write(L("xref"));
        ms.Write(L("0 6"));
        ms.Write(L("0000000000 65535 f "));
        foreach (var o in new[] { o1, o2, o3, o4, o5 })
            ms.Write(L($"{o:D10} 00000 n "));
        ms.Write(L("trailer << /Size 6 /Root 1 0 R >>"));
        ms.Write(L("startxref"));
        ms.Write(L(xref.ToString()));
        ms.Write(Encoding.Latin1.GetBytes("%%EOF"));

        return ms.ToArray();
    }
}
