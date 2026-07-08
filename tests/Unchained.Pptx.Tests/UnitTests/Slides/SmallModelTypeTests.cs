using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Text;
using Unchained.Pptx.Core;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Slides;

/// <summary>
///     Coverage top-ups for small model types: <see cref="NotesSlide" /> text accessor,
///     <see cref="SlideSize" /> presets and <see cref="object" />-typed equality, and
///     <see cref="HyperlinkReference.Remove" />.
/// </summary>
public sealed class SmallModelTypeTests
{
    // ── NotesSlide ────────────────────────────────────────────────────────────

    [Fact]
    public void NotesSlide_NotesText_DefaultEmpty()
    {
        var notes = new NotesSlide();
        notes.NotesText.ShouldBe(string.Empty);
        notes.NotesTextFrame.ShouldBeNull();
    }

    [Fact]
    public void NotesSlide_SetNotesText_CreatesFrameAndRoundTrips()
    {
        var notes = new NotesSlide { NotesText = "Speaker note" };
        notes.NotesTextFrame.ShouldNotBeNull();
        notes.NotesText.ShouldBe("Speaker note");
    }

    [Fact]
    public void NotesSlide_NotesText_ReflectsExistingFrame()
    {
        var notes = new NotesSlide { NotesTextFrame = new TextFrame { PlainText = "Pre-set" } };
        notes.NotesText.ShouldBe("Pre-set");
    }

    [Fact]
    public void NotesSlide_SetNotesText_WithExistingFrame_ReusesFrame()
    {
        var notes = new NotesSlide { NotesText = "first" };
        var frame = notes.NotesTextFrame;
        notes.NotesText = "second"; // ??= takes the non-null branch, reusing the frame
        notes.NotesTextFrame.ShouldBeSameAs(frame);
        notes.NotesText.ShouldBe("second");
    }

    // ── SlideSize presets + equality ────────────────────────────────────────

    [Fact]
    public void SlideSize_Presets_HaveExpectedDimensions()
    {
        SlideSize.A4Portrait.Width.Value.ShouldBe(7_560_000);
        SlideSize.A4Portrait.Height.Value.ShouldBe(10_692_000);
        SlideSize.A4Landscape.Width.Value.ShouldBe(10_692_000);
        SlideSize.A4Landscape.Height.Value.ShouldBe(7_560_000);
        SlideSize.LetterPortrait.Width.Value.ShouldBe(Emu.FromInches(8.5).Value);
        SlideSize.LetterPortrait.Height.Value.ShouldBe(Emu.FromInches(11).Value);
        SlideSize.LetterLandscape.Width.Value.ShouldBe(Emu.FromInches(11).Value);
        SlideSize.LetterLandscape.Height.Value.ShouldBe(Emu.FromInches(8.5).Value);
    }

    [Fact]
    public void SlideSize_ObjectEquals_MatchesAndRejects()
    {
        object same = new SlideSize(new Emu(10), new Emu(20));
        var value = new SlideSize(new Emu(10), new Emu(20));
        value.Equals(same).ShouldBeTrue();
        // ReSharper disable once SuspiciousTypeConversion.Global
        value.Equals("not a size").ShouldBeFalse();
    }

    // ── HyperlinkReference ────────────────────────────────────────────────────

    [Fact]
    public void HyperlinkReference_Remove_ClearsShapeClickAction()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        var shape = slide.Shapes.AddTextBox(Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(1), "t");
        shape.ClickAction = new HyperlinkAction { Url = "https://example.com" };

        var reference = slide.GetHyperlinks().ShouldHaveSingleItem();
        reference.Slide.ShouldBeSameAs(slide);
        reference.Shape.ShouldBeSameAs(shape);
        reference.Action.Url.ShouldBe("https://example.com");

        reference.Remove();
        shape.ClickAction.ShouldBeNull();
        slide.GetHyperlinks().ShouldBeEmpty();
    }
}
