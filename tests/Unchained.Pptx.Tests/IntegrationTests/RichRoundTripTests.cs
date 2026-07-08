using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Animations;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Builds a presentation with diverse content — formatted text, auto-shapes, a chart, a table,
///     a connector, comments, a slide transition, and an animation — then round-trips it through
///     save/load. Exercises the full parser+writer stack (shape, chart, comment, slide, layout,
///     master, transition, animation) in one pass.
/// </summary>
public sealed class RichRoundTripTests
{
    private static PresentationDocument BuildRichDocument()
    {
        var doc = PptxFixtures.BlankPresentation();
        var layout = doc.Masters[0].Layouts[0];
        var slide = doc.Slides.AddBlank(layout);

        // Formatted text box.
        var tb = slide.Shapes.AddTextBox(Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(4), Emu.FromInches(1));
        var para = tb.TextFrame.Paragraphs.Add();
        var run = para.Runs.Add("Rich text");
        run.Format.Bold = InheritableBool.True;
        run.Format.FontSizePoints = 28;
        run.Format.Fill = new FillFormat();
        run.Format.Fill.SetSolid(ColorSpec.FromRgb(0xC0, 0x10, 0x20));

        // Auto-shape with a fill.
        var rect = slide.Shapes.AddShape(
            AutoShapeType.Rectangle,
            Emu.FromInches(1),
            Emu.FromInches(3),
            Emu.FromInches(2),
            Emu.FromInches(1)
        );
        rect.Fill.SetSolid(ColorSpec.FromTheme(ThemeColorSlot.Accent2));

        // Chart with one series.
        var chartShape = slide.Shapes.AddChart(
            ChartType.ColumnClustered,
            Emu.FromInches(4),
            Emu.FromInches(3),
            Emu.FromInches(4),
            Emu.FromInches(3)
        );
        chartShape.Chart.Title = "Quarterly";
        chartShape.Chart.Data.Categories.AddRange(["Q1", "Q2", "Q3"]);
        var series = new ChartSeries { Name = "Revenue" };
        series.Values.AddRange([10.0, 20.0, 30.0]);
        chartShape.Chart.Data.Series.Add(series);

        // Table.
        slide.Shapes.AddTable(
            Emu.FromInches(1),
            Emu.FromInches(5),
            [Emu.FromInches(2), Emu.FromInches(2)],
            [Emu.FromInches(1), Emu.FromInches(1)]
        );

        // Connector.
        slide.Shapes.AddLine(Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(2));

        // Comment.
        var author = doc.CommentAuthors.Add("Reviewer");
        slide.AddComment("Looks good", new SlidePosition(Emu.FromInches(1), Emu.FromInches(1)), author);

        // Transition + animation.
        slide.Transition.Effect = TransitionEffect.Fade;
        slide.Transition.DurationSeconds = 1.0;
        slide.Animations.MainSequence.AddEffect(rect.ShapeId);

        return doc;
    }

    [Fact]
    public async Task RichDocument_RoundTrips_PreservesSlideCount()
    {
        var doc = BuildRichDocument();
        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RichDocument_RoundTrips_PreservesFormattedText()
    {
        var doc = BuildRichDocument();
        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        var textShapes = reloaded.Slides[0].Shapes.OfType<AutoShape>().ToList();
        textShapes.ShouldContain(static s => s.TextFrame.PlainText.Contains("Rich text"));
    }

    [Fact]
    public async Task RichDocument_RoundTrips_PreservesChart()
    {
        var doc = BuildRichDocument();
        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        var chart = reloaded.Slides[0].Shapes.OfType<ChartShape>().SingleOrDefault();
        chart.ShouldNotBeNull();
        chart.Chart.Data.Series.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task RichDocument_RoundTrips_PreservesTable()
    {
        var doc = BuildRichDocument();
        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Shapes.OfType<TableShape>().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task RichDocument_RoundTrips_PreservesConnector()
    {
        var doc = BuildRichDocument();
        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Shapes.OfType<ConnectorShape>().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task RichDocument_RoundTrips_PreservesComment()
    {
        var doc = BuildRichDocument();
        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].GetComments().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task RichDocument_RoundTrips_PreservesTransition()
    {
        var doc = BuildRichDocument();
        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Transition.Effect.ShouldBe(TransitionEffect.Fade);
    }

    [Fact]
    public async Task RichDocument_RoundTrips_PreservesAnimation()
    {
        var doc = BuildRichDocument();
        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Animations.HasAnimations.ShouldBeTrue();
    }
}
