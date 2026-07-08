using Shouldly;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests.FormatConversion;

public sealed class SvgToPdfTests : PdfTestBase
{
    private const string SimpleSvg = """
                                     <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 100">
                                       <rect x="10" y="10" width="80" height="60" fill="#3399ff" stroke="black" stroke-width="2"/>
                                       <circle cx="150" cy="50" r="30" fill="red"/>
                                       <text x="20" y="90" font-size="14">Hello SVG</text>
                                     </svg>
                                     """;

    private const string PathSvg = """
                                   <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
                                     <path d="M 10 10 L 90 10 L 90 90 L 10 90 Z" fill="none" stroke="black"/>
                                     <path d="M 50 10 C 90 10 90 90 50 90 C 10 90 10 10 50 10 Z" fill="#ccffcc"/>
                                   </svg>
                                   """;

    [Fact]
    public async Task LoadFromSvg_SimpleShapes_ProducesOnePage()
    {
        await using var doc = await Processor.LoadFromSvgAsync(SimpleSvg, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
        doc.Pages[1].Width.ShouldBeGreaterThan(0);
        doc.Pages[1].Height.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task LoadFromSvg_WithPaths_ProducesValidPdf()
    {
        await using var doc = await Processor.LoadFromSvgAsync(PathSvg, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task LoadFromSvg_FitToPage_PageMatchesOptions()
    {
        var opts = new SvgLoadOptions(400f, 300f);
        await using var doc = await Processor.LoadFromSvgAsync(SimpleSvg, opts, TestContext.Current.CancellationToken);
        doc.Pages[1].Width.ShouldBe(400, 1.0);
        doc.Pages[1].Height.ShouldBe(300, 1.0);
    }

    [Fact]
    public async Task LoadFromSvg_NoViewBox_FallsBackToDimensions()
    {
        const string svg = """
                           <svg xmlns="http://www.w3.org/2000/svg" width="300" height="200">
                             <rect x="0" y="0" width="300" height="200" fill="lightblue"/>
                           </svg>
                           """;
        await using var doc = await Processor.LoadFromSvgAsync(svg, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task LoadFromSvg_GroupWithTransform_ProducesValidPdf()
    {
        const string svg = """
                           <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
                             <g transform="translate(10,10)">
                               <rect width="50" height="50" fill="green"/>
                             </g>
                           </svg>
                           """;
        await using var doc = await Processor.LoadFromSvgAsync(svg, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task LoadFromSvg_RoundTrip_PreservesPageCount()
    {
        await using var doc = await Processor.LoadFromSvgAsync(SimpleSvg, ct: TestContext.Current.CancellationToken);
        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(doc.PageCount);
    }

    [Fact]
    public async Task LoadFromSvg_Ellipse_ProducesValidPdf()
    {
        const string svg = """
                           <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
                             <ellipse cx="50" cy="50" rx="40" ry="25" fill="purple"/>
                           </svg>
                           """;
        await using var doc = await Processor.LoadFromSvgAsync(svg, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }
}
