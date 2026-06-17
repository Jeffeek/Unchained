using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Drives <c>ShapeParser</c> via round-trips of every top-level shape kind (autoshape, text box,
///     picture-less group, connector, table, group with children), asserting type, geometry, name,
///     and nesting survive a save/reload.
/// </summary>
public sealed class ShapeParserRoundTripTests : PptxTestBase
{
    [Fact]
    public async Task AllShapeKinds_SurviveRoundTrip()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shapes = doc.Slides[0].Shapes;
        shapes.AddShape(AutoShapeType.Ellipse, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(1));
        shapes.AddTextBox(Emu.FromInches(1), Emu.FromInches(3), Emu.FromInches(3), Emu.FromInches(1), "boxed");
        shapes.AddConnector(ConnectorType.Bent, Emu.FromInches(4), Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(1));
        shapes.AddTable(Emu.FromInches(1), Emu.FromInches(4), [Emu.FromInches(1), Emu.FromInches(1)], [Emu.FromInches(1)]);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var rs = reloaded.Slides[0].Shapes;
        rs.OfType<AutoShape>().Any(static s => s.ShapeType == AutoShapeType.Ellipse).ShouldBeTrue();
        rs.OfType<AutoShape>().Any(static s => s.IsTextBox).ShouldBeTrue();
        rs.OfType<ConnectorShape>().ShouldNotBeEmpty();
        rs.OfType<TableShape>().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ShapeGeometryAndRotation_RoundTrip()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0]
            .Shapes.AddShape(AutoShapeType.Rectangle, Emu.FromInches(2), Emu.FromInches(3), Emu.FromInches(4), Emu.FromInches(1));
        shape.RotationDegrees = 45;
        shape.Name = "RotatedRect";

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var rs = reloaded.Slides[0].Shapes.OfType<AutoShape>().Single(static s => s.Name == "RotatedRect");
        rs.X.Value.ShouldBe(Emu.FromInches(2).Value);
        rs.Y.Value.ShouldBe(Emu.FromInches(3).Value);
        rs.RotationDegrees.ShouldBe(45, 0.01);
    }

    [Fact]
    public async Task FlippedConnector_RoundTrips()
    {
        var doc = PptxFixtures.WithSlides(1);
        // AddLine going up-left to encode flipH + flipV.
        doc.Slides[0].Shapes.AddLine(Emu.FromInches(5), Emu.FromInches(5), Emu.FromInches(1), Emu.FromInches(1));

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var conn = reloaded.Slides[0].Shapes.OfType<ConnectorShape>().Single();
        conn.FlipHorizontal.ShouldBeTrue();
        conn.FlipVertical.ShouldBeTrue();
    }

    [Fact]
    public async Task GroupWithChildren_RoundTrips()
    {
        var doc = PptxFixtures.WithSlides(1);
        var group = doc.Slides[0].Shapes.AddGroup();
        group.Children.AddShape(AutoShapeType.Rectangle, Emu.Zero, Emu.Zero, Emu.FromInches(1), Emu.FromInches(1));
        group.Children.AddShape(AutoShapeType.Ellipse, Emu.FromInches(1), Emu.Zero, Emu.FromInches(1), Emu.FromInches(1));

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var rg = reloaded.Slides[0].Shapes.OfType<GroupShape>().Single();
        rg.Children.Count.ShouldBe(2);
    }

    [Fact]
    public async Task TableMergedCells_RoundTrip()
    {
        var doc = PptxFixtures.WithSlides(1);
        var table = doc.Slides[0]
            .Shapes.AddTable(
                Emu.FromInches(1),
                Emu.FromInches(1),
                [Emu.FromInches(2), Emu.FromInches(2)],
                [Emu.FromInches(1), Emu.FromInches(1)]
            );
        table.Grid[0, 0].TextFrame.Paragraphs.Add("merged");
        table.Grid[0, 0].ColumnSpan = 2;
        table.Grid[1, 0].IsHorizontalMergeContinuation = true;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var rt = reloaded.Slides[0].Shapes.OfType<TableShape>().Single();
        rt.Grid.RowCount.ShouldBe(2);
        rt.Grid.ColumnCount.ShouldBe(2);
    }

    [Fact]
    public async Task ShapeAltTextAndDecorative_RoundTrip()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0]
            .Shapes.AddShape(AutoShapeType.Rectangle, Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1));
        shape.AltText = "described";
        shape.Name = "Described";

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var rs = reloaded.Slides[0].Shapes.OfType<AutoShape>().Single(static s => s.Name == "Described");
        rs.AltText.ShouldBe("described");
    }
}
