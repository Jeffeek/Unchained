using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
/// Find/replace across the presentation, slides, and individual text frames (M-G).
/// Run formatting must be preserved, and matches that span multiple runs must be handled.
/// </summary>
public sealed class FindReplaceTests : PptxTestBase
{
    [Fact]
    public void ReplaceText_WithinSingleRun_ReplacesAndKeepsCount()
    {
        var doc = PptxFixtures.WithSlides(1);
        var box = doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(4), Emu.FromInches(1),
            "Hello world, hello again");

        var n = doc.ReplaceText("hello", "hi", StringComparison.OrdinalIgnoreCase);

        n.ShouldBe(2);
        box.TextFrame.PlainText.ShouldBe("hi world, hi again");
    }

    [Fact]
    public void ReplaceText_PreservesRunFormatting()
    {
        var doc = PptxFixtures.WithSlides(1);
        var box = doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(4), Emu.FromInches(1));

        // Two runs: "Foo" (bold) + "bar" (not bold).
        var para = box.TextFrame.Paragraphs.Add();
        var r1 = para.Runs.Add("Foo");
        r1.Format.Bold = InheritableBool.True;
        para.Runs.Add("bar");

        var n = box.TextFrame.ReplaceText("Foo", "Baz");

        n.ShouldBe(1);
        box.TextFrame.PlainText.ShouldBe("Bazbar");
        para.Runs[0].Text.ShouldBe("Baz");
        para.Runs[0].Format.Bold.ShouldBe(InheritableBool.True, "the replaced run keeps its bold formatting");
    }

    [Fact]
    public void ReplaceText_AcrossRunBoundary_ReplacesAndCollapses()
    {
        var doc = PptxFixtures.WithSlides(1);
        var box = doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(4), Emu.FromInches(1));

        // "Hel" + "lo " + "World" — match "lo Wo" spans runs.
        var para = box.TextFrame.Paragraphs.Add();
        para.Runs.Add("Hel");
        para.Runs.Add("lo ");
        para.Runs.Add("World");

        var n = box.TextFrame.ReplaceText("lo Wo", "XX");

        n.ShouldBe(1);
        box.TextFrame.PlainText.ShouldBe("HelXXrld");
    }

    [Fact]
    public void ReplaceText_InTableCells()
    {
        var doc = PptxFixtures.WithSlides(1);
        var table = doc.Slides[0].Shapes.AddTable(
            Emu.Zero, Emu.Zero,
            [Emu.FromInches(2), Emu.FromInches(2)],
            [Emu.FromInches(0.5)]);
        table.Grid[0, 0].TextFrame.PlainText = "TOKEN here";
        table.Grid[1, 0].TextFrame.PlainText = "and TOKEN there";

        var n = doc.ReplaceText("TOKEN", "value");

        n.ShouldBe(2);
        table.Grid[0, 0].TextFrame.PlainText.ShouldBe("value here");
        table.Grid[1, 0].TextFrame.PlainText.ShouldBe("and value there");
    }

    [Fact]
    public void ReplaceText_DoesNotTouchNotes_ByDefault()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(4), Emu.FromInches(1), "TOKEN");
        doc.Slides[0].Notes.NotesText = "TOKEN in notes";

        var n = doc.ReplaceText("TOKEN", "x");
        n.ShouldBe(1, "notes are excluded unless includeNotes is set");
        doc.Slides[0].Notes.NotesText.ShouldBe("TOKEN in notes");

        var n2 = doc.ReplaceText("TOKEN", "x", includeNotes: true);
        n2.ShouldBe(1);
        doc.Slides[0].Notes.NotesText.ShouldBe("x in notes");
    }

    [Fact]
    public async Task ReplaceText_SurvivesRoundTrip()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(4), Emu.FromInches(1), "before");
        doc.ReplaceText("before", "after");

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].GetAllText().ShouldContain("after");
        reloaded.Slides[0].GetAllText().ShouldNotContain("before");
    }

    [Fact]
    public void ReplaceText_EmptySearch_NoOp()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(4), Emu.FromInches(1), "abc");
        doc.ReplaceText("", "x").ShouldBe(0);
    }
}
