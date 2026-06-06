using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class CommentsTests : PptxTestBase
{
    // ── CommentAuthorCollection ───────────────────────────────────────────────

    [Fact]
    public void CommentAuthors_Empty_ByDefault()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.CommentAuthors.Count.ShouldBe(0);
    }

    [Fact]
    public void CommentAuthors_Add_StoresAuthor()
    {
        var doc = PptxFixtures.WithSlides(1);
        var author = doc.CommentAuthors.Add("Alice Smith");
        author.Name.ShouldBe("Alice Smith");
        doc.CommentAuthors.Count.ShouldBe(1);
    }

    [Fact]
    public void CommentAuthors_Add_GeneratesInitials()
    {
        var doc = PptxFixtures.WithSlides(1);
        var author = doc.CommentAuthors.Add("Alice Smith");
        author.Initials.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void CommentAuthors_Add_ExplicitInitials()
    {
        var doc = PptxFixtures.WithSlides(1);
        var author = doc.CommentAuthors.Add("Alice Smith", "AS");
        author.Initials.ShouldBe("AS");
    }

    [Fact]
    public void CommentAuthors_FindById_ReturnsCorrectAuthor()
    {
        var doc = PptxFixtures.WithSlides(1);
        var author = doc.CommentAuthors.Add("Bob");
        doc.CommentAuthors.FindById(author.Id).ShouldBe(author);
    }

    // ── Slide.AddComment ──────────────────────────────────────────────────────

    [Fact]
    public void AddComment_CreatesComment()
    {
        var doc = PptxFixtures.WithSlides(1);
        var author = doc.CommentAuthors.Add("Alice");
        var pos = new SlidePosition(Emu.FromInches(1), Emu.FromInches(1));

        var comment = doc.Slides[0].AddComment("Hello", pos, author);

        comment.ShouldNotBeNull();
        comment.Text.ShouldBe("Hello");
        comment.Author.ShouldBe(author);
    }

    [Fact]
    public void AddComment_GetComments_ReturnsIt()
    {
        var doc = PptxFixtures.WithSlides(1);
        var author = doc.CommentAuthors.Add("Alice");
        var pos = new SlidePosition(Emu.Zero, Emu.Zero);

        doc.Slides[0].AddComment("Test", pos, author);

        doc.Slides[0].GetComments().Count.ShouldBe(1);
    }

    [Fact]
    public void AddComment_Position_Stored()
    {
        var doc = PptxFixtures.WithSlides(1);
        var author = doc.CommentAuthors.Add("Alice");
        var x = Emu.FromInches(2);
        var y = Emu.FromInches(3);
        var pos = new SlidePosition(x, y);

        var comment = doc.Slides[0].AddComment("Text", pos, author);

        comment.Position.X.ShouldBe(x);
        comment.Position.Y.ShouldBe(y);
    }

    [Fact]
    public void AddComment_MultipleComments_AllStored()
    {
        var doc = PptxFixtures.WithSlides(1);
        var author = doc.CommentAuthors.Add("Alice");
        var pos = new SlidePosition(Emu.Zero, Emu.Zero);

        doc.Slides[0].AddComment("First", pos, author);
        doc.Slides[0].AddComment("Second", pos, author);

        doc.Slides[0].GetComments().Count.ShouldBe(2);
    }

    [Fact]
    public void RemoveComment_DecreasesCount()
    {
        var doc = PptxFixtures.WithSlides(1);
        var author = doc.CommentAuthors.Add("Alice");
        var pos = new SlidePosition(Emu.Zero, Emu.Zero);

        var comment = doc.Slides[0].AddComment("To remove", pos, author);
        doc.Slides[0].RemoveComment(comment);

        doc.Slides[0].GetComments().Count.ShouldBe(0);
    }

    [Fact]
    public void HasComments_FalseWhenNoComments()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].HasComments.ShouldBeFalse();
    }

    [Fact]
    public void HasComments_TrueAfterAddComment()
    {
        var doc = PptxFixtures.WithSlides(1);
        var author = doc.CommentAuthors.Add("Alice");
        doc.Slides[0].AddComment("Hi", new SlidePosition(Emu.Zero, Emu.Zero), author);
        doc.Slides[0].HasComments.ShouldBeTrue();
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RoundTrip_CommentText_Preserved()
    {
        var doc = PptxFixtures.WithSlides(1);
        var author = doc.CommentAuthors.Add("Alice");
        var pos = new SlidePosition(Emu.FromInches(1), Emu.FromInches(1));
        doc.Slides[0].AddComment("Review this slide", pos, author);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        reloaded.Slides[0].GetComments().Count.ShouldBe(1);
        reloaded.Slides[0].GetComments()[0].Text.ShouldBe("Review this slide");
    }

    [Fact]
    public async Task RoundTrip_CommentAuthor_Preserved()
    {
        var doc = PptxFixtures.WithSlides(1);
        var author = doc.CommentAuthors.Add("Bob Jones", "BJ");
        doc.Slides[0].AddComment("Note", new SlidePosition(Emu.Zero, Emu.Zero), author);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        reloaded.Slides[0].GetComments()[0].Author.Name.ShouldBe("Bob Jones");
    }

    [Fact]
    public async Task RoundTrip_CommentPosition_Preserved()
    {
        var doc = PptxFixtures.WithSlides(1);
        var author = doc.CommentAuthors.Add("Alice");
        var x = Emu.FromInches(2);
        var y = Emu.FromInches(1.5);
        doc.Slides[0].AddComment("Note", new SlidePosition(x, y), author);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        var pos = reloaded.Slides[0].GetComments()[0].Position;
        pos.X.ShouldBe(x);
        pos.Y.ShouldBe(y);
    }

    [Fact]
    public async Task RoundTrip_MultipleComments_AllPreserved()
    {
        var doc = PptxFixtures.WithSlides(1);
        var author = doc.CommentAuthors.Add("Alice");
        var pos = new SlidePosition(Emu.Zero, Emu.Zero);
        doc.Slides[0].AddComment("First comment", pos, author);
        doc.Slides[0].AddComment("Second comment", pos, author);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        reloaded.Slides[0].GetComments().Count.ShouldBe(2);
    }

    [Fact]
    public async Task RoundTrip_NoComments_SlideLoadsClean()
    {
        var doc = PptxFixtures.WithSlides(1);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].GetComments().Count.ShouldBe(0);
    }

    [Fact]
    public async Task RoundTrip_AuthorCollection_Preserved()
    {
        var doc = PptxFixtures.WithSlides(1);
        var author = doc.CommentAuthors.Add("Charlie", "CK");
        doc.Slides[0].AddComment("Hi", new SlidePosition(Emu.Zero, Emu.Zero), author);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        reloaded.CommentAuthors.Count.ShouldBe(1);
        reloaded.CommentAuthors[0].Name.ShouldBe("Charlie");
    }

    [Fact]
    public async Task RoundTrip_CommentsOnDifferentSlides_BothPreserved()
    {
        var doc = PptxFixtures.WithSlides(2);
        var author = doc.CommentAuthors.Add("Alice");
        doc.Slides[0].AddComment("Slide 1 note", new SlidePosition(Emu.Zero, Emu.Zero), author);
        doc.Slides[1].AddComment("Slide 2 note", new SlidePosition(Emu.Zero, Emu.Zero), author);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        reloaded.Slides[0].GetComments()[0].Text.ShouldBe("Slide 1 note");
        reloaded.Slides[1].GetComments()[0].Text.ShouldBe("Slide 2 note");
    }
}
