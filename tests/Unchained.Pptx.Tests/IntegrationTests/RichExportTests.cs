using System.Text;
using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Exports a content-rich presentation through every export writer (SVG, HTML, HTML player,
///     ODP, PDF) and reimports the ODP, exercising the export-writer and ODP-parser paths with
///     real shape/text/chart/table content rather than blank slides.
/// </summary>
public sealed class RichExportTests
{
    private static PresentationDocument BuildContentDocument()
    {
        var doc = PptxFixtures.BlankPresentation();
        var layout = doc.Masters[0].Layouts[0];
        var slide = doc.Slides.AddBlank(layout);

        var tb = slide.Shapes.AddTextBox(Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(5), Emu.FromInches(1));
        var run = tb.TextFrame.Paragraphs.Add().Runs.Add("Exported content");
        run.Format.Bold = InheritableBool.True;
        run.Format.FontSizePoints = 24;

        var rect = slide.Shapes.AddShape(
            AutoShapeType.Rectangle,
            Emu.FromInches(1),
            Emu.FromInches(3),
            Emu.FromInches(2),
            Emu.FromInches(1)
        );
        rect.Fill.SetSolid(ColorSpec.FromRgb(0x20, 0x80, 0xC0));

        var chart = slide.Shapes.AddChart(
            ChartType.Pie,
            Emu.FromInches(4),
            Emu.FromInches(3),
            Emu.FromInches(3),
            Emu.FromInches(3)
        );
        chart.Chart.Data.Categories.AddRange(["A", "B"]);
        var series = new ChartSeries { Name = "S" };
        series.Values.AddRange([60.0, 40.0]);
        chart.Chart.Data.Series.Add(series);

        slide.Shapes.AddTable(
            Emu.FromInches(1),
            Emu.FromInches(5),
            [Emu.FromInches(2), Emu.FromInches(2)],
            [Emu.FromInches(1)]
        );

        return doc;
    }

    [Fact]
    public async Task ExportAsSvg_RichContent_ProducesSvgRoot()
    {
        await using var doc = BuildContentDocument();
        var svgs = await new PresentationProcessor().ExportAsSvgAsync(doc);
        svgs.Length.ShouldBe(1);
        Encoding.UTF8.GetString(svgs[0]).ShouldContain("<svg");
    }

    [Fact]
    public async Task SaveAsHtml_RichContent_ProducesHtmlFile()
    {
        await using var doc = BuildContentDocument();
        var dir = Path.Combine(Path.GetTempPath(), "unchained-html-" + Path.GetRandomFileName());
        try
        {
            var files = await new PresentationProcessor().SaveAsHtmlAsync(doc, dir);
            files.ShouldNotBeEmpty();
            var html = await File.ReadAllTextAsync(files[0]);
            html.ShouldContain("<!DOCTYPE html>");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task ExportHtmlPlayer_RichContent_ProducesHtml()
    {
        await using var doc = BuildContentDocument();
        var bytes = await PresentationProcessor.ExportHtmlPlayerAsync(doc);
        Encoding.UTF8.GetString(bytes).ShouldContain("<");
    }

    [Fact]
    public async Task ExportOdp_ThenReimport_PreservesSlide()
    {
        await using var doc = BuildContentDocument();
        var processor = new PresentationProcessor();
        var odpBytes = await processor.ExportOdpAsync(doc);
        odpBytes.ShouldNotBeEmpty();

        using var ms = new MemoryStream(odpBytes);
        await using var reloaded = await processor.LoadAsync(ms);
        reloaded.Slides.Count.ShouldBe(1);
        reloaded.Slides[0].Shapes.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task SaveAsPdf_RichContent_ProducesPdfBytes()
    {
        await using var doc = BuildContentDocument();
        using var ms = new MemoryStream();
        await new PresentationProcessor().SaveAsPdfAsync(doc, ms);
        ms.Length.ShouldBeGreaterThan(0);
        // PDF files start with "%PDF".
        ms.Position = 0;
        var header = new byte[4];
        _ = ms.Read(header, 0, 4);
        Encoding.ASCII.GetString(header).ShouldBe("%PDF");
    }
}
