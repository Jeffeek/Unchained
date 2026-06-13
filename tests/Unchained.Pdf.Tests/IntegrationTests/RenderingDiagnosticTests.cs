using System.IO.Compression;
using System.Runtime.InteropServices;
using HarfBuzzSharp;
using Shouldly;
using Unchained.Drawing.Text;
using Unchained.Pdf.Core;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;
using Buffer = HarfBuzzSharp.Buffer;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     Diagnostic tests that pinpoint exactly WHICH step in the text rendering pipeline fails.
///     Each test isolates one stage: glyph shaping, FreeType glyph loading, pixel blitting.
///     Run these to find the root cause of blank text output.
/// </summary>
public sealed class RenderingDiagnosticTests : RendererTestBase
{
    // xUnit v3 uses TestContext.Current for output instead of ITestOutputHelper constructor injection
    private static void Log(string msg) => TestContext.Current.TestOutputHelper?.WriteLine(msg);

    // ── Stage 1: does HarfBuzz produce any glyph infos? ──────────────────────
    // We access FontCache and HarfBuzz directly through a minimal shim PDF.

    [Fact]
    public async Task Stage1_EmbeddedFont_GetContentOperators_ContainsTj()
    {
        // Verify the content stream is parsed and Tj is present with a PdfString operand.
        SkipIfNoFreeType();

        var fontData = LoadDejaVuSansRegular();
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData, "BT /F1 24 Tf 100 600 Td (H) Tj ET");
        await using var doc = await LoadAsync(pdfBytes, TestContext.Current.CancellationToken);

        var ops = doc.Pages[1].GetContentOperators();
        Log($"Operators: {string.Join(", ", ops.Select(static o => o.Name))}");

        var tjOp = ops.FirstOrDefault(static o => o.Name == "Tj");
        tjOp.ShouldNotBeNull("Tj operator must be present in content stream");
        tjOp.Operands.Count.ShouldBe(1, "Tj must have exactly 1 operand (the string)");

