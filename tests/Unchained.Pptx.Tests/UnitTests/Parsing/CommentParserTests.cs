using System.Xml.Linq;
using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Parsing;
using Unchained.Pptx.Slides;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Parsing;

/// <summary>
///     Branch coverage for <see cref="CommentParser" />: invalid/unknown author, invalid index,
///     index-bumps-author-last-index, position present vs absent, timestamp present vs absent,
///     and text present vs absent.
/// </summary>
public sealed class CommentParserTests
{
    private static readonly XNamespace Pml = PmlNames.Pml;

    private static XElement Comment(
        string authorId,
        string idx,
        bool withPos,
        string? dt,
        bool withText
    )
    {
        var cm = new XElement(Pml + "cm", new XAttribute("authorId", authorId), new XAttribute("idx", idx));
        if (dt != null) cm.Add(new XAttribute("dt", dt));
        if (withPos)
            cm.Add(new XElement(Pml + "pos", new XAttribute("x", "914400"), new XAttribute("y", "457200")));
        if (withText) cm.Add(new XElement(Pml + "text", "hello"));
        return cm;
    }

    private static (Slide slide, CommentAuthorCollection authors) Fixture()
    {
        var authors = new CommentAuthorCollection();
        authors.Add("Alice");
        return (new Slide(), authors);
    }

    [Fact]
    public void Parse_FullComment_WithPositionTimestampAndText()
    {
        var (slide, authors) = Fixture();
        var root = new XElement(
            Pml + "cmLst",
            Comment("0", "1", true, "2024-01-02T03:04:05Z", true)
        );

        CommentParser.Parse(root, slide, authors);

        var c = slide.GetComments().ShouldHaveSingleItem();
        c.Text.ShouldBe("hello");
        c.Position.X.Value.ShouldBe(914400);
        c.CreatedAt.UtcDateTime.Year.ShouldBe(2024);
    }

    [Fact]
    public void Parse_MinimalComment_NoPositionNoTimestampNoText()
    {
        var (slide, authors) = Fixture();
        var root = new XElement(
            Pml + "cmLst",
            Comment("0", "1", false, null, false)
        );

        CommentParser.Parse(root, slide, authors);

        var c = slide.GetComments().ShouldHaveSingleItem();
        c.Text.ShouldBe(string.Empty);
        c.Position.X.ShouldBe(Emu.Zero);
        c.Position.Y.ShouldBe(Emu.Zero);
    }

    [Fact]
    public void Parse_IndexHigherThanAuthorLastIndex_BumpsLastIndex()
    {
        var (slide, authors) = Fixture();
        var root = new XElement(
            Pml + "cmLst",
            Comment("0", "7", false, null, true)
        );

        CommentParser.Parse(root, slide, authors);

        slide.GetComments().ShouldHaveSingleItem().Index.ShouldBe(7u);
    }

    [Fact]
    public void Parse_InvalidAuthorId_SkipsComment()
    {
        var (slide, authors) = Fixture();
        var root = new XElement(
            Pml + "cmLst",
            Comment("notANumber", "1", false, null, true)
        );

        CommentParser.Parse(root, slide, authors);

        slide.GetComments().ShouldBeEmpty();
    }

    [Fact]
    public void Parse_UnknownAuthorId_SkipsComment()
    {
        var (slide, authors) = Fixture();
        var root = new XElement(
            Pml + "cmLst",
            Comment("999", "1", false, null, true)
        );

        CommentParser.Parse(root, slide, authors);

        slide.GetComments().ShouldBeEmpty();
    }

    [Fact]
    public void Parse_InvalidIndex_SkipsComment()
    {
        var (slide, authors) = Fixture();
        var root = new XElement(
            Pml + "cmLst",
            Comment("0", "notANumber", false, null, true)
        );

        CommentParser.Parse(root, slide, authors);

        slide.GetComments().ShouldBeEmpty();
    }
}
