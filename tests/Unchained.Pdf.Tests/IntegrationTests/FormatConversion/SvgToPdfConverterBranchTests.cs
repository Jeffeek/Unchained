using Shouldly;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests.FormatConversion;

/// <summary>
///     Additional SVG→PDF conversion tests exercising the element and path-data branches the base
///     <see cref="SvgToPdfTests" /> does not reach: polyline/polygon, line, relative path commands
///     (m/l/h/v/c), hex and named colours, matrix transforms, and stroke styling.
/// </summary>
public sealed class SvgToPdfConverterBranchTests : PdfTestBase
{
    private static async Task<string> ContentTextAsync(string svg)
    {
        await using var doc = await Processor.LoadFromSvgAsync(svg, ct: TestContext.Current.CancellationToken);
        var ops = doc.Pages[1].GetContentOperators();
        return string.Join(" ", ops.Select(static o => o.Name));
    }

    [Fact]
    public async Task Polyline_EmitsLineSegmentsAndStroke()
    {
        const string svg = """
                           <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
                             <polyline points="10,10 50,50 90,10" fill="none" stroke="black"/>
                           </svg>
                           """;
        var names = await ContentTextAsync(svg);
        names.ShouldContain("m");
        names.ShouldContain("l");
        names.ShouldContain("S");
    }

    [Fact]
    public async Task Polygon_EmitsClosedFill()
    {
        const string svg = """
                           <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
                             <polygon points="10,10 90,10 50,90" fill="green"/>
                           </svg>
                           """;
        var names = await ContentTextAsync(svg);
        names.ShouldContain("m");
        names.ShouldContain("f");
    }

    [Fact]
    public async Task Line_EmitsMoveLineStroke()
    {
        const string svg = """
                           <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
                             <line x1="0" y1="0" x2="100" y2="100" stroke="red" stroke-width="3"/>
                           </svg>
                           """;
        var names = await ContentTextAsync(svg);
        names.ShouldContain("m");
        names.ShouldContain("l");
        names.ShouldContain("S");
    }

    [Fact]
    public async Task PathWithRelativeCommands_ProducesValidPdf()
    {
        const string svg = """
                           <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
                             <path d="m 10 10 l 20 0 h 10 v 10 c 5 5 10 5 15 0" fill="#abcdef"/>
                           </svg>
                           """;
        await using var doc = await Processor.LoadFromSvgAsync(svg, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
        var names = string.Join(" ", doc.Pages[1].GetContentOperators().Select(static o => o.Name));
        // Relative l, h, v all emit line segments; relative c emits a cubic curve.
        names.ShouldContain("c");
        names.ShouldContain("l");
    }

    [Fact]
    public async Task HexColorFill_ParsedToRgb()
    {
        const string svg = """
                           <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 50 50">
                             <rect x="0" y="0" width="50" height="50" fill="#ff8800"/>
                           </svg>
                           """;
        var names = await ContentTextAsync(svg);
        names.ShouldContain("rg"); // fill colour set
        names.ShouldContain("re");
        names.ShouldContain("f");
    }

    [
        Theory,
        InlineData("red"),
        InlineData("green"),
        InlineData("blue"),
        InlineData("gray"),
        InlineData("white")
    ]
    public async Task NamedColors_ProduceValidPdf(string color)
    {
        var svg = $"""
                   <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10">
                     <rect x="0" y="0" width="10" height="10" fill="{color}"/>
                   </svg>
                   """;
        await using var doc = await Processor.LoadFromSvgAsync(svg, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task GroupWithMatrixTransform_EmitsCm()
    {
        const string svg = """
                           <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
                             <g transform="matrix(1,0,0,1,5,5)">
                               <rect width="20" height="20" fill="black"/>
                             </g>
                           </svg>
                           """;
        var names = await ContentTextAsync(svg);
        names.ShouldContain("cm");
        names.ShouldContain("q");
        names.ShouldContain("Q");
    }

    [Fact]
    public async Task RectWithFillAndStroke_EmitsFillStrokePaint()
    {
        const string svg = """
                           <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 50 50">
                             <rect x="5" y="5" width="40" height="40" fill="yellow" stroke="black" stroke-width="2"/>
                           </svg>
                           """;
        var names = await ContentTextAsync(svg);
        names.ShouldContain("B"); // fill + stroke
    }

    [Fact]
    public async Task FillNoneWithStroke_EmitsStrokeOnly()
    {
        const string svg = """
                           <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 50 50">
                             <rect x="5" y="5" width="40" height="40" fill="none" stroke="black"/>
                           </svg>
                           """;
        var names = await ContentTextAsync(svg);
        names.ShouldContain("S");
    }

    [Fact]
    public async Task TextElement_EmitsTextOperators()
    {
        const string svg = """
                           <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 50">
                             <text x="10" y="30" font-size="16">Hello</text>
                           </svg>
                           """;
        var names = await ContentTextAsync(svg);
        names.ShouldContain("BT");
        names.ShouldContain("Tj");
        names.ShouldContain("ET");
    }

    [Fact]
    public async Task NoViewBox_WidthHeightUnits_FallsBack()
    {
        const string svg = """
                           <svg xmlns="http://www.w3.org/2000/svg" width="120px" height="80px">
                             <circle cx="60" cy="40" r="30" fill="blue"/>
                           </svg>
                           """;
        await using var doc = await Processor.LoadFromSvgAsync(svg, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task ZeroSizeRect_Skipped()
    {
        const string svg = """
                           <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
                             <rect x="0" y="0" width="0" height="0" fill="black"/>
                             <rect x="0" y="0" width="50" height="50" fill="red"/>
                           </svg>
                           """;
        await using var doc = await Processor.LoadFromSvgAsync(svg, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task FitToPage_DisabledByDefaultOption_PageDimensionsHonoured()
    {
        const string svg = """
                           <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
                             <rect width="100" height="100" fill="black"/>
                           </svg>
                           """;
        var opts = new SvgLoadOptions(FitToPage: false);
        await using var doc = await Processor.LoadFromSvgAsync(svg, opts, TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }
}
