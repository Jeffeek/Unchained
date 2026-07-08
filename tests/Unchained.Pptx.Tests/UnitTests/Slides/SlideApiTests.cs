using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Slides;

/// <summary>
///     Unit coverage for the public <see cref="Unchained.Pptx.Slides.Slide" /> API: comment
///     add/remove (including the null-author guard and not-belonging removal), text helpers
///     (<c>GetAllText</c>, <c>FindShapeByName</c>, <c>FindShapeByAltText</c>), <c>ReplaceText</c>
///     across shapes and notes, and shape click-hyperlink enumeration through group shapes.
/// </summary>
public sealed class SlideApiTests
{
    private static Slide FirstSlide()
    {
        var doc = PptxFixtures.WithSlides(1);
        return doc.Slides[0];
    }

    private static AutoShape AddText(Slide slide, string name, string text)
    {
        var shape = slide.Shapes.AddTextBox(Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(3), Emu.FromInches(1), text);
        shape.Name = name;
        return shape;
    }

    // ── Comments ──────────────────────────────────────────────────────────────

    [Fact]
    public void AddComment_WithAuthor_AppendsAndAssignsIndex()
    {
        var slide = FirstSlide();
        var authors = new CommentAuthorCollection();
        var author = authors.Add("Alice", "A");

        var comment = slide.AddComment("Hello", new SlidePosition(new Emu(0), new Emu(0)), author);

        slide.GetComments().ShouldHaveSingleItem().ShouldBeSameAs(comment);
        comment.Author.ShouldBeSameAs(author);
        comment.Index.ShouldBe(1u);
    }

    [Fact]
    public void AddComment_NullAuthor_Throws()
    {
        var slide = FirstSlide();
        Should.Throw<ArgumentNullException>(() => slide.AddComment("x", new SlidePosition(new Emu(0), new Emu(0)), null));
    }

    [Fact]
    public void RemoveComment_OnThisSlide_Removes()
    {
        var slide = FirstSlide();
        var author = new CommentAuthorCollection().Add("Bob", "B");
        var comment = slide.AddComment("hi", new SlidePosition(new Emu(0), new Emu(0)), author);

        slide.RemoveComment(comment);
        slide.GetComments().ShouldBeEmpty();
    }

    [Fact]
    public void RemoveComment_NotOnThisSlide_Throws()
    {
        var slide = FirstSlide();
        var author = new CommentAuthorCollection().Add("Bob", "B");
        var other = FirstSlide();
        var foreignComment = other.AddComment("x", new SlidePosition(new Emu(0), new Emu(0)), author);

        Should.Throw<ArgumentException>(() => slide.RemoveComment(foreignComment));
    }

    // ── Text helpers ────────────────────────────────────────────────────────

    [Fact]
    public void GetAllText_ConcatenatesShapeText()
    {
        var slide = FirstSlide();
        AddText(slide, "A", "First");
        AddText(slide, "B", "Second");
        slide.GetAllText().ShouldBe("First\nSecond");
    }

    [Fact]
    public void FindShapeByName_ReturnsMatchOrNull()
    {
        var slide = FirstSlide();
        AddText(slide, "Target", "t");
        slide.FindShapeByName("Target").ShouldNotBeNull();
        slide.FindShapeByName("Missing").ShouldBeNull();
    }

    [Fact]
    public void FindShapeByAltText_ReturnsMatchOrNull()
    {
        var slide = FirstSlide();
        var shape = AddText(slide, "A", "t");
        shape.AltText = "describe me";
        slide.FindShapeByAltText("describe me").ShouldBeSameAs(shape);
        slide.FindShapeByAltText("nope").ShouldBeNull();
    }

    [Fact]
    public void ReplaceText_AcrossShapes_ReturnsCount()
    {
        var slide = FirstSlide();
        AddText(slide, "A", "foo bar foo");
        var replaced = slide.ReplaceText("foo", "baz");
        replaced.ShouldBe(2);
        slide.GetAllText().ShouldContain("baz");
    }

    [Fact]
    public void ReplaceText_IncludeNotes_SearchesNotesFrame()
    {
        var slide = FirstSlide();
        slide.Notes.NotesText = "note foo";
        var replaced = slide.ReplaceText("foo", "bar", includeNotes: true);
        replaced.ShouldBe(1);
    }

    // ── Hyperlinks ──────────────────────────────────────────────────────────

    [Fact]
    public void GetHyperlinks_EnumeratesClickActionsIncludingGroupChildren()
    {
        var slide = FirstSlide();
        var top = AddText(slide, "Top", "t");
        top.ClickAction = new HyperlinkAction { Url = "https://example.com" };

        var group = slide.Shapes.AddGroup();
        var child = group.Children.AddTextBox(Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(1), "c");
        child.ClickAction = new HyperlinkAction { Url = "https://child.example" };

        var links = slide.GetHyperlinks().ToList();
        links.Count.ShouldBe(2);
    }

    [Fact]
    public void GetHyperlinks_NoClickActions_Empty()
    {
        var slide = FirstSlide();
        AddText(slide, "A", "t");
        slide.GetHyperlinks().ShouldBeEmpty();
    }
}
