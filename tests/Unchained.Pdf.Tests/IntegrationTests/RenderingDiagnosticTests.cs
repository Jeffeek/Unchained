using System.IO.Compression;
using Shouldly;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
/// Diagnostic tests that pinpoint exactly WHICH step in the text rendering pipeline fails.
/// Each test isolates one stage: glyph shaping, FreeType glyph loading, pixel blitting.
/// Run these to find the root cause of blank text output.
/// </summary>
public sealed class RenderingDiagnosticTests : RendererTestBase
{
    // xUnit v3 uses TestContext.Current for output instead of ITestOutputHelper constructor injection
    private void Log(string msg) => TestContext.Current.TestOutputHelper?.WriteLine(msg);

    // ── Stage 1: does HarfBuzz produce any glyph infos? ──────────────────────
    // We access FontCache and HarfBuzz directly through a minimal shim PDF.

    [Fact]
    public async Task Stage1_EmbeddedFont_GetContentOperators_ContainsTj()
    {
        // Verify the content stream is parsed and Tj is present with a PdfString operand.
        SkipIfNoFreeType();

        var fontData = LoadDejaVuBytes();
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData, "BT /F1 24 Tf 100 600 Td (H) Tj ET");
        await using var doc = await LoadAsync(pdfBytes, ct: TestContext.Current.CancellationToken);

        var ops = doc.Pages[1].GetContentOperators();
        Log($"Operators: {string.Join(", ", ops.Select(o => o.Name))}");

        var tjOp = ops.FirstOrDefault(o => o.Name == "Tj");
        tjOp.ShouldNotBeNull("Tj operator must be present in content stream");
        tjOp.Operands.Count.ShouldBe(1, "Tj must have exactly 1 operand (the string)");

