using System.Text;
using Shouldly;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     Rendering tests that drive the <c>PageRenderer</c> partial classes (text modes, path
///     fills/strokes, radial/mesh shadings, dash patterns, CMYK and gray colour operators) through
///     the public <c>PdfRenderer</c>. Each verifies non-trivial pixel output rather than just PNG
///     validity. Skips when FreeType2 is unavailable.
/// </summary>
public sealed class PageRendererContentTests : RendererTestBase
{
    private static byte[] Page100(string content)
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();

        PdfFixtures.Ln(sb, "%PDF-1.7");
        PdfFixtures.Ln(sb, "%\xE2\xE3\xCF\xD3");
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "1 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        PdfFixtures.Ln(sb, "endobj");
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "2 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        PdfFixtures.Ln(sb, "endobj");
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "3 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R /Resources << >> >>");
        PdfFixtures.Ln(sb, "endobj");
        var cb = Encoding.Latin1.GetBytes(content);
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "4 0 obj");
        PdfFixtures.Ln(sb, $"<< /Length {cb.Length} >>");
        sb.Append("stream\n").Append(content);
        PdfFixtures.Ln(sb, "\nendstream");
        PdfFixtures.Ln(sb, "endobj");
        var xref = PdfFixtures.Len(sb);
        PdfFixtures.Ln(sb, "xref");
        PdfFixtures.Ln(sb, "0 5");
        PdfFixtures.Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) PdfFixtures.Ln(sb, $"{o:D10} 00000 n ");
        PdfFixtures.Ln(sb, "trailer");
        PdfFixtures.Ln(sb, "<< /Size 5 /Root 1 0 R >>");
        PdfFixtures.Ln(sb, "startxref");
        PdfFixtures.Ln(sb, xref.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    private async Task<int> NonWhiteAsync(byte[] pdf, int dpi = 96)
    {
        await using var doc = await LoadAsync(pdf, TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(dpi), TestContext.Current.CancellationToken);
        return PdfTestConstants.CountNonWhitePixels(png);
    }

    // ── Path fills (PageRenderer.Paths) ───────────────────────────────────────

    [Fact]
    public async Task FilledRectangle_ProducesNonWhitePixels()
    {
        SkipIfNoFreeType();
        var n = await NonWhiteAsync(Page100("0 0 0 rg 10 10 80 80 re f"));
        n.ShouldBeGreaterThan(1000);
    }

    [Fact]
    public async Task StrokedRectangle_ProducesNonWhitePixels()
    {
        SkipIfNoFreeType();
        var n = await NonWhiteAsync(Page100("0 0 0 RG 3 w 10 10 80 80 re S"));
        n.ShouldBeGreaterThan(50);
    }

    [Fact]
    public async Task StrokedLine_WithRoundCaps_ProducesNonWhitePixels()
    {
        SkipIfNoFreeType();
        // J 1 = round cap, j 1 = round join.
        var n = await NonWhiteAsync(Page100("0 0 0 RG 5 w 1 J 1 j 10 10 m 90 90 l S"));
        n.ShouldBeGreaterThan(50);
    }

    [Fact]
    public async Task DashedLine_ProducesNonWhitePixels()
    {
        SkipIfNoFreeType();
        var n = await NonWhiteAsync(Page100("0 0 0 RG 4 w [6 4] 0 d 5 50 m 95 50 l S"));
        n.ShouldBeGreaterThan(20);
    }

    [Fact]
    public async Task BezierCurveFill_ProducesNonWhitePixels()
    {
        SkipIfNoFreeType();
        var n = await NonWhiteAsync(Page100("0 0 0 rg 10 50 m 30 90 70 90 90 50 c 70 10 30 10 10 50 c f"));
        n.ShouldBeGreaterThan(500);
    }

    [Fact]
    public async Task EvenOddFill_ProducesNonWhitePixels()
    {
        SkipIfNoFreeType();
        // Two nested rectangles filled with the even-odd rule (f*) → ring shape.
        var n = await NonWhiteAsync(Page100("0 0 0 rg 10 10 80 80 re 30 30 40 40 re f*"));
        n.ShouldBeGreaterThan(500);
    }

    // ── Colour operators (PageRenderer.cs) ────────────────────────────────────

    [Fact]
    public async Task CmykFill_ProducesNonWhitePixels()
    {
        SkipIfNoFreeType();
        // k = DeviceCMYK fill; pure cyan.
        var n = await NonWhiteAsync(Page100("1 0 0 0 k 0 0 100 100 re f"));
        n.ShouldBeGreaterThan(5000);
    }

    [Fact]
    public async Task GrayFill_ProducesNonWhitePixels()
    {
        SkipIfNoFreeType();
        var n = await NonWhiteAsync(Page100("0.5 g 0 0 100 100 re f"));
        n.ShouldBeGreaterThan(5000);
    }

    [Fact]
    public async Task ClippedFill_RestrictsPaintedArea()
    {
        SkipIfNoFreeType();
        // Clip to a 20×20 box, then fill the whole page black: only the clip should paint.
        var clipped = await NonWhiteAsync(Page100("20 20 20 20 re W n 0 0 0 rg 0 0 100 100 re f"));
        var unclipped = await NonWhiteAsync(Page100("0 0 0 rg 0 0 100 100 re f"));
        clipped.ShouldBeLessThan(unclipped);
        clipped.ShouldBeGreaterThan(0);
    }

    // ── Text render modes (PageRenderer.Text) ─────────────────────────────────

    [Fact]
    public async Task TextStrokeMode_ProducesNonWhitePixels()
    {
        SkipIfNoFreeType();
        var fontData = LoadDejaVuSansRegular();
        // Tr 1 = stroke text.
        var pdf = PdfFixtures.WithEmbeddedFont(fontData, "BT /F1 40 Tf 1 Tr 0 0 0 RG 2 w 10 40 Td (R) Tj ET");
        var n = await NonWhiteAsync(pdf);
        n.ShouldBeGreaterThan(20);
    }

    [Fact]
    public async Task TextFillStrokeMode_ProducesNonWhitePixels()
    {
        SkipIfNoFreeType();
        var fontData = LoadDejaVuSansRegular();
        // Tr 2 = fill then stroke.
        var pdf = PdfFixtures.WithEmbeddedFont(fontData, "BT /F1 40 Tf 2 Tr 0 0 0 rg 0 0 0 RG 1 w 10 40 Td (B) Tj ET");
        var n = await NonWhiteAsync(pdf);
        n.ShouldBeGreaterThan(20);
    }

    [Fact]
    public async Task TextWithRiseAndCharSpacing_Renders()
    {
        SkipIfNoFreeType();
        var fontData = LoadDejaVuSansRegular();
        var pdf = PdfFixtures.WithEmbeddedFont(fontData, "BT /F1 18 Tf 2 Tc 4 Ts 10 50 Td (Hi) Tj ET");
        var n = await NonWhiteAsync(pdf);
        n.ShouldBeGreaterThan(20);
    }

    [Fact]
    public async Task TextWithTJArray_Renders()
    {
        SkipIfNoFreeType();
        var fontData = LoadDejaVuSansRegular();
        var pdf = PdfFixtures.WithEmbeddedFont(fontData, "BT /F1 20 Tf 10 50 Td [(A) -200 (B) -200 (C)] TJ ET");
        var n = await NonWhiteAsync(pdf);
        n.ShouldBeGreaterThan(40);
    }

    // ── Radial shading (PageRenderer.Shading) ─────────────────────────────────

    [Fact]
    public async Task RadialShading_ProducesNonWhitePixels()
    {
        SkipIfNoFreeType();
        var n = await NonWhiteAsync(RadialShadingPdf());
        n.ShouldBeGreaterThan(1000);
    }

    [Fact]
    public async Task ShadingPatternFill_ProducesNonWhitePixels()
    {
        SkipIfNoFreeType();
        // /Pattern cs + scn with a PatternType-2 (shading) pattern fills the path with a gradient.
        var n = await NonWhiteAsync(ShadingPatternPdf());
        n.ShouldBeGreaterThan(1000);
    }

    [Fact]
    public async Task TilingPatternFill_ProducesNonWhitePixels()
    {
        SkipIfNoFreeType();
        // Reuses the shared tiling-pattern fixture; drives PaintTilingInPathBounds.
        await using var doc = await LoadAsync(PdfFixtures.WithTilingPattern(), TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(96), TestContext.Current.CancellationToken);
        PdfTestConstants.CountNonWhitePixels(png).ShouldBeGreaterThan(500);
    }

    [Fact]
    public async Task MeshShading_Type4_ProducesNonWhitePixels()
    {
        SkipIfNoFreeType();
        var n = await NonWhiteAsync(MeshShadingPdf());
        n.ShouldBeGreaterThan(200);
    }

    private static byte[] ShadingPatternPdf()
    {
        const string content = "/Pattern cs /P1 scn 0 0 100 100 re f";
        var sb = new StringBuilder();
        var offsets = new List<int>();

        PdfFixtures.Ln(sb, "%PDF-1.7");
        PdfFixtures.Ln(sb, "%\xE2\xE3\xCF\xD3");
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "1 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        PdfFixtures.Ln(sb, "endobj");
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "2 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        PdfFixtures.Ln(sb, "endobj");
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "3 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R");
        PdfFixtures.Ln(sb, "   /Resources << /Pattern << /P1 5 0 R >> >> >>");
        PdfFixtures.Ln(sb, "endobj");
        var cb = Encoding.Latin1.GetBytes(content);
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "4 0 obj");
        PdfFixtures.Ln(sb, $"<< /Length {cb.Length} >>");
        sb.Append("stream\n").Append(content);
        PdfFixtures.Ln(sb, "\nendstream");
        PdfFixtures.Ln(sb, "endobj");
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "5 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Pattern /PatternType 2 /Shading");
        PdfFixtures.Ln(sb, "   << /ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 100 100] /Domain [0 1]");
        PdfFixtures.Ln(sb, "      /Function << /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [0 0 1] /N 1 >>");
        PdfFixtures.Ln(sb, "      /Extend [true true] >> >>");
        PdfFixtures.Ln(sb, "endobj");
        var xref = PdfFixtures.Len(sb);
        PdfFixtures.Ln(sb, "xref");
        PdfFixtures.Ln(sb, "0 6");
        PdfFixtures.Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) PdfFixtures.Ln(sb, $"{o:D10} 00000 n ");
        PdfFixtures.Ln(sb, "trailer");
        PdfFixtures.Ln(sb, "<< /Size 6 /Root 1 0 R >>");
        PdfFixtures.Ln(sb, "startxref");
        PdfFixtures.Ln(sb, xref.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    private static byte[] MeshShadingPdf()
    {
        // Type-4 free-form Gouraud mesh: one triangle filling most of the page, 8-bit fields.
        // Data per vertex: flag(1) + x(1) + y(1) + r(1) + g(1) + b(1).
        byte[] mesh =
        [
            0, 0, 0, 255, 0, 0,    // v0 (0,0) red
            0, 255, 0, 0, 255, 0,  // v1 (100,0) green
            0, 128, 255, 0, 0, 255 // v2 (50,100) blue
        ];

        var sb = new StringBuilder();
        var offsets = new List<int>();

        PdfFixtures.Ln(sb, "%PDF-1.7");
        PdfFixtures.Ln(sb, "%\xE2\xE3\xCF\xD3");
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "1 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        PdfFixtures.Ln(sb, "endobj");
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "2 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        PdfFixtures.Ln(sb, "endobj");
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "3 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R");
        PdfFixtures.Ln(sb, "   /Resources << /Shading << /Sh1 5 0 R >> >> >>");
        PdfFixtures.Ln(sb, "endobj");
        const string content = "q 0 0 100 100 re W n /Sh1 sh Q";
        var cb = Encoding.Latin1.GetBytes(content);
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "4 0 obj");
        PdfFixtures.Ln(sb, $"<< /Length {cb.Length} >>");
        sb.Append("stream\n").Append(content);
        PdfFixtures.Ln(sb, "\nendstream");
        PdfFixtures.Ln(sb, "endobj");
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "5 0 obj");
        PdfFixtures.Ln(sb, "<< /ShadingType 4 /ColorSpace /DeviceRGB /BitsPerCoordinate 8 /BitsPerComponent 8");
        PdfFixtures.Ln(sb, "   /BitsPerFlag 8 /Decode [0 100 0 100 0 1 0 1 0 1]");
        PdfFixtures.Ln(sb, $"   /Length {mesh.Length} >>");
        sb.Append("stream\n");
        foreach (var b in mesh) sb.Append((char)b);
        PdfFixtures.Ln(sb, "\nendstream");
        PdfFixtures.Ln(sb, "endobj");
        var xref = PdfFixtures.Len(sb);
        PdfFixtures.Ln(sb, "xref");
        PdfFixtures.Ln(sb, "0 6");
        PdfFixtures.Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) PdfFixtures.Ln(sb, $"{o:D10} 00000 n ");
        PdfFixtures.Ln(sb, "trailer");
        PdfFixtures.Ln(sb, "<< /Size 6 /Root 1 0 R >>");
        PdfFixtures.Ln(sb, "startxref");
        PdfFixtures.Ln(sb, xref.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    private static byte[] RadialShadingPdf()
    {
        const string content = "q 0 0 100 100 re W n /Sh1 sh Q";
        var sb = new StringBuilder();
        var offsets = new List<int>();

        PdfFixtures.Ln(sb, "%PDF-1.7");
        PdfFixtures.Ln(sb, "%\xE2\xE3\xCF\xD3");
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "1 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        PdfFixtures.Ln(sb, "endobj");
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "2 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        PdfFixtures.Ln(sb, "endobj");
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "3 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R");
        PdfFixtures.Ln(sb, "   /Resources << /Shading << /Sh1 5 0 R >> >> >>");
        PdfFixtures.Ln(sb, "endobj");
        var cb = Encoding.Latin1.GetBytes(content);
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "4 0 obj");
        PdfFixtures.Ln(sb, $"<< /Length {cb.Length} >>");
        sb.Append("stream\n").Append(content);
        PdfFixtures.Ln(sb, "\nendstream");
        PdfFixtures.Ln(sb, "endobj");
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "5 0 obj");
        PdfFixtures.Ln(sb, "<< /ShadingType 3 /ColorSpace /DeviceRGB /Coords [50 50 0 50 50 50] /Domain [0 1]");
        PdfFixtures.Ln(sb, "   /Function << /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [0 0 1] /N 1 >>");
        PdfFixtures.Ln(sb, "   /Extend [true true] >>");
        PdfFixtures.Ln(sb, "endobj");
        var xref = PdfFixtures.Len(sb);
        PdfFixtures.Ln(sb, "xref");
        PdfFixtures.Ln(sb, "0 6");
        PdfFixtures.Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) PdfFixtures.Ln(sb, $"{o:D10} 00000 n ");
        PdfFixtures.Ln(sb, "trailer");
        PdfFixtures.Ln(sb, "<< /Size 6 /Root 1 0 R >>");
        PdfFixtures.Ln(sb, "startxref");
        PdfFixtures.Ln(sb, xref.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }
}
