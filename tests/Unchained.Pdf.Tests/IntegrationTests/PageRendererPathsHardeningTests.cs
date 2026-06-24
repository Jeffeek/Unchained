using System.Text;
using Shouldly;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     Hardening branch coverage for <c>PageRenderer.Paths.cs</c>: bevel and miter line joins
///     (and the miter-limit bevel fallback), soft-masked polygon span fills, the pattern-fill
///     grey approximation, zero-length dash segments, an <c>l</c> before any <c>m</c>, and a
///     degenerate single-point clip subpath. Drives everything through <c>PdfRenderer</c>.
/// </summary>
public sealed class PageRendererPathsHardeningTests : RendererTestBase
{
    private static byte[] Page100(string content, string resources = "")
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
        PdfFixtures.Ln(sb, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R /Resources << {resources} >> >>");
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
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(96), TestContext.Current.CancellationToken);
        return PdfTestConstants.CountNonWhitePixels(png);
    }

    [Fact]
    public async Task BevelJoin_ThickPolyline_Paints()
    {
        SkipIfNoFreeType();
        // j 2 = bevel join, thick stroke, 3 vertices → interior join rendered.
        var n = await NonWhiteAsync(Page100("0 0 0 RG 6 w 2 j 10 10 m 50 80 l 90 10 l S"));
        n.ShouldBeGreaterThan(50);
    }

    [Fact]
    public async Task RoundJoin_ThickPolyline_Paints()
    {
        SkipIfNoFreeType();
        // j 1 = round join → FillCircle at the interior vertex (case 1 of DrawLineJoin).
        var n = await NonWhiteAsync(Page100("0 0 0 RG 8 w 1 j 10 10 m 50 80 l 90 10 l S"));
        n.ShouldBeGreaterThan(50);
    }

    [Fact]
    public async Task OutOfSpecJoin_FallsIntoMiterDefaultCase()
    {
        SkipIfNoFreeType();
        // j 3 is out of spec (valid values 0/1/2). It passes the non-zero guard and lands in the
        // DrawLineJoin `default` (miter) arm, which extends the outer edges to the intersection.
        var n = await NonWhiteAsync(Page100("0 0 0 RG 6 w 3 j 10 M 10 10 m 50 80 l 90 10 l S"));
        n.ShouldBeGreaterThan(50);
    }

    [Fact]
    public async Task OutOfSpecJoin_NearStraightSegments_HitsParallelGuard()
    {
        SkipIfNoFreeType();
        // Nearly-collinear segments make the miter sinHalf ~ 0, exercising the parallel-segments
        // early break in the miter default arm.
        var n = await NonWhiteAsync(Page100("0 0 0 RG 6 w 3 j 10 M 10 50 m 50 50 l 90 50 l S"));
        n.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task MiterJoin_ThickPolyline_Paints()
    {
        SkipIfNoFreeType();
        // j 0 = miter join with a generous miter limit → miter triangle path.
        var n = await NonWhiteAsync(Page100("0 0 0 RG 6 w 0 j 10 M 10 10 m 50 80 l 90 10 l S"));
        n.ShouldBeGreaterThan(50);
    }

    [Fact]
    public async Task MiterJoin_SharpAngle_ExceedsLimit_FallsBackToBevel()
    {
        SkipIfNoFreeType();
        // A very sharp spike with a low miter limit forces the miter→bevel fallback.
        var n = await NonWhiteAsync(Page100("0 0 0 RG 6 w 0 j 1 M 10 10 m 50 90 l 90 10 l 50 12 l S"));
        n.ShouldBeGreaterThan(50);
    }

    [Fact]
    public async Task ProjectingSquareCaps_OpenPath_Paints()
    {
        SkipIfNoFreeType();
        // J 2 = projecting square cap on an open subpath.
        var n = await NonWhiteAsync(Page100("0 0 0 RG 8 w 2 J 20 50 m 80 50 l S"));
        n.ShouldBeGreaterThan(50);
    }

    [Fact]
    public async Task SoftMaskedPolygonFill_Paints()
    {
        SkipIfNoFreeType();
        await using var doc = await LoadAsync(SoftMaskTrianglePdf(), TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(96), TestContext.Current.CancellationToken);
        // Some pixels inside the mask region must be painted (triangle, not a rect → span path).
        PdfTestConstants.CountNonWhitePixels(png).ShouldBeGreaterThan(50);
    }

    [Fact]
    public async Task PatternFill_NoComponents_UsesGreyApproximation()
    {
        SkipIfNoFreeType();
        // scn with only a pattern name (no numerics) and an unknown pattern → FillIsPattern
        // stays true → DrawFill paints the neutral grey (160) approximation. Use a non-rect
        // path so the polygon fill path (not FillRect) runs.
        var n = await NonWhiteAsync(Page100("/Pattern cs /P1 scn 10 10 m 90 20 l 50 90 l f"));
        n.ShouldBeGreaterThan(100);
    }

    [Fact]
    public async Task ZeroLengthDashSegment_IsSkipped()
    {
        SkipIfNoFreeType();
        // Dash array with a zero entry mixed with a positive one exercises the seg<=0 skip.
        var n = await NonWhiteAsync(Page100("0 0 0 RG 4 w [8 0 4] 0 d 5 50 m 95 50 l S"));
        n.ShouldBeGreaterThan(20);
    }

    [Fact]
    public async Task LineToBeforeMoveTo_StartsSubpath()
    {
        SkipIfNoFreeType();
        // An 'l' with no preceding 'm' must implicitly start a subpath (PathLineTo null branch).
        var n = await NonWhiteAsync(Page100("0 0 0 RG 4 w 20 20 l 80 80 l S"));
        n.ShouldBeGreaterThan(10);
    }

    [Fact]
    public async Task SinglePointClipSubpath_IsIgnored()
    {
        SkipIfNoFreeType();
        // A clip whose only subpath is a single moveto (count < 2) is skipped by ApplyPendingClip;
        // since no polygons remain, the clip is a no-op and the later fill still paints.
        var n = await NonWhiteAsync(Page100("50 50 m W n 0 0 0 rg 0 0 100 100 re f"));
        n.ShouldBeGreaterThan(1000);
    }

    // A 100×100 page that fills a TRIANGLE (forcing the polygon span path, not FillRect) with a
    // soft mask active, so FillSpanSoftMasked runs.
    private static byte[] SoftMaskTrianglePdf()
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
        PdfFixtures.Ln(sb, "   /Resources << /ExtGState << /GS1 5 0 R >> >> >>");
        PdfFixtures.Ln(sb, "endobj");
        const string content =
            "1 g 0 0 100 100 re f\n" +
            "/GS1 gs\n" +
            "0 g 10 10 m 90 10 l 50 90 l f"; // triangle fill under soft mask
        var cb = Encoding.Latin1.GetBytes(content);
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "4 0 obj");
        PdfFixtures.Ln(sb, $"<< /Length {cb.Length} >>");
        sb.Append("stream\n").Append(content);
        PdfFixtures.Ln(sb, "\nendstream");
        PdfFixtures.Ln(sb, "endobj");
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "5 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /ExtGState /SMask << /Type /Mask /S /Alpha /G 6 0 R >> >>");
        PdfFixtures.Ln(sb, "endobj");
        const string maskContent = "1 g 20 20 60 60 re f";
        var mc = Encoding.Latin1.GetBytes(maskContent);
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "6 0 obj");
        PdfFixtures.Ln(sb, $"<< /Type /XObject /Subtype /Form /BBox [0 0 100 100] /Length {mc.Length} >>");
        sb.Append("stream\n").Append(maskContent);
        PdfFixtures.Ln(sb, "\nendstream");
        PdfFixtures.Ln(sb, "endobj");
        var xref = PdfFixtures.Len(sb);
        PdfFixtures.Ln(sb, "xref");
        PdfFixtures.Ln(sb, "0 7");
        PdfFixtures.Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets)
            PdfFixtures.Ln(sb, $"{o:D10} 00000 n ");
        PdfFixtures.Ln(sb, "trailer");
        PdfFixtures.Ln(sb, "<< /Size 7 /Root 1 0 R >>");
        PdfFixtures.Ln(sb, "startxref");
        PdfFixtures.Ln(sb, xref.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }
}