        var str = tjOp.Operands[0] as PdfString;
        str.ShouldNotBeNull("Tj operand must be PdfString");
        Log($"Tj string bytes: [{string.Join(",", str.Bytes.ToArray())}]");
        str.Bytes.IsEmpty.ShouldBeFalse("Tj string must not be empty");
    }

    [Fact]
    public async Task Stage2_EmbeddedFont_FontMapAndEmbeddedBytesArePopulated()
    {
        // Verify GetFontNameMap() and GetEmbeddedFontBytes() return usable data.
        SkipIfNoFreeType();

        var fontData = LoadDejaVuSansRegular();
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData, "BT /F1 24 Tf 100 600 Td (H) Tj ET");
        await using var doc = await LoadAsync(pdfBytes, TestContext.Current.CancellationToken);

        var page = doc.Pages[1];
        var fontMap = page.GetFontNameMap();
        var embMap = page.GetEmbeddedFontBytes();

        Log($"FontMap keys: [{string.Join(", ", fontMap.Keys)}]");
        Log($"EmbMap keys: [{string.Join(", ", embMap.Keys)}]");

        fontMap.ShouldNotBeEmpty("GetFontNameMap() should return at least one font");
        fontMap.ContainsKey("F1").ShouldBeTrue("F1 must be in font map");

        embMap.ContainsKey("F1").ShouldBeTrue("F1 must be in embedded bytes map");
        var embBytes = embMap["F1"];
        embBytes.ShouldNotBeNull("Embedded font bytes for F1 must not be null");
        embBytes.Length.ShouldBeGreaterThan(1000, "Embedded font data should be substantial");
        Log($"Embedded font bytes length: {embBytes.Length}");
    }

    [Fact]
    public async Task Stage3_EmbeddedFont_TextMatrixIsPopulatedAfterBtTd()
    {
        // Render with a content stream that has diagnostics at different zoom levels.
        // We render a black rectangle (known to work) plus text, and compare.
        SkipIfNoFreeType();

        // Reference: black rect only (re f) — known to produce non-white pixels
        var rectPdf = PdfFixtures.WithImageXObject(200,
            50,
            Enumerable.Repeat((byte)0, 200 * 50 * 3).ToArray()); // all-black image
        await using var rectDoc = await LoadAsync(rectPdf, TestContext.Current.CancellationToken);
        var rectPng = await Renderer!.RenderPageAsync(rectDoc.Pages[1], new RenderOptions(72), TestContext.Current.CancellationToken);
        var rectPixels = PdfTestConstants.CountNonWhitePixels(rectPng);
        Log($"Black rect non-white pixels: {rectPixels}");

        // Text only
        var fontData = LoadDejaVuSansRegular();
        var textPdf = PdfFixtures.WithEmbeddedFont(fontData, "BT /F1 24 Tf 100 600 Td (H) Tj ET");
        await using var textDoc = await LoadAsync(textPdf, TestContext.Current.CancellationToken);
        var textPng = await Renderer!.RenderPageAsync(textDoc.Pages[1], new RenderOptions(72), TestContext.Current.CancellationToken);
        var textPixels = PdfTestConstants.CountNonWhitePixels(textPng);
        Log($"Text 'H' non-white pixels: {textPixels}");

        rectPixels.ShouldBeGreaterThan(100, "black rectangle must produce non-white pixels (sanity check)");
        textPixels.ShouldBeGreaterThan(20,
            $"'H' at 24pt must produce visible pixels. rect={rectPixels}, text={textPixels}");
    }

    [Fact]
    public async Task Stage4_EmbeddedFont_DifferentFontSizes()
    {
        // Try multiple font sizes to rule out a size-specific rendering issue.
        SkipIfNoFreeType();
        var fontData = LoadDejaVuSansRegular();

        foreach (var fontSize in new[] { 8, 12, 24, 48, 72 })
        {
            var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData,
                $"BT /F1 {fontSize} Tf 72 600 Td (HELLO) Tj ET");
            await using var doc = await LoadAsync(pdfBytes, TestContext.Current.CancellationToken);
            var png = await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(96), TestContext.Current.CancellationToken);
            var nonWhite = PdfTestConstants.CountNonWhitePixels(png);
            Log($"Font size {fontSize}pt → {nonWhite} non-white pixels");
        }

        // At 72pt we MUST see text pixels
        var bigPdf = PdfFixtures.WithEmbeddedFont(fontData,
            "BT /F1 72 Tf 50 500 Td (H) Tj ET");
        await using var bigDoc = await LoadAsync(bigPdf, TestContext.Current.CancellationToken);
        var bigPng = await Renderer!.RenderPageAsync(bigDoc.Pages[1], new RenderOptions(96), TestContext.Current.CancellationToken);
        var bigPixels = PdfTestConstants.CountNonWhitePixels(bigPng);

        bigPixels.ShouldBeGreaterThan(100,
            $"'H' at 72pt should be very visible. Got {bigPixels} non-white pixels.");
    }

    [Fact]
    public void Stage6_HarfBuzz_ShapingProducesGlyphInfos()
    {
        // Test HarfBuzz shaping in isolation: confirm it produces glyph IDs.
        SkipIfNoFreeType();

        var fontData = LoadDejaVuSansRegular();

        var gch = GCHandle.Alloc(fontData,
            GCHandleType.Pinned);
        try
        {
            using var blob = new Blob(gch.AddrOfPinnedObject(), fontData.Length, MemoryMode.Duplicate);
            using var hbFace = new Face(blob, 0);
            using var hbFont = new Font(hbFace);
            hbFont.SetScale(32 * 64, 32 * 64); // 32px * 64 (26.6 format)

            using var buf = new Buffer();
            buf.AddUtf8("H");
            buf.GuessSegmentProperties();
            hbFont.Shape(buf);

            var infos = buf.GlyphInfos;
            var positions = buf.GlyphPositions;

            Log($"Glyph count: {infos.Length}");
            for (var i = 0; i < infos.Length; i++)
                Log($"  [{i}] glyph={infos[i].Codepoint} xAdv={positions[i].XAdvance} yAdv={positions[i].YAdvance}");

            infos.Length.ShouldBe(1, "shaping 'H' should produce exactly 1 glyph");
            infos[0].Codepoint.ShouldNotBe(0u, "glyph ID must not be 0 (notdef)");
            positions[0].XAdvance.ShouldBeGreaterThan(0, "advance must be positive");
        }
        finally
        {
            gch.Free();
        }
    }

    // ── Stage 7: is ShowString throwing? ─────────────────────────────────────

    [Fact]
    public async Task Stage7_TextErrorCount_IsZeroMeansNoExceptionInShowString()
    {
        // PdfRenderer.LastTextErrors counts how many Tj/TJ operators threw.
        // 0 = ShowString ran without exception (though pixels may still be wrong).
        // >0 = ShowString threw, which is why text is blank.
        SkipIfNoFreeType();

        var fontData = LoadDejaVuSansRegular();
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData,
            "BT /F1 24 Tf 100 600 Td (Hello) Tj ET");
        await using var doc = await LoadAsync(pdfBytes, TestContext.Current.CancellationToken);
        await Renderer!.RenderPageAsync(doc.Pages[1],
            new RenderOptions(96),
            TestContext.Current.CancellationToken);

        var errors = Renderer.LastTextErrors;
        Log($"Text operator errors: {errors}");

        errors.ShouldBe(0,
            "Tj operator must not throw inside ShowString; if >0 the per-operator catch swallowed an exception");
    }

    // ── Stage 8: expose exactly what happens per glyph ───────────────────────

    [Fact]
    public async Task Stage7b_GlyphCounts_ConfirmWhereLoopBreaks()
    {
        // Distinguish between: (A) loop never runs (glyphInfos.Length=0), or
        // (B) loop runs but LoadGlyph throws (GlyphsSkipped > 0), or
        // (C) loop runs and LoadGlyph succeeds (GlyphsAttempted > 0) but BlitGlyphBitmap produces no pixels.
        SkipIfNoFreeType();

        var fontData = LoadDejaVuSansRegular();
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData,
            "BT /F1 24 Tf 100 600 Td (Hello) Tj ET");
        await using var doc = await LoadAsync(pdfBytes, TestContext.Current.CancellationToken);
        await Renderer!.RenderPageAsync(doc.Pages[1],
            new RenderOptions(96),
            TestContext.Current.CancellationToken);

        Log($"TextErrors:      {Renderer.LastTextErrors}");
        Log($"GlyphsAttempted: {Renderer.LastGlyphsAttempted}");
        Log($"GlyphsSkipped:   {Renderer.LastGlyphsSkipped}");

        // If GlyphsAttempted == 0 AND GlyphsSkipped == 0 AND TextErrors == 0:
        // → glyphInfos.Length == 0 (HarfBuzz produced no glyphs in ShowString)
        //
        // If GlyphsSkipped > 0:
        // → LoadGlyph threw for some/all glyphs
        //
        // If GlyphsAttempted > 0 but pixels == 0:
        // → BlitGlyphBitmap runs but writes nothing
        Renderer.LastGlyphsAttempted.ShouldBeGreaterThan(0,
            $"ShowString must call BlitGlyphBitmap for at least one glyph. " +
            $"TextErrors={Renderer.LastTextErrors}, Attempted={Renderer.LastGlyphsAttempted}, Skipped={Renderer.LastGlyphsSkipped}");
    }

    [Fact]
    public void Stage8_FontCache_DiagnoseGlyphRender_ReturnsOkForDejaVu()
    {
        // Uses FontCache.DiagnoseGlyphRender() which runs the exact same sequence
        // as ShowString: HarfBuzz shape → FreeType2 LoadGlyph → bitmap check.
        // Output pinpoints which step fails.
        SkipIfNoFreeType();

        var fontData = LoadDejaVuSansRegular();

        // Use PdfRenderer's FontCache (same instance ShowString uses).
        var diagnosis = Renderer!.FontsForDiagnostics.DiagnoseGlyphRender(
            "TestFont",
            fontData,
            'H',
            32); // ceil(24pt * 96dpi / 72)

        Log($"DiagnoseGlyphRender result: {diagnosis}");

        diagnosis.StartsWith("OK:", StringComparison.Ordinal).ShouldBeTrue(
            $"glyph render pipeline should complete successfully; got: {diagnosis}");
    }
}
