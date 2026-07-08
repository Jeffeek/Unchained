using System.Text;
using Shouldly;
using Unchained.Pdf.Models;
using Unchained.Pdf.Rendering.Tests.Helpers;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Rendering.Tests.IntegrationTests;

/// <summary>
///     Operator-dispatch branch coverage for <c>PageRenderer.cs</c>: the K (stroke CMYK), v/y
///     curve, b*/b close-fill-stroke, TD/'/" text operators, the scn pattern-with-components
///     heuristic (1/3/4 numeric operands), the per-operator text-error catch, uncoloured
///     (PaintType 2) tiling patterns (SetInitialFillColor), and sc/scn over named colour spaces
///     (ResolveColorComponents device + named arms). Drives everything through <c>PdfRenderer</c>.
/// </summary>
public sealed class PageRendererDispatchTests : RendererTestBase
{
    private static byte[] Page100(string content, string extraResources = "")
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
        PdfFixtures.Ln(sb, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R /Resources << {extraResources} >> >>");
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

    private async Task<int> NonWhiteAsync(byte[] pdf)
    {
        await using var doc = await LoadAsync(pdf, TestContext.Current.CancellationToken);
        var png = await Renderer.RenderPageAsync(doc.Pages[1], new RenderOptions(96), TestContext.Current.CancellationToken);
        return PdfTestConstants.CountNonWhitePixels(png);
    }

    [Fact]
    public async Task StrokeCmyk_K_Operator_Paints()
    {
        // K = DeviceCMYK stroke colour; pure magenta stroke on a thick line.
        var n = await NonWhiteAsync(Page100("0 1 0 0 K 5 w 10 10 m 90 90 l S"));
        n.ShouldBeGreaterThan(20);
    }

    [Fact]
    public async Task VCurveOperator_Paints()
    {
        // v: first control point = current point.
        var n = await NonWhiteAsync(Page100("0 0 0 rg 10 50 m 40 90 90 50 v f"));
        n.ShouldBeGreaterThan(100);
    }

    [Fact]
    public async Task YCurveOperator_Paints()
    {
        // y: second control point = end point.
        var n = await NonWhiteAsync(Page100("0 0 0 rg 10 50 m 40 90 90 50 y f"));
        n.ShouldBeGreaterThan(100);
    }

    [Fact]
    public async Task CloseFillStroke_LowerB_Star_Paints()
    {
        // b* = close + even-odd fill + stroke.
        var n = await NonWhiteAsync(Page100("0 0 0 rg 0 0 0 RG 2 w 10 10 m 90 10 l 50 90 l b*"));
        n.ShouldBeGreaterThan(100);
    }

    [Fact]
    public async Task CloseFillStroke_LowerB_Paints()
    {
        // b = close + nonzero fill + stroke.
        var n = await NonWhiteAsync(Page100("0 0 0 rg 0 0 0 RG 2 w 10 10 m 90 10 l 50 90 l b"));
        n.ShouldBeGreaterThan(100);
    }

    [Fact]
    public async Task FillStroke_UpperB_Star_Paints()
    {
        var n = await NonWhiteAsync(Page100("0 0 0 rg 0 0 0 RG 2 w 10 10 50 50 re 20 20 20 20 re B*"));
        n.ShouldBeGreaterThan(100);
    }

    [Fact]
    public async Task TextLine_TD_Quote_DoubleQuote_Render()
    {
        var fontData = LoadDejaVuSansRegular();
        // TD sets leading + moves; ' shows on next line; " sets word/char spacing then shows.
        var pdf = PdfFixtures.WithEmbeddedFont(
            fontData,
            "BT /F1 14 Tf 10 80 Td 12 TL (Line1) Tj 0 -14 TD (Line2) ' 1 1 (Line3) \" ET"
        );
        var n = await NonWhiteAsync(pdf);
        n.ShouldBeGreaterThan(40);
    }

    [Fact]
    public async Task ScnPatternWithComponents_GrayHeuristic_Paints()
    {
        // /Pattern cs then scn with ONE numeric operand + a pattern name → nums.Count==1
        // heuristic (SetFillGray) for an unknown pattern (name not declared in resources).
        const string content = "/Pattern cs 0.3 /P1 scn 0 0 100 100 re f";
        var n = await NonWhiteAsync(Page100(content));
        // Unknown pattern → grey approximation; should still paint a large area (not pure white).
        n.ShouldBeGreaterThan(1000);
    }

    [Fact]
    public async Task ScnWithThreeComponents_RgbHeuristic_Paints()
    {
        // scn with 3 numeric operands + pattern name → nums.Count==3 RGB heuristic.
        const string content = "/Pattern cs 1 0 0 /P1 scn 0 0 100 100 re f";
        var n = await NonWhiteAsync(Page100(content));
        n.ShouldBeGreaterThan(1000);
    }

    [Fact]
    public async Task ScnWithFourComponents_CmykHeuristic_Paints()
    {
        // scn with 4 numeric operands + pattern name → nums.Count==4 CMYK heuristic.
        const string content = "/Pattern cs 0 1 1 0 /P1 scn 0 0 100 100 re f";
        var n = await NonWhiteAsync(Page100(content));
        n.ShouldBeGreaterThan(1000);
    }

    [Fact]
    public async Task ScColorOverNamedSeparation_Paints()
    {
        // Named Separation cs + sc → ResolveColorComponents takes the named-space arm.
        await using var doc = await LoadAsync(PdfFixtures.WithSeparationColorSpace(), TestContext.Current.CancellationToken);
        var png = await Renderer.RenderPageAsync(doc.Pages[1], new RenderOptions(96), TestContext.Current.CancellationToken);
        PdfTestConstants.CountNonWhitePixels(png).ShouldBeGreaterThan(1000);
    }

    [Fact]
    public async Task UncolouredTilingPattern_PaintType2_UsesFillColor()
    {
        await using var doc = await LoadAsync(UncolouredTilingPdf(), TestContext.Current.CancellationToken);
        var png = await Renderer.RenderPageAsync(doc.Pages[1], new RenderOptions(96), TestContext.Current.CancellationToken);
        PdfTestConstants.CountNonWhitePixels(png).ShouldBeGreaterThan(100);
    }

    [Fact]
    public async Task ScSingleComponent_OverUnknownNamedSpace_FallsBackToGrayHeuristic()
    {
        // cs names a space not present in /ColorSpace resources → ResolveColorComponents
        // falls into the "colorSpaces null or missing" component-count heuristic (1 → gray).
        const string content = "/Unknown cs 0.5 sc 0 0 100 100 re f";
        var n = await NonWhiteAsync(Page100(content));
        n.ShouldBeGreaterThan(1000);
    }

    [Fact]
    public async Task BadTextOperator_IsCaughtAndCounted()
    {
        // A Tj referencing an undefined font resource forces ShowString down a path that may
        // throw; the per-operator catch must swallow it and continue rendering the rectangle.
        var n = await NonWhiteAsync(Page100("BT /Missing 12 Tf 10 50 Td (X) Tj ET 0 0 0 rg 10 10 30 30 re f"));
        n.ShouldBeGreaterThan(100);
    }

    // A 100×100 page whose page rect is painted with an UNCOLOURED (PaintType 2) tiling pattern.
    // Each cell strokes a diagonal; the parent's fill colour (red) is used for the ink.
    private static byte[] UncolouredTilingPdf()
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
        PdfFixtures.Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R");
        PdfFixtures.Ln(sb, "   /Resources << /Pattern << /P1 5 0 R >> >> >>");
        PdfFixtures.Ln(sb, "endobj");
        // Set an uncoloured Pattern colour space with DeviceRGB base; select red, fill page.
        const string content = "/Pattern cs 1 0 0 /P1 scn 0 0 100 100 re f";
        var cb = Encoding.Latin1.GetBytes(content);
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "4 0 obj");
        PdfFixtures.Ln(sb, $"<< /Length {cb.Length} >>");
        sb.Append("stream\n").Append(content);
        PdfFixtures.Ln(sb, "\nendstream");
        PdfFixtures.Ln(sb, "endobj");
        // PaintType 2 (uncoloured) tiling pattern cell: a filled square, no colour of its own.
        const string cell = "2 2 6 6 re f";
        var cellBytes = Encoding.Latin1.GetBytes(cell);
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "5 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Pattern /PatternType 1 /PaintType 2 /TilingType 1");
        PdfFixtures.Ln(sb, "   /BBox [0 0 10 10] /XStep 10 /YStep 10 /Resources << >>");
        PdfFixtures.Ln(sb, $"   /Length {cellBytes.Length} >>");
        sb.Append("stream\n").Append(cell);
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
}
