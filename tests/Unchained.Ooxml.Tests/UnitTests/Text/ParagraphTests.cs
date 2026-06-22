using Shouldly;
using Unchained.Ooxml.Text;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Text;

/// <summary>
///     Tests for <see cref="Paragraph" /> — the <see cref="Paragraph.PlainText" /> getter/setter and
///     <see cref="Paragraph.ReplaceText" /> across single runs, multiple runs, and field boundaries
///     (field runs split the paragraph into segments that matches never cross).
/// </summary>
public sealed class ParagraphTests
{
    [Fact]
    public void PlainText_Getter_ConcatenatesRuns()
    {
        var p = new Paragraph();
        p.Runs.Add("Hello, ");
        p.Runs.Add("world");
        p.PlainText.ShouldBe("Hello, world");
    }

    [Fact]
    public void PlainText_Setter_ReplacesAllRunsWithSingleRun()
    {
        var p = new Paragraph();
        p.Runs.Add("old1");
        p.Runs.Add("old2");

        p.PlainText = "fresh";

        p.Runs.Count.ShouldBe(1);
        p.PlainText.ShouldBe("fresh");
    }

    [Fact]
    public void ReplaceText_EmptyOldText_ReturnsZero()
    {
        var p = new Paragraph();
        p.Runs.Add("anything");
        p.ReplaceText(string.Empty, "x").ShouldBe(0);
    }

    [Fact]
    public void ReplaceText_WithinSingleRun_ReplacesAndKeepsFormat()
    {
        var p = new Paragraph();
        p.Runs.Add("the quick brown fox");

        var count = p.ReplaceText("quick", "slow");

        count.ShouldBe(1);
        p.PlainText.ShouldBe("the slow brown fox");
    }

    [Fact]
    public void ReplaceText_MultipleOccurrences_ReplacesAll()
    {
        var p = new Paragraph();
        p.Runs.Add("a a a");
        p.ReplaceText("a", "b").ShouldBe(3);
        p.PlainText.ShouldBe("b b b");
    }

    [Fact]
    public void ReplaceText_SpanningTwoRuns_MergesOntoFirstRun()
    {
        var p = new Paragraph();
        p.Runs.Add("Hello ");
        p.Runs.Add("World");
        // "lo Wo" spans the boundary between the two runs.
        var count = p.ReplaceText("lo Wo", "XX");

        count.ShouldBe(1);
        p.PlainText.ShouldBe("HelXXrld");
    }

    [Fact]
    public void ReplaceText_SpanningThreeRuns_ClearsMiddleRun()
    {
        var p = new Paragraph();
        p.Runs.Add("AAA");
        p.Runs.Add("BBB");
        p.Runs.Add("CCC");
        // Match "AABBBCC" spans all three runs; the middle run is fully consumed.
        var count = p.ReplaceText("AABBBCC", "-");

        count.ShouldBe(1);
        p.PlainText.ShouldBe("A-C");
    }

    [Fact]
    public void ReplaceText_FieldRun_ActsAsBoundary()
    {
        var p = new Paragraph();
        p.Runs.Add("Page ");
        var field = p.Runs.Add("5");
        field.Field = FieldType.SlideNumber;
        p.Runs.Add(" of total");

        // A match cannot cross the field run; "Page  of" is not contiguous text.
        var count = p.ReplaceText("Page  of", "X");
        count.ShouldBe(0);

        // But a match inside one segment works, and the field is untouched.
        p.ReplaceText("total", "end").ShouldBe(1);
        p.PlainText.ShouldBe("Page 5 of end");
    }

    [Fact]
    public void ReplaceText_NoMatch_ReturnsZero()
    {
        var p = new Paragraph();
        p.Runs.Add("nothing here");
        p.ReplaceText("absent", "x").ShouldBe(0);
        p.PlainText.ShouldBe("nothing here");
    }
}
