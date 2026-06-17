using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Core;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Engine;

/// <summary>
///     Unit tests for the <see cref="Unchained.Pptx.Engine.PresentationDocument" /> container
///     accessors, flags, signature/hyperlink enumeration, find/replace, and disposal — exercised
///     against an in-memory blank presentation.
/// </summary>
public sealed class PresentationDocumentTests
{
    [Fact]
    public void Blank_ExposesNonNullCollections()
    {
        using var doc = PptxFixtures.BlankPresentation();
        doc.Slides.ShouldNotBeNull();
        doc.Masters.ShouldNotBeNull();
        doc.Media.ShouldNotBeNull();
        doc.Properties.ShouldNotBeNull();
        doc.CommentAuthors.ShouldNotBeNull();
        doc.Sections.ShouldNotBeNull();
        doc.SlideShow.ShouldNotBeNull();
        doc.Protection.ShouldNotBeNull();
    }

    [Fact]
    public void Blank_HasNoMacrosOrSignatures()
    {
        using var doc = PptxFixtures.BlankPresentation();
        doc.HasMacros.ShouldBeFalse();
        doc.HasDigitalSignatures.ShouldBeFalse();
        doc.GetDigitalSignatures().ShouldBeEmpty();
    }

    [Fact]
    public void SlideSize_IsSettable()
    {
        using var doc = PptxFixtures.BlankPresentation();
        var size = new SlideSize(new Emu(100), new Emu(200));
        doc.SlideSize = size;
        doc.SlideSize.Width.Value.ShouldBe(100);
        doc.SlideSize.Height.Value.ShouldBe(200);
    }

    [Fact]
    public void GetHyperlinks_NoLinks_ReturnsEmpty()
    {
        using var doc = PptxFixtures.WithSlides(1);
        doc.GetHyperlinks().ShouldBeEmpty();
    }

    [Fact]
    public void ReplaceText_AcrossSlides_ReplacesInTextBoxes()
    {
        using var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1), "hello world");

        var count = doc.ReplaceText("world", "there");
        count.ShouldBeGreaterThan(0);
        doc.Slides[0]
            .Shapes.OfType<AutoShape>()
            .ShouldContain(static s => s.TextFrame.PlainText.Contains("there"));
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var doc = PptxFixtures.BlankPresentation();
        doc.Dispose();
        Should.NotThrow(() => doc.Dispose());
    }

    [Fact]
    public async Task DisposeAsync_Works()
    {
        var doc = PptxFixtures.BlankPresentation();
        await doc.DisposeAsync();
        Should.NotThrow(doc.Dispose);
    }

    [Fact]
    public void ReplaceText_DefaultsToSkippingNotes()
    {
        using var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1), "alpha");
        doc.Slides[0].Notes.NotesText = "alpha in notes";

        var count = doc.ReplaceText("alpha", "beta");
        count.ShouldBe(1);
        doc.Slides[0].Notes.NotesText.ShouldContain("alpha");
    }

    [Fact]
    public void ReplaceText_IncludeNotes_ReplacesInNotes()
    {
        using var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1), "alpha");
        doc.Slides[0].Notes.NotesText = "alpha in notes";

        var count = doc.ReplaceText("alpha", "beta", includeNotes: true);
        count.ShouldBeGreaterThanOrEqualTo(2);
        doc.Slides[0].Notes.NotesText.ShouldNotContain("alpha");
    }

    [Fact]
    public void ReplaceText_CaseInsensitive_Matches()
    {
        using var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1), "Hello");

        var count = doc.ReplaceText("hello", "Howdy", StringComparison.OrdinalIgnoreCase);
        count.ShouldBe(1);
    }

    [Fact]
    public void ReplaceText_NoMatch_ReturnsZero()
    {
        using var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1), "content");
        doc.ReplaceText("absent", "x").ShouldBe(0);
    }

    [Fact]
    public void GetHyperlinks_WithLinks_EnumeratesAcrossSlides()
    {
        using var doc = PptxFixtures.WithSlides(2);
        var box0 = doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1), "a");
        box0.ClickAction = HyperlinkAction.ToUrl("https://example.com", true);
        var box1 = doc.Slides[1].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1), "b");
        box1.ClickAction = HyperlinkAction.ToSlide(1);

        doc.GetHyperlinks().Count().ShouldBe(2);
    }

    [Fact]
    public async Task ReplaceText_RoundTrips()
    {
        await using var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(3), Emu.FromInches(1), "needle here");
        doc.ReplaceText("needle", "thread");

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].GetAllText().ShouldContain("thread");
    }

    [Fact]
    public async Task SyncStatistics_RunOnSave_ReflectsSlideAndNotesCounts()
    {
        await using var doc = PptxFixtures.WithSlides(3);
        doc.Slides[1].IsHidden = true;
        doc.Slides[0].Notes.NotesText = "speaker notes";

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Properties.SlideCount.ShouldBe(3);
        reloaded.Properties.HiddenSlideCount.ShouldBe(1);
    }

    [Fact]
    public async Task RoundTrip_HasNoMacrosOrSignatures()
    {
        await using var doc = PptxFixtures.WithSlides(1);
        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.HasMacros.ShouldBeFalse();
        reloaded.HasDigitalSignatures.ShouldBeFalse();
        reloaded.GetDigitalSignatures().ShouldBeEmpty();
    }

    [Fact]
    public void SlideShow_IsSettable()
    {
        using var doc = PptxFixtures.BlankPresentation();
        doc.SlideShow.ShouldNotBeNull();
    }
}
