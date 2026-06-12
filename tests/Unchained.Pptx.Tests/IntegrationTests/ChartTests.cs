using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Parsing;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;
using Unchained.Pptx.Tests.Helpers;
using Unchained.Pptx.Writing;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class ChartTests : PptxTestBase
{
    // ── Direct ChartWriter/ChartParser unit tests ─────────────────────────────

    [Fact]
    public void ChartWriter_LineChart_ProducesValidXml()
    {
        var model = new ChartModel { Type = ChartType.Line, Title = "Test Chart" };
        model.Data.Categories.AddRange(["A", "B", "C"]);
        var s = new ChartSeries { Name = "Series 1" };
        s.Values.AddRange([1.0, 2.0, 3.0]);
        model.Data.Series.Add(s);

        var bytes = ChartWriter.Write(model);
        bytes.ShouldNotBeEmpty();

        var doc = OoXmlHelper.ParseXml(bytes);
        doc.Root.ShouldNotBeNull();
        doc.Root!.Name.LocalName.ShouldBe("chartSpace");
    }

    [Fact]
    public void ChartParser_LineChart_ParsesType()
    {
        var model = new ChartModel { Type = ChartType.Line };
        AddSimpleSeries(model);
        var bytes = ChartWriter.Write(model);

        var doc = OoXmlHelper.ParseXml(bytes);
        var result = new ChartModel();
        ChartParser.Parse(doc.Root!, result);

        result.Type.ShouldBe(ChartType.Line);
    }

    [Fact]
    public void ChartParser_ColumnChart_ParsesTitle()
    {
        var model = new ChartModel { Type = ChartType.ColumnClustered, Title = "Sales 2024" };
        AddSimpleSeries(model);
        var bytes = ChartWriter.Write(model);

        var doc = OoXmlHelper.ParseXml(bytes);
        var result = new ChartModel();
        ChartParser.Parse(doc.Root!, result);

        result.Title.ShouldBe("Sales 2024");
    }

    [Fact]
    public void ChartParser_WithCategories_ParsesCategories()
    {
        var model = new ChartModel { Type = ChartType.ColumnClustered };
        model.Data.Categories.AddRange(["Q1", "Q2", "Q3"]);
        var s = new ChartSeries { Name = "Revenue" };
        s.Values.AddRange([10.0, 20.0, 30.0]);
        model.Data.Series.Add(s);
        var bytes = ChartWriter.Write(model);

        var doc = OoXmlHelper.ParseXml(bytes);
        var result = new ChartModel();
        ChartParser.Parse(doc.Root!, result);

        result.Data.Categories.ShouldBe(["Q1", "Q2", "Q3"]);
        result.Data.Series[0].Name.ShouldBe("Revenue");
        result.Data.Series[0].Values.ShouldBe([10.0, 20.0, 30.0]);
    }

    // ── AddChart factory ──────────────────────────────────────────────────────

    [Fact]
    public void AddChart_ReturnsChartShape()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];

        var shape = slide.Shapes.AddChart(
            ChartType.ColumnClustered,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(6),
            Emu.FromInches(4));

        shape.ShouldBeOfType<ChartShape>();
    }

    [Fact]
    public void AddChart_AssignsShapeId()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddChart(
            ChartType.BarClustered,
            Emu.Zero,
            Emu.Zero,
            Emu.FromInches(5),
            Emu.FromInches(3));

        shape.ShapeId.ShouldBeGreaterThan(0u);
    }

    [Fact]
    public void AddChart_SetsPosition()
    {
        var doc = PptxFixtures.WithSlides(1);
        var x = Emu.FromInches(2);
        var y = Emu.FromInches(1.5);
        var shape = doc.Slides[0].Shapes.AddChart(
            ChartType.Pie,
            x,
            y,
            Emu.FromInches(4),
            Emu.FromInches(3));

        shape.X.ShouldBe(x);
        shape.Y.ShouldBe(y);
    }

    [Fact]
    public void AddChart_SetsSize()
    {
        var doc = PptxFixtures.WithSlides(1);
        var width = Emu.FromInches(6);
        var height = Emu.FromInches(4);
        var shape = doc.Slides[0].Shapes.AddChart(
            ChartType.Line,
            Emu.Zero,
            Emu.Zero,
            width,
            height);

        shape.Width.ShouldBe(width);
        shape.Height.ShouldBe(height);
    }

    [Fact]
    public void AddChart_SetsChartType()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddChart(
            ChartType.LineWithMarkers,
            Emu.Zero,
            Emu.Zero,
            Emu.FromInches(5),
            Emu.FromInches(3));

        shape.Chart.Type.ShouldBe(ChartType.LineWithMarkers);
    }

    [Fact]
    public void AddChart_IncreasesShapeCount()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.Zero,
            Emu.Zero,
            Emu.FromInches(1),
            Emu.FromInches(1));
        slide.Shapes.AddChart(ChartType.ColumnClustered,
            Emu.FromInches(2),
            Emu.Zero,
            Emu.FromInches(5),
            Emu.FromInches(3));

        slide.Shapes.Count.ShouldBe(2);
    }

    // ── ChartModel mutation ───────────────────────────────────────────────────

    [Fact]
    public void ChartModel_SetTitle()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddChart(
            ChartType.ColumnClustered,
            Emu.Zero,
            Emu.Zero,
            Emu.FromInches(5),
            Emu.FromInches(3));

        shape.Chart.Title = "Sales by Region";
        shape.Chart.Title.ShouldBe("Sales by Region");
    }

    [Fact]
    public void ChartModel_AddCategoriesAndSeries()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddChart(
            ChartType.ColumnClustered,
            Emu.Zero,
            Emu.Zero,
            Emu.FromInches(5),
            Emu.FromInches(3));

        shape.Chart.Data.Categories.AddRange(["Q1", "Q2", "Q3", "Q4"]);
        var series = new ChartSeries { Name = "Revenue" };
        series.Values.AddRange([10.0, 20.0, 30.0, 25.0]);
        shape.Chart.Data.Series.Add(series);

        shape.Chart.Data.Categories.Count.ShouldBe(4);
        shape.Chart.Data.Series.Count.ShouldBe(1);
        shape.Chart.Data.Series[0].Name.ShouldBe("Revenue");
        shape.Chart.Data.Series[0].Values[2].ShouldBe(30.0);
    }

    [Fact]
    public void ChartModel_Legend_DefaultsToBottom()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddChart(
            ChartType.Line,
            Emu.Zero,
            Emu.Zero,
            Emu.FromInches(5),
            Emu.FromInches(3));

        shape.Chart.Legend.Position.ShouldBe(ChartLegendPosition.Bottom);
        shape.Chart.Legend.IsVisible.ShouldBeTrue();
    }

    [Fact]
    public void ChartModel_Legend_CanBeChanged()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddChart(
            ChartType.Line,
            Emu.Zero,
            Emu.Zero,
            Emu.FromInches(5),
            Emu.FromInches(3));

        shape.Chart.Legend.Position = ChartLegendPosition.Right;
        shape.Chart.Legend.IsVisible = false;

        shape.Chart.Legend.Position.ShouldBe(ChartLegendPosition.Right);
        shape.Chart.Legend.IsVisible.ShouldBeFalse();
    }

    // ── Round-trip: new chart ─────────────────────────────────────────────────

    [Fact]
    public async Task RoundTrip_NewColumnChart_PreservesChartType()
    {
        var doc = PptxFixtures.WithSlides(1);
        PopulateColumnChart(doc.Slides[0]);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        var chart = reloaded.Slides[0].Shapes.OfType<ChartShape>().Single();
        chart.Chart.Type.ShouldBe(ChartType.ColumnClustered);
    }

    [Fact]
    public async Task RoundTrip_NewColumnChart_PreservesTitle()
    {
        var doc = PptxFixtures.WithSlides(1);
        PopulateColumnChart(doc.Slides[0]);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        var chart = reloaded.Slides[0].Shapes.OfType<ChartShape>().Single();
        chart.Chart.Title.ShouldBe("Sales 2024");
    }

    [Fact]
    public async Task RoundTrip_NewColumnChart_PreservesCategories()
    {
        var doc = PptxFixtures.WithSlides(1);
        PopulateColumnChart(doc.Slides[0]);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        var chart = reloaded.Slides[0].Shapes.OfType<ChartShape>().Single();
        chart.Chart.Data.Categories.ShouldBe(["Q1", "Q2", "Q3", "Q4"]);
    }

    [Fact]
    public async Task RoundTrip_NewColumnChart_PreservesSeriesName()
    {
        var doc = PptxFixtures.WithSlides(1);
        PopulateColumnChart(doc.Slides[0]);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        var chart = reloaded.Slides[0].Shapes.OfType<ChartShape>().Single();
        chart.Chart.Data.Series[0].Name.ShouldBe("Revenue");
    }

    [Fact]
    public async Task RoundTrip_NewColumnChart_PreservesSeriesValues()
    {
        var doc = PptxFixtures.WithSlides(1);
        PopulateColumnChart(doc.Slides[0]);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        var chart = reloaded.Slides[0].Shapes.OfType<ChartShape>().Single();
        chart.Chart.Data.Series[0].Values.ShouldBe([10.0, 20.0, 30.0, 25.0]);
    }

    [Fact]
    public async Task RoundTrip_NewColumnChart_PreservesMultipleSeries()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        var shape = slide.Shapes.AddChart(ChartType.ColumnClustered,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(8),
            Emu.FromInches(4));

        shape.Chart.Title = "Multi-Series";
        shape.Chart.Data.Categories.AddRange(["Jan", "Feb", "Mar"]);

        var s1 = new ChartSeries { Name = "Product A" };
        s1.Values.AddRange([100.0, 150.0, 120.0]);
        shape.Chart.Data.Series.Add(s1);

        var s2 = new ChartSeries { Name = "Product B" };
        s2.Values.AddRange([80.0, 90.0, 110.0]);
        shape.Chart.Data.Series.Add(s2);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        var chart = reloaded.Slides[0].Shapes.OfType<ChartShape>().Single();
        chart.Chart.Data.Series.Count.ShouldBe(2);
        chart.Chart.Data.Series[1].Name.ShouldBe("Product B");
        chart.Chart.Data.Series[1].Values[2].ShouldBe(110.0);
    }

    [Fact]
    public async Task RoundTrip_BarChart_PreservesChartType()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddChart(ChartType.BarClustered,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(6),
            Emu.FromInches(4));
        AddSimpleSeries(shape.Chart);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Shapes.OfType<ChartShape>().Single()
            .Chart.Type.ShouldBe(ChartType.BarClustered);
    }

    [Fact]
    public async Task RoundTrip_LineChart_PreservesChartType()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddChart(ChartType.Line,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(6),
            Emu.FromInches(4));
        AddSimpleSeries(shape.Chart);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Shapes.OfType<ChartShape>().Single()
            .Chart.Type.ShouldBe(ChartType.Line);
    }

    [Fact]
    public async Task RoundTrip_LineWithMarkersChart_PreservesChartType()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddChart(ChartType.LineWithMarkers,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(6),
            Emu.FromInches(4));
        AddSimpleSeries(shape.Chart);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Shapes.OfType<ChartShape>().Single()
            .Chart.Type.ShouldBe(ChartType.LineWithMarkers);
    }

    [Fact]
    public async Task RoundTrip_PieChart_PreservesChartType()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddChart(ChartType.Pie,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(5),
            Emu.FromInches(4));
        AddSimpleSeries(shape.Chart);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Shapes.OfType<ChartShape>().Single()
            .Chart.Type.ShouldBe(ChartType.Pie);
    }

    [Fact]
    public async Task RoundTrip_DoughnutChart_PreservesChartType()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddChart(ChartType.Doughnut,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(5),
            Emu.FromInches(4));
        AddSimpleSeries(shape.Chart);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Shapes.OfType<ChartShape>().Single()
            .Chart.Type.ShouldBe(ChartType.Doughnut);
    }

    [Fact]
    public async Task RoundTrip_AreaChart_PreservesChartType()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddChart(ChartType.Area,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(6),
            Emu.FromInches(4));
        AddSimpleSeries(shape.Chart);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Shapes.OfType<ChartShape>().Single()
            .Chart.Type.ShouldBe(ChartType.Area);
    }

    [Fact]
    public async Task RoundTrip_ScatterChart_PreservesChartType()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddChart(ChartType.ScatterWithMarkersOnly,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(6),
            Emu.FromInches(4));
        AddSimpleSeries(shape.Chart);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Shapes.OfType<ChartShape>().Single()
            .Chart.Type.ShouldBe(ChartType.ScatterWithMarkersOnly);
    }

    [Fact]
    public async Task RoundTrip_ScatterChart_PreservesExplicitXValues()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddChart(ChartType.ScatterWithMarkersOnly,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(6),
            Emu.FromInches(4));
        var series = new ChartSeries { Name = "XY" };
        series.XValues.AddRange([1.5, 2.5, 3.5]);
        series.Values.AddRange([10.0, 20.0, 30.0]);
        shape.Chart.Data.Series.Add(series);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var reloadedSeries = reloaded.Slides[0].Shapes.OfType<ChartShape>().Single()
            .Chart.Data.Series.Single();
        reloadedSeries.XValues.ShouldBe([1.5, 2.5, 3.5]);
        reloadedSeries.Values.ShouldBe([10.0, 20.0, 30.0]);
    }

    [Fact]
    public async Task RoundTrip_RadarChart_PreservesChartType()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddChart(ChartType.Radar,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(6),
            Emu.FromInches(4));
        AddSimpleSeries(shape.Chart);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Shapes.OfType<ChartShape>().Single()
            .Chart.Type.ShouldBe(ChartType.Radar);
    }

    [Fact]
    public async Task RoundTrip_NewColumnChart_PreservesPosition()
    {
        var doc = PptxFixtures.WithSlides(1);
        var x = Emu.FromInches(2);
        var y = Emu.FromInches(1.5);
        var shape = doc.Slides[0].Shapes.AddChart(ChartType.ColumnClustered,
            x,
            y,
            Emu.FromInches(6),
            Emu.FromInches(4));
        AddSimpleSeries(shape.Chart);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var reloadedShape = reloaded.Slides[0].Shapes.OfType<ChartShape>().Single();
        reloadedShape.X.ShouldBe(x);
        reloadedShape.Y.ShouldBe(y);
    }

    [Fact]
    public async Task RoundTrip_LegendPosition_PreservesRight()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddChart(ChartType.ColumnClustered,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(6),
            Emu.FromInches(4));
        AddSimpleSeries(shape.Chart);
        shape.Chart.Legend.Position = ChartLegendPosition.Right;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Shapes.OfType<ChartShape>().Single()
            .Chart.Legend.Position.ShouldBe(ChartLegendPosition.Right);
    }

    [Fact]
    public async Task RoundTrip_LegendHidden_PreservesVisibility()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddChart(ChartType.ColumnClustered,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(6),
            Emu.FromInches(4));
        AddSimpleSeries(shape.Chart);
        shape.Chart.Legend.IsVisible = false;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Shapes.OfType<ChartShape>().Single()
            .Chart.Legend.IsVisible.ShouldBeFalse();
    }

    [Fact]
    public async Task RoundTrip_TwoChartsOnOneSlide_BothPreserved()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];

        var s1 = slide.Shapes.AddChart(ChartType.ColumnClustered,
            Emu.FromInches(0.5),
            Emu.FromInches(1),
            Emu.FromInches(4.5),
            Emu.FromInches(3.5));
        s1.Chart.Title = "Chart A";
        AddSimpleSeries(s1.Chart);

        var s2 = slide.Shapes.AddChart(ChartType.Pie,
            Emu.FromInches(5.5),
            Emu.FromInches(1),
            Emu.FromInches(4),
            Emu.FromInches(3.5));
        s2.Chart.Title = "Chart B";
        AddSimpleSeries(s2.Chart);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var charts = reloaded.Slides[0].Shapes.OfType<ChartShape>().ToList();
        charts.Count.ShouldBe(2);
        charts.Any(static c => c.Chart.Type == ChartType.ColumnClustered).ShouldBeTrue();
        charts.Any(static c => c.Chart.Type == ChartType.Pie).ShouldBeTrue();
    }

    [Fact]
    public async Task RoundTrip_ChartOnSlide2_SlideCountPreserved()
    {
        var doc = PptxFixtures.WithSlides(3);
        var shape = doc.Slides[1].Shapes.AddChart(ChartType.Line,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(8),
            Emu.FromInches(4));
        AddSimpleSeries(shape.Chart);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        reloaded.Slides.Count.ShouldBe(3);
        reloaded.Slides[1].Shapes.OfType<ChartShape>().Count().ShouldBe(1);
    }

    [Fact]
    public async Task RoundTrip_ColumnStacked_PreservesChartType()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddChart(ChartType.ColumnStacked,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(6),
            Emu.FromInches(4));
        AddSimpleSeries(shape.Chart);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Shapes.OfType<ChartShape>().Single()
            .Chart.Type.ShouldBe(ChartType.ColumnStacked);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void PopulateColumnChart(Slide slide)
    {
        var shape = slide.Shapes.AddChart(ChartType.ColumnClustered,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(8),
            Emu.FromInches(4));

        shape.Chart.Title = "Sales 2024";
        shape.Chart.Data.Categories.AddRange(["Q1", "Q2", "Q3", "Q4"]);

        var series = new ChartSeries { Name = "Revenue" };
        series.Values.AddRange([10.0, 20.0, 30.0, 25.0]);
        shape.Chart.Data.Series.Add(series);
    }

    private static void AddSimpleSeries(ChartModel chart)
    {
        chart.Data.Categories.AddRange(["A", "B", "C"]);
        var series = new ChartSeries { Name = "Series 1" };
        series.Values.AddRange([1.0, 2.0, 3.0]);
        chart.Data.Series.Add(series);
    }
}
