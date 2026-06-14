using Shouldly;
using Unchained.Ooxml.Text;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Text;

public sealed class ParagraphCollectionTests
{
    [Fact]
    public void Add_Empty_AppendsParagraph()
    {
        var paragraphs = new ParagraphCollection();
        var p = paragraphs.Add();
        paragraphs.Count.ShouldBe(1);
        paragraphs[0].ShouldBeSameAs(p);
    }

    [Fact]
    public void Add_WithText_CreatesRun()
    {
        var paragraphs = new ParagraphCollection();
        var p = paragraphs.Add("hello");
        p.Runs.Count.ShouldBe(1);
        p.Runs[0].Text.ShouldBe("hello");
    }

    [Fact]
    public void Insert_PlacesParagraphAtIndex()
    {
        var paragraphs = new ParagraphCollection
        {
            "a",
            "c"
        };
        var inserted = paragraphs.Insert(1);
        paragraphs[1].ShouldBeSameAs(inserted);
        paragraphs.Count.ShouldBe(3);
    }

    [Fact]
    public void Remove_RemovesParagraph()
    {
        var paragraphs = new ParagraphCollection();
        var p = paragraphs.Add();
        paragraphs.Remove(p);
        paragraphs.Count.ShouldBe(0);
    }

    [Fact]
    public void RemoveAt_RemovesByIndex()
    {
        var paragraphs = new ParagraphCollection
        {
            "a",
            "b"
        };
        paragraphs.RemoveAt(0);
        paragraphs.Count.ShouldBe(1);
        paragraphs[0].Runs[0].Text.ShouldBe("b");
    }

    [Fact]
    public void Clear_RemovesAll()
    {
        var paragraphs = new ParagraphCollection();
        paragraphs.Add();
        paragraphs.Add();
        paragraphs.Clear();
        paragraphs.Count.ShouldBe(0);
    }

    [Fact]
    public void NonGenericEnumerator_Works()
    {
        var paragraphs = new ParagraphCollection();
        paragraphs.Add();
        paragraphs.Count.ShouldBe(1);
    }
}
