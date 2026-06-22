using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Text;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Slides;

public sealed class ShapeTextWalkerTests
{
    [Fact]
    public void Enumerate_AutoShape_YieldsTextFrame()
    {
        var auto = new AutoShape
        {
            TextFrame =
            {
                PlainText = "hello"
            }
        };

        var frames = ShapeTextWalker.EnumerateTextFrames([auto]).ToList();

        frames.Count.ShouldBe(1);
        frames[0].PlainText.ShouldBe("hello");
    }

    [Fact]
    public void Enumerate_PictureWithCaption_YieldsCaption()
    {
        var caption = new TextFrame
        {
            PlainText = "cap"
        };
        var pic = new PictureShape { Caption = caption };

        var frames = ShapeTextWalker.EnumerateTextFrames([pic]).ToList();

        frames.Count.ShouldBe(1);
        frames[0].PlainText.ShouldBe("cap");
    }

    [Fact]
    public void Enumerate_PictureWithoutCaption_YieldsNothing()
    {
        var pic = new PictureShape();
        ShapeTextWalker.EnumerateTextFrames([pic]).ShouldBeEmpty();
    }

    [Fact]
    public void Enumerate_Table_YieldsEveryCellTextFrame()
    {
        var grid = TableGrid.Create(
            [Emu.FromInches(1), Emu.FromInches(1)],
            [Emu.FromInches(1), Emu.FromInches(1)]
        );
        var table = new TableShape { Grid = grid };
        table[0, 0].TextFrame.PlainText = "a";
        table[1, 1].TextFrame.PlainText = "d";

        var frames = ShapeTextWalker.EnumerateTextFrames([table]).ToList();

        frames.Count.ShouldBe(4);
        frames.Select(static f => f.PlainText).ShouldContain("a");
        frames.Select(static f => f.PlainText).ShouldContain("d");
    }

    [Fact]
    public void Enumerate_Group_RecursesIntoChildren()
    {
        var inner = new AutoShape
        {
            TextFrame =
            {
                PlainText = "nested"
            }
        };
        var group = new GroupShape();
        group.Children.AddParsed(inner);

        var frames = ShapeTextWalker.EnumerateTextFrames([group]).ToList();

        frames.Count.ShouldBe(1);
        frames[0].PlainText.ShouldBe("nested");
    }

    [Fact]
    public void Enumerate_MixedShapes_YieldsAllFrames()
    {
        var auto = new AutoShape
        {
            TextFrame =
            {
                PlainText = "auto"
            }
        };
        var nested = new AutoShape
        {
            TextFrame =
            {
                PlainText = "deep"
            }
        };
        var group = new GroupShape();
        group.Children.AddParsed(nested);
        var connector = new ConnectorShape();

        var frames = ShapeTextWalker.EnumerateTextFrames([auto, group, connector]).ToList();

        frames.Select(static f => f.PlainText).ShouldBe(["auto", "deep"]);
    }
}
