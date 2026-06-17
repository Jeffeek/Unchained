using System.Text;
using Shouldly;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     Branch coverage for <see cref="Unchained.Pdf.Engine.FontMutator" /> paths the existing
///     <see cref="FontMutatorBranchTests" /> miss: the <c>ReplaceFont</c> match body (font found),
///     the <c>SubsetFonts</c> success path (a raw, unfiltered FontFile2 whose subset is smaller),
///     and the glyph-collection walk over <c>Tj</c>/<c>TJ</c> operators for a simple font.
///     Uses a PDF whose FontFile2 holds the real DejaVu TrueType program with no filter, so the
///     subsetter receives a valid font program rather than ASCII-hex text.
/// </summary>
public sealed class FontMutatorRawFontTests : RendererTestBase
{
    // Builds a single-page PDF with an UNFILTERED /FontFile2 (raw TTF bytes) and content that
    // shows text via both Tj and TJ so the glyph-collection walk visits both operator arms.
    private static byte[] BuildWithRawFontFile2(byte[] ttf, string baseFont = "DejaVuSans")
    {
        const string content = "BT /F1 12 Tf 100 700 Td (Hello) Tj [(Wor) -10 (ld)] TJ ET";
        var csBytes = Encoding.Latin1.GetBytes(content);

        using var ms = new MemoryStream();
        var offsets = new long[7];

        Line("%PDF-1.7");
        Line("%\xE2\xE3\xCF\xD3");

        offsets[0] = Pos();
        Line("1 0 obj");
        Line("<< /Type /Catalog /Pages 2 0 R >>");
        Line("endobj");

        offsets[1] = Pos();
        Line("2 0 obj");
        Line("<< /Type /Pages /Kids [7 0 R] /Count 1 >>");
        Line("endobj");

        offsets[2] = Pos();
        Line("3 0 obj");
        Line($"<< /Length {csBytes.Length} >>");
        Line("stream");
        Binary(csBytes);
        Line(string.Empty);
        Line("endstream");
        Line("endobj");

        offsets[3] = Pos();
        Line("4 0 obj");
        Line($"<< /Type /FontDescriptor /FontName /{baseFont} /Flags 32 /FontFile2 5 0 R >>");
        Line("endobj");

        // Object 5 — raw (no filter) embedded font program.
        offsets[4] = Pos();
        Line("5 0 obj");
        Line($"<< /Length {ttf.Length} /Length1 {ttf.Length} >>");
        Line("stream");
        Binary(ttf);
        Line(string.Empty);
        Line("endstream");
        Line("endobj");

        offsets[5] = Pos();
        Line("6 0 obj");
        Line($"<< /Type /Font /Subtype /TrueType /BaseFont /{baseFont} /FontDescriptor 4 0 R >>");
        Line("endobj");

        offsets[6] = Pos();
        Line("7 0 obj");
        Line("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 3 0 R");
        Line("   /Resources << /Font << /F1 6 0 R >> >> >>");
        Line("endobj");

        var xref = Pos();
        Line("xref");
        Line("0 8");
        Line("0000000000 65535 f ");
        foreach (var o in offsets) Line($"{o:D10} 00000 n ");
        Line("trailer");
        Line("<< /Size 8 /Root 1 0 R >>");
        Line("startxref");
        Line(xref.ToString());
        Text("%%EOF");

        return ms.ToArray();

        void Text(string s) => ms.Write(Encoding.Latin1.GetBytes(s));

        void Line(string s)
        {
            Text(s);
            ms.WriteByte((byte)'\n');
        }

        void Binary(byte[] b) => ms.Write(b);
        long Pos() => ms.Position;
    }

    [Fact]
    public async Task SubsetFonts_RawFontFile_ReducesEmbeddedFontSize()
    {
        var font = LoadDejaVuSansRegular();
        await using var doc = await LoadAsync(BuildWithRawFontFile2(font), TestContext.Current.CancellationToken);

        using var before = new MemoryStream();
        await Processor.SaveAsync(doc, before, ct: TestContext.Current.CancellationToken);
        var sizeBefore = before.Length;

        await Processor.SubsetFontsAsync(doc, TestContext.Current.CancellationToken);

        using var after = new MemoryStream();
        await Processor.SaveAsync(doc, after, ct: TestContext.Current.CancellationToken);
        // Only a few glyphs are used, so the subset is dramatically smaller than full DejaVu.
        after.Length.ShouldBeLessThan(sizeBefore);
    }

    [Fact]
    public async Task ReplaceFont_MatchingBaseFont_SwapsProgramAndReloads()
    {
        var font = LoadDejaVuSansRegular();
        await using var doc = await LoadAsync(BuildWithRawFontFile2(font), TestContext.Current.CancellationToken);

        // "DejaVuSans" matches the document's BaseFont → the match body runs.
        await Processor.ReplaceFontAsync(
            doc,
            "DejaVuSans",
            font,
            TestContext.Current.CancellationToken
        );

        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task ReplaceFont_StyleSuffixName_MatchesViaNormalisation()
    {
        var font = LoadDejaVuSansRegular();
        // BaseFont has a style suffix; NormalizeBaseFont strips it to "DejaVuSans".
        await using var doc = await LoadAsync(
            BuildWithRawFontFile2(font, "DejaVuSans-Bold"),
            TestContext.Current.CancellationToken
        );

        await Processor.ReplaceFontAsync(
            doc,
            "DejaVuSans-Bold",
            font,
            TestContext.Current.CancellationToken
        );

        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(1);
    }
}
