using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Rendering.Engine;
using Unchained.Pptx.Rendering.Models;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Rendering.Tests.IntegrationTests;

/// <summary>
///     Renders slides containing charts, tables, connectors, and effect-laden shapes through
///     <see cref="SlideRenderer" />, exercising the rasterizer's chart, effect, connector, and
///     text paint paths (beyond the blank-slide coverage in <c>RenderingTests</c>).
/// </summary>
public sealed class RichRenderingTests
{
    private static readonly RenderOptions Small = new() { WidthPx = 320, HeightPx = 180 };

    [Fact]
    public async Task RenderSlide_WithColumnChart_ProducesImage()
    {
        var doc = PptxFixtures.WithSlides(1);
        var chart = doc.Slides[0]
            .Shapes.AddChart(
                ChartType.ColumnClustered,
                Emu.FromInches(1),
                Emu.FromInches(1),
                Emu.FromInches(5),
                Emu.FromInches(4)
            );
        chart.Chart.Title = "Sales";
        chart.Chart.Data.Categories.AddRange(["Q1", "Q2", "Q3", "Q4"]);
        var series = new ChartSeries { Name = "Revenue" };
        series.Values.AddRange([10.0, 25.0, 15.0, 30.0]);
        chart.Chart.Data.Series.Add(series);

        var image = await SlideRenderer.RenderAsync(doc.Slides[0], doc.SlideSize, Small);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RenderSlide_WithPieChart_ProducesImage()
    {
        var doc = PptxFixtures.WithSlides(1);
        var chart = doc.Slides[0]
            .Shapes.AddChart(
                ChartType.Pie,
                Emu.FromInches(1),
                Emu.FromInches(1),
                Emu.FromInches(4),
                Emu.FromInches(4)
            );
        chart.Chart.Data.Categories.AddRange(["A", "B", "C"]);
        var series = new ChartSeries { Name = "Share" };
        series.Values.AddRange([50.0, 30.0, 20.0]);
        chart.Chart.Data.Series.Add(series);

        var image = await SlideRenderer.RenderAsync(doc.Slides[0], doc.SlideSize, Small);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RenderSlide_WithLineChart_ProducesImage()
    {
        var doc = PptxFixtures.WithSlides(1);
        var chart = doc.Slides[0]
            .Shapes.AddChart(
                ChartType.Line,
                Emu.FromInches(1),
                Emu.FromInches(1),
                Emu.FromInches(5),
                Emu.FromInches(4)
            );
        chart.Chart.Data.Categories.AddRange(["Jan", "Feb", "Mar"]);
        var series = new ChartSeries { Name = "Temp" };
        series.Values.AddRange([5.0, 12.0, 18.0]);
        chart.Chart.Data.Series.Add(series);

        var image = await SlideRenderer.RenderAsync(doc.Slides[0], doc.SlideSize, Small);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RenderSlide_WithTable_ProducesImage()
    {
        var doc = PptxFixtures.WithSlides(1);
        var table = doc.Slides[0]
            .Shapes.AddTable(
                Emu.FromInches(1),
                Emu.FromInches(1),
                [Emu.FromInches(2), Emu.FromInches(2)],
                [Emu.FromInches(1), Emu.FromInches(1)]
            );
        table.Grid[0, 0].TextFrame.Paragraphs.Add("Header");
        table.Grid[1, 1].TextFrame.Paragraphs.Add("Cell");

        var image = await SlideRenderer.RenderAsync(doc.Slides[0], doc.SlideSize, Small);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RenderSlide_WithConnector_ProducesImage()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0]
            .Shapes.AddLine(
                Emu.FromInches(1),
                Emu.FromInches(1),
                Emu.FromInches(5),
                Emu.FromInches(4)
            );

        var image = await SlideRenderer.RenderAsync(doc.Slides[0], doc.SlideSize, Small);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RenderSlide_WithShadowedShape_ProducesImage()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0]
            .Shapes.AddShape(
                AutoShapeType.Rectangle,
                Emu.FromInches(1),
                Emu.FromInches(1),
                Emu.FromInches(3),
                Emu.FromInches(2)
            );
        shape.Fill.SetSolid(ColorSpec.FromRgb(0, 112, 192));
        shape.Effects.OuterShadow = new OuterShadowEffect
        {
            Color = ColorSpec.FromArgb(0x80, 0, 0, 0),
            BlurRadius = Emu.FromPoints(4),
            Distance = Emu.FromPoints(3),
            DirectionDegrees = 45
        };

        var image = await SlideRenderer.RenderAsync(doc.Slides[0], doc.SlideSize, Small);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RenderSlide_WithGlowShape_ProducesImage()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0]
            .Shapes.AddShape(
                AutoShapeType.Ellipse,
                Emu.FromInches(1),
                Emu.FromInches(1),
                Emu.FromInches(2),
                Emu.FromInches(2)
            );
        shape.Fill.SetSolid(ColorSpec.FromRgb(255, 0, 0));
        shape.Effects.Glow = new GlowEffect { Color = ColorSpec.FromRgb(255, 255, 0), Radius = Emu.FromPoints(8) };

        var image = await SlideRenderer.RenderAsync(doc.Slides[0], doc.SlideSize, Small);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RenderSlide_WithRotatedFormattedText_ProducesImage()
    {
        var doc = PptxFixtures.WithSlides(1);
        var tb = doc.Slides[0]
            .Shapes.AddTextBox(
                Emu.FromInches(1),
                Emu.FromInches(1),
                Emu.FromInches(4),
                Emu.FromInches(1)
            );
        tb.RotationDegrees = 30;
        var run = tb.TextFrame.Paragraphs.Add().Runs.Add("Rotated bold text");
        run.Format.Bold = InheritableBool.True;
        run.Format.FontSizePoints = 32;
        run.Format.Fill = new FillFormat();
        run.Format.Fill.SetSolid(ColorSpec.FromRgb(0x10, 0x80, 0x40));

        var image = await SlideRenderer.RenderAsync(doc.Slides[0], doc.SlideSize, Small);
        image.Data.Length.ShouldBeGreaterThan(0);
    }
}
