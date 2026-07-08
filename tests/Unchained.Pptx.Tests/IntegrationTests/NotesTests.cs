using Shouldly;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class NotesTests : PptxTestBase
{
    // ── Default state ─────────────────────────────────────────────────────────

    [Fact]
    public void Notes_DefaultText_IsEmpty()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Notes.NotesText.ShouldBe(string.Empty);
    }

    [Fact]
    public void Notes_HasNotes_FalseByDefault()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].HasNotes.ShouldBeFalse();
    }

    // ── Setting notes ─────────────────────────────────────────────────────────

    [Fact]
    public void Notes_SetText_StoresText()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Notes.NotesText = "My speaker notes";
        doc.Slides[0].Notes.NotesText.ShouldBe("My speaker notes");
    }

    [Fact]
    public void Notes_SetText_HasNotesBecomesTrue()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Notes.NotesText = "Notes here";
        doc.Slides[0].HasNotes.ShouldBeTrue();
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RoundTrip_NotesText_Preserved()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Notes.NotesText = "My speaker notes";

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Notes.NotesText.ShouldBe("My speaker notes");
    }

    [Fact]
    public async Task RoundTrip_EmptyNotes_SlideLoadsClean()
    {
        var doc = PptxFixtures.WithSlides(1);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Notes.NotesText.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task RoundTrip_MultilineNotes_Preserved()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Notes.NotesText = "Line one\nLine two\nLine three";

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Notes.NotesText.ShouldBe("Line one\nLine two\nLine three");
    }

    [Fact]
    public async Task RoundTrip_NotesOnSlide2_OtherSlideUnaffected()
    {
        var doc = PptxFixtures.WithSlides(3);
        doc.Slides[1].Notes.NotesText = "Slide 2 notes";

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Notes.NotesText.ShouldBe(string.Empty);
        reloaded.Slides[1].Notes.NotesText.ShouldBe("Slide 2 notes");
        reloaded.Slides[2].Notes.NotesText.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task RoundTrip_MultipleSlideNotes_AllPreserved()
    {
        var doc = PptxFixtures.WithSlides(3);
        doc.Slides[0].Notes.NotesText = "Notes for slide 1";
        doc.Slides[2].Notes.NotesText = "Notes for slide 3";

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Notes.NotesText.ShouldBe("Notes for slide 1");
        reloaded.Slides[1].Notes.NotesText.ShouldBe(string.Empty);
        reloaded.Slides[2].Notes.NotesText.ShouldBe("Notes for slide 3");
    }

    [Fact]
    public async Task RoundTrip_LongNotesText_Preserved()
    {
        var doc = PptxFixtures.WithSlides(1);
        var longText = string.Concat(Enumerable.Repeat("This is a long speaker note. ", 20));
        doc.Slides[0].Notes.NotesText = longText;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Notes.NotesText.ShouldBe(longText);
    }

    [Fact]
    public async Task RoundTrip_SlideCountUnaffectedByNotes()
    {
        var doc = PptxFixtures.WithSlides(2);
        doc.Slides[0].Notes.NotesText = "Some notes";

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides.Count.ShouldBe(2);
    }
}
