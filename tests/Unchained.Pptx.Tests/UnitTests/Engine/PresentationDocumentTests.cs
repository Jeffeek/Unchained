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
}
