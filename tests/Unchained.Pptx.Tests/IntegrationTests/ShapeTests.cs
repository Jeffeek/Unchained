using Shouldly;
using Unchained.Pptx.Core;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class ShapeTests : PptxTestBase
{
    private static Slides.Slide FirstSlide()
    {
        var doc = PptxFixtures.WithSlides(1);
        return doc.Slides[0];
    }

    [Fact]
    public void AddShape_IncreasesShapeCount()
    {
        var slide = FirstSlide();
        slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.FromInches(1), Emu.FromInches(1),
            Emu.FromInches(2), Emu.FromInches(1));
        slide.Shapes.Count.ShouldBe(1);
    }

    [Fact]
    public void AddShape_AssignsShapeId()
    {
        var slide = FirstSlide();
        var shape = slide.Shapes.AddShape(AutoShapeType.Ellipse,
            Emu.Zero, Emu.Zero,
            Emu.FromInches(1), Emu.FromInches(1));
        shape.ShapeId.ShouldBeGreaterThan(0u);
    }

    [Fact]
    public void AddShape_PositionIsCorrect()
    {
        var slide = FirstSlide();
        var x = Emu.FromInches(2);
        var y = Emu.FromInches(3);
        var shape = slide.Shapes.AddShape(AutoShapeType.Rectangle, x, y,
            Emu.FromInches(1), Emu.FromInches(1));
        shape.X.ShouldBe(x);
        shape.Y.ShouldBe(y);
    }

    [Fact]
    public void AddShape_SizeIsCorrect()
    {
        var slide = FirstSlide();
        var w = Emu.FromInches(3);
        var h = Emu.FromInches(2);
        var shape = slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.Zero, Emu.Zero, w, h);
        shape.Width.ShouldBe(w);
        shape.Height.ShouldBe(h);
    }

    [Fact]
    public void AddTextBox_IsTextBox()
    {
        var slide = FirstSlide();
        var tb = slide.Shapes.AddTextBox(
            Emu.Zero, Emu.Zero,
            Emu.FromInches(3), Emu.FromInches(1));
        tb.IsTextBox.ShouldBeTrue();
    }

    [Fact]
    public void AddTextBox_WithText_HasCorrectText()
    {
        var slide = FirstSlide();
        var tb = slide.Shapes.AddTextBox(
            Emu.Zero, Emu.Zero,
            Emu.FromInches(3), Emu.FromInches(1),
            "Hello World");
        tb.TextFrame.PlainText.ShouldBe("Hello World");
    }

    [Fact]
    public void AddTable_CreatesTableShape()
    {
        var slide = FirstSlide();
        var table = slide.Shapes.AddTable(
            Emu.Zero, Emu.Zero,
            [Emu.FromInches(2), Emu.FromInches(2)],
            [Emu.FromInches(0.5), Emu.FromInches(0.5)]);

        table.ShouldBeOfType<TableShape>();
        table.Grid.ColumnCount.ShouldBe(2);
        table.Grid.RowCount.ShouldBe(2);
    }

    [Fact]
    public void AddConnector_CreatesConnectorShape()
    {
        var slide = FirstSlide();
        var connector = slide.Shapes.AddConnector(
            ConnectorType.Straight,
            Emu.Zero, Emu.Zero,
            Emu.FromInches(2), Emu.FromInches(1));
        connector.ShouldBeOfType<ConnectorShape>();
        connector.ConnectorType.ShouldBe(ConnectorType.Straight);
    }

    [Fact]
    public void AddGroup_CreatesGroupShape()
    {
        var slide = FirstSlide();
        var group = slide.Shapes.AddGroup();
        group.ShouldBeOfType<GroupShape>();
    }

    [Fact]
    public void Remove_DecreasesShapeCount()
    {
        var slide = FirstSlide();
        var shape = slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.Zero, Emu.Zero,
            Emu.FromInches(1), Emu.FromInches(1));
        slide.Shapes.Remove(shape);
        slide.Shapes.Count.ShouldBe(0);
    }

    [Fact]
    public void BringToFront_MovesShapeToEnd()
    {
        var slide = FirstSlide();
        var first = slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.Zero, Emu.Zero, Emu.FromInches(1), Emu.FromInches(1));
        var second = slide.Shapes.AddShape(AutoShapeType.Ellipse,
            Emu.Zero, Emu.Zero, Emu.FromInches(1), Emu.FromInches(1));
        slide.Shapes.BringToFront(first);
        slide.Shapes[slide.Shapes.Count - 1].ShouldBeSameAs(first);
    }

    [Fact]
    public void SendToBack_MovesShapeToStart()
    {
        var slide = FirstSlide();
        slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.Zero, Emu.Zero, Emu.FromInches(1), Emu.FromInches(1));
        var second = slide.Shapes.AddShape(AutoShapeType.Ellipse,
            Emu.Zero, Emu.Zero, Emu.FromInches(1), Emu.FromInches(1));
        slide.Shapes.SendToBack(second);
        slide.Shapes[0].ShouldBeSameAs(second);
    }

    [Fact]
    public void FindShapeByName_ReturnsCorrectShape()
    {
        var slide = FirstSlide();
        var shape = slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.Zero, Emu.Zero, Emu.FromInches(1), Emu.FromInches(1));
        shape.Name = "MyShape";
        slide.FindShapeByName("MyShape").ShouldBeSameAs(shape);
    }

    [Fact]
    public void FindShapeByName_NotFound_ReturnsNull()
    {
        var slide = FirstSlide();
        slide.FindShapeByName("NonExistent").ShouldBeNull();
    }

    [Fact]
    public async Task Shapes_RoundTrip_PreservesCount()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1));
        doc.Slides[0].Shapes.AddShape(AutoShapeType.Ellipse,
            Emu.FromInches(3), Emu.Zero, Emu.FromInches(2), Emu.FromInches(1));

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Shapes.Count.ShouldBe(2);
    }
}