        var str = tjOp.Operands[0] as Unchained.Pdf.Core.PdfString;
        str.ShouldNotBeNull("Tj operand must be PdfString");
        Log($"Tj string bytes: [{string.Join(",", str.Bytes.ToArray())}]");
        str.Bytes.IsEmpty.ShouldBeFalse("Tj string must not be empty");
    }

    [Fact]
    public async Task Stage2_EmbeddedFont_FontMapAndEmbeddedBytesArePopulated()
    {
        // Verify GetFontNameMap() and GetEmbeddedFontBytes() return usable data.
        SkipIfNoFreeType();

        var fontData = LoadDejaVuBytes();
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData, "BT /F1 24 Tf 100 600 Td (H) Tj ET");
        await using var doc = await LoadAsync(pdfBytes, ct: TestContext.Current.CancellationToken);

        var page    = doc.Pages[1];
        var fontMap = page.GetFontNameMap();
        var embMap  = page.GetEmbeddedFontBytes();

        Log($"FontMap keys: [{string.Join(", ", fontMap.Keys)}]");
        Log($"EmbMap keys: [{string.Join(", ", embMap.Keys)}]");

        fontMap.ShouldNotBeEmpty("GetFontNameMap() should return at least one font");
        fontMap.ContainsKey("F1").ShouldBeTrue("F1 must be in font map");

        embMap.ContainsKey("F1").ShouldBeTrue("F1 must be in embedded bytes map");
        var embBytes = embMap["F1"];
        embBytes.ShouldNotBeNull("Embedded font bytes for F1 must not be null");
        embBytes!.Length.ShouldBeGreaterThan(1000, "Embedded font data should be substantial");
        Log($"Embedded font bytes length: {embBytes.Length}");
    }

    [Fact]
    public async Task Stage3_EmbeddedFont_TextMatrixIsPopulatedAfterBtTd()
    {
        // Render with a content stream that has diagnostics at different zoom levels.
        // We render a black rectangle (known to work) plus text, and compare.
        SkipIfNoFreeType();

        // Reference: black rect only (re f) — known to produce non-white pixels
        var rectPdf = PdfFixtures.WithImageXObject(200, 50,
            Enumerable.Repeat((byte)0, 200 * 50 * 3).ToArray()); // all-black image
        await using var rectDoc = await LoadAsync(rectPdf, ct: TestContext.Current.CancellationToken);
        var rectPng     = await Renderer!.RenderPageAsync(rectDoc.Pages[1], new RenderOptions(Dpi: 72), ct: TestContext.Current.CancellationToken);
        var rectPixels  = CountNonWhitePixels(rectPng);
        Log($"Black rect non-white pixels: {rectPixels}");

        // Text only
        var fontData = LoadDejaVuBytes();
        var textPdf  = PdfFixtures.WithEmbeddedFont(fontData, "BT /F1 24 Tf 100 600 Td (H) Tj ET");
        await using var textDoc = await LoadAsync(textPdf, ct: TestContext.Current.CancellationToken);
        var textPng     = await Renderer!.RenderPageAsync(textDoc.Pages[1], new RenderOptions(Dpi: 72), ct: TestContext.Current.CancellationToken);
        var textPixels  = CountNonWhitePixels(textPng);
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
        var fontData = LoadDejaVuBytes();

        foreach (var fontSize in new[] { 8, 12, 24, 48, 72 })
        {
            var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData,
                $"BT /F1 {fontSize} Tf 72 600 Td (HELLO) Tj ET");
            await using var doc = await LoadAsync(pdfBytes, ct: TestContext.Current.CancellationToken);
            var png      = await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(Dpi: 96), ct: TestContext.Current.CancellationToken);
            var nonWhite = CountNonWhitePixels(png);
            Log($"Font size {fontSize}pt → {nonWhite} non-white pixels");
        }

        // At 72pt we MUST see text pixels
        var bigPdf = PdfFixtures.WithEmbeddedFont(fontData,
            "BT /F1 72 Tf 50 500 Td (H) Tj ET");
        await using var bigDoc = await LoadAsync(bigPdf, ct: TestContext.Current.CancellationToken);
        var bigPng    = await Renderer!.RenderPageAsync(bigDoc.Pages[1], new RenderOptions(Dpi: 96), ct: TestContext.Current.CancellationToken);
        var bigPixels = CountNonWhitePixels(bigPng);

        bigPixels.ShouldBeGreaterThan(100,
            $"'H' at 72pt should be very visible. Got {bigPixels} non-white pixels.");
    }

    [Fact]
    public void Stage6_HarfBuzz_ShapingProducesGlyphInfos()
    {
        // Test HarfBuzz shaping in isolation: confirm it produces glyph IDs.
        SkipIfNoFreeType();

        var fontData = LoadDejaVuBytes();

        var gch = System.Runtime.InteropServices.GCHandle.Alloc(fontData,
            System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            using var blob   = new HarfBuzzSharp.Blob(gch.AddrOfPinnedObject(), fontData.Length, HarfBuzzSharp.MemoryMode.Duplicate);
            using var hbFace = new HarfBuzzSharp.Face(blob, 0);
            using var hbFont = new HarfBuzzSharp.Font(hbFace);
            hbFont.SetScale(32 * 64, 32 * 64); // 32px * 64 (26.6 format)

            using var buf = new HarfBuzzSharp.Buffer();
            buf.AddUtf8("H");
            buf.GuessSegmentProperties();
            hbFont.Shape(buf);

            var infos     = buf.GlyphInfos;
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

        var fontData = LoadDejaVuBytes();
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData,
            "BT /F1 24 Tf 100 600 Td (Hello) Tj ET");
        await using var doc = await LoadAsync(pdfBytes, ct: TestContext.Current.CancellationToken);
        await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(Dpi: 96),
            ct: TestContext.Current.CancellationToken);

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

        var fontData = LoadDejaVuBytes();
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData,
            "BT /F1 24 Tf 100 600 Td (Hello) Tj ET");
        await using var doc = await LoadAsync(pdfBytes, ct: TestContext.Current.CancellationToken);
        await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(Dpi: 96),
            ct: TestContext.Current.CancellationToken);

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
    public async Task Stage7c_GlyphPixelsWritten_ConfirmsBlitGlyphBitmapWritesPixels()
    {
        // If GlyphsAttempted > 0 but GlyphPixelsWritten == 0, BlitGlyphBitmap runs
        // but writes nothing — the bug is inside that function.
        SkipIfNoFreeType();

        var fontData = LoadDejaVuBytes();
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData,
            "BT /F1 24 Tf 100 600 Td (Hello) Tj ET");
        await using var doc = await LoadAsync(pdfBytes, ct: TestContext.Current.CancellationToken);
        await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(Dpi: 96),
            ct: TestContext.Current.CancellationToken);

        Log($"GlyphsAttempted:    {Renderer.LastGlyphsAttempted}");
        Log($"GlyphsSkipped:      {Renderer.LastGlyphsSkipped}");
        Log($"GlyphPixelsWritten: {Renderer.LastGlyphPixelsWritten}");

        Renderer.LastGlyphPixelsWritten.ShouldBeGreaterThan(0,
            $"BlitGlyphBitmap must write at least 1 non-transparent pixel. " +
            $"Attempted={Renderer.LastGlyphsAttempted}, Skipped={Renderer.LastGlyphsSkipped}, " +
            $"PixelsWritten={Renderer.LastGlyphPixelsWritten}");
    }

    [Fact]
    public async Task Stage7d_WhyCounters_PinpointExactBlitGlyphBitmapFailure()
    {
        // After Stage7c confirmed GlyphPixelsWritten==0, this test reveals WHICH guard
        // inside BlitGlyphBitmap is responsible (zero dims / null buf / zero pitch /
        // unknown pixel mode / all-alpha-zero) and dumps LastGlyphDiag for further insight.
        SkipIfNoFreeType();

        var fontData = LoadDejaVuBytes();
        var pdfBytes = PdfFixtures.WithEmbeddedFont(fontData,
            "BT /F1 24 Tf 100 600 Td (Hello) Tj ET");
        await using var doc = await LoadAsync(pdfBytes, ct: TestContext.Current.CancellationToken);
        await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(Dpi: 96),
            ct: TestContext.Current.CancellationToken);

        Log($"GlyphsAttempted:      {Renderer.LastGlyphsAttempted}");
        Log($"GlyphsSkipped:        {Renderer.LastGlyphsSkipped}");
        Log($"GlyphPixelsWritten:   {Renderer.LastGlyphPixelsWritten}");
        Log($"ZeroDims:             {Renderer.LastGlyphZeroDims}");
        Log($"NullBuf:              {Renderer.LastGlyphNullBuf}");
        Log($"ZeroPitch:            {Renderer.LastGlyphZeroPitch}");
        Log($"UnknownMode:          {Renderer.LastGlyphUnknownMode}");
        Log($"AllAlphaZero:         {Renderer.LastGlyphAllAlphaZero}");
        var d = Renderer.LastGlyphDiag;
        Log($"LastGlyphDiag:        W={d.W} H={d.H} Pitch={d.Pitch} Mode={d.PixelMode} NonZeroAlpha={d.NonZeroAlpha}");
        var b = Renderer.LastBitmapAfterLoad;
        Log($"LastBitmapAfterLoad:  W={b.W} H={b.H} Pitch={b.Pitch} Mode={b.Mode} Left={b.Left} Top={b.Top}");

        // Assert: pixels must be written. If this fails the failure message names the counter.
        var failReason = Renderer.LastGlyphZeroDims     > 0 ? $"ZeroDims={Renderer.LastGlyphZeroDims}"
                       : Renderer.LastGlyphNullBuf      > 0 ? $"NullBuf={Renderer.LastGlyphNullBuf}"
                       : Renderer.LastGlyphZeroPitch    > 0 ? $"ZeroPitch={Renderer.LastGlyphZeroPitch}"
                       : Renderer.LastGlyphUnknownMode  > 0 ? $"UnknownMode={Renderer.LastGlyphUnknownMode} mode={d.PixelMode}"
                       : Renderer.LastGlyphAllAlphaZero > 0 ? $"AllAlphaZero={Renderer.LastGlyphAllAlphaZero} (all rowBuf bytes were 0)"
                       : "unknown — no WHY-counter incremented";

        Renderer.LastGlyphPixelsWritten.ShouldBeGreaterThan(0,
            $"BlitGlyphBitmap wrote 0 pixels. Root cause: {failReason}. " +
            $"Diag: W={d.W} H={d.H} Pitch={d.Pitch} Mode={d.PixelMode} NonZeroAlpha={d.NonZeroAlpha}");
    }

    [Fact]
    public void Stage8_FontCache_DiagnoseGlyphRender_ReturnsOkForDejaVu()
    {
        // Uses FontCache.DiagnoseGlyphRender() which runs the exact same sequence
        // as ShowString: HarfBuzz shape → FreeType2 LoadGlyph → bitmap check.
        // Output pinpoints which step fails.
        SkipIfNoFreeType();

        var fontData = LoadDejaVuBytes();

        // Use PdfRenderer's FontCache (same instance ShowString uses).
        var diagnosis = Renderer!.FontsForDiagnostics.DiagnoseGlyphRender(
            fontName:      "TestFont",
            embeddedBytes: fontData,
            ch:            'H',
            pixelSize:     32);   // ceil(24pt * 96dpi / 72)

        Log($"DiagnoseGlyphRender result: {diagnosis}");

        diagnosis.StartsWith("OK:").ShouldBeTrue(
            $"glyph render pipeline should complete successfully; got: {diagnosis}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static byte[] LoadDejaVuBytes()
    {
        var asm = typeof(Unchained.Pdf.Rendering.Engine.PdfRenderer).Assembly;
        using var s  = asm.GetManifestResourceStream(
            "Unchained.Pdf.Rendering.Rendering.Fonts.DejaVuSans-Regular.ttf")!;
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private static int CountNonWhitePixels(byte[] png)
    {
        var width  = (int)ReadUInt32BE(png, 16);
        var height = (int)ReadUInt32BE(png, 20);
        var idatLen = (int)ReadUInt32BE(png, 33);
        var idat    = png.AsSpan(33 + 8, idatLen).ToArray();
        using var cms = new MemoryStream(idat);
        using var dec = new MemoryStream();
        using (var z = new ZLibStream(cms, CompressionMode.Decompress)) z.CopyTo(dec);
        var raw    = dec.ToArray();
        var stride = 1 + (width * 4); // RGBA color type 6
        var count  = 0;
        for (var y = 0; y < height; y++)
        {
            var row = y * stride + 1;
            for (var x = 0; x < width; x++)
            {
                if (raw[row + x*4] < 255 || raw[row + x*4+1] < 255 || raw[row + x*4+2] < 255)
                    count++;
            }
        }
        return count;
    }

    private static uint ReadUInt32BE(byte[] d, int o) =>
        ((uint)d[o]<<24)|((uint)d[o+1]<<16)|((uint)d[o+2]<<8)|d[o+3];
}
