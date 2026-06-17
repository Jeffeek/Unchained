using Shouldly;
using Unchained.Drawing;
using Unchained.Drawing.Encoders;
using Unchained.Ooxml;
using Unchained.Pptx.Animations;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Round-trip coverage for the comment, animation, and ODP parsers across their less-common
///     branches: multi-author comments with explicit timestamps, interactive animation sequences,
///     and ODP image import.
/// </summary>
public sealed class ParserCoverageTests : PptxTestBase
{
    private static byte[] SmallPng()
    {
        var buffer = new RasterBuffer(4, 4);
        buffer.Clear(60, 120, 240);
        return PngEncoder.Encode(buffer);
    }

    // ── CommentParser ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Comments_MultipleAuthors_RoundTrip()
    {
        var doc = PptxFixtures.WithSlides(1);
        var alice = doc.CommentAuthors.Add("Alice");
        var bob = doc.CommentAuthors.Add("Bob");
        doc.Slides[0].AddComment("from alice", new SlidePosition(Emu.FromInches(1), Emu.FromInches(1)), alice);
        doc.Slides[0].AddComment("from bob", new SlidePosition(Emu.FromInches(2), Emu.FromInches(2)), bob);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.CommentAuthors.Count.ShouldBe(2);
        var comments = reloaded.Slides[0].GetComments();
        comments.Count.ShouldBe(2);
        comments.Select(static c => c.Author.Name).ShouldBe(["Alice", "Bob"], true);
    }

    [Fact]
    public async Task Comment_Timestamp_RoundTrips()
    {
        var doc = PptxFixtures.WithSlides(1);
        var author = doc.CommentAuthors.Add("Carol");
        var comment = doc.Slides[0].AddComment("timed", new SlidePosition(Emu.Zero, Emu.Zero), author);
        var when = comment.CreatedAt;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var rc = reloaded.Slides[0].GetComments().Single();
        // Timestamps round-trip to second precision through the OOXML dt attribute.
        rc.CreatedAt.UtcDateTime.ShouldBe(when.UtcDateTime, TimeSpan.FromSeconds(2));
    }

    // ── AnimationParser ──────────────────────────────────────────────────────────

    [Fact]
    public async Task InteractiveSequence_RoundTrips()
    {
        var doc = PptxFixtures.WithSlides(1);
        var trigger = doc.Slides[0]
            .Shapes.AddShape(AutoShapeType.Rectangle, Emu.Zero, Emu.Zero, Emu.FromInches(1), Emu.FromInches(1));
        var target = doc.Slides[0]
            .Shapes.AddShape(AutoShapeType.Ellipse, Emu.FromInches(2), Emu.Zero, Emu.FromInches(1), Emu.FromInches(1));

        var seq = doc.Slides[0].Animations.AddInteractiveSequence(trigger.ShapeId);
        seq.Sequence.AddEffect(target.ShapeId);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var rseq = reloaded.Slides[0].Animations.InteractiveSequences;
        rseq.Count.ShouldBe(1);
        rseq[0].TriggerShapeId.ShouldBe(trigger.ShapeId);
        rseq[0].Sequence.Effects.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task MainSequence_MixedTriggers_RoundTrip()
    {
        var doc = PptxFixtures.WithSlides(1);
        var s1 = doc.Slides[0]
            .Shapes.AddShape(AutoShapeType.Rectangle, Emu.Zero, Emu.Zero, Emu.FromInches(1), Emu.FromInches(1));
        var s2 = doc.Slides[0]
            .Shapes.AddShape(AutoShapeType.Ellipse, Emu.FromInches(2), Emu.Zero, Emu.FromInches(1), Emu.FromInches(1));

        doc.Slides[0].Animations.MainSequence.AddEffect(s1.ShapeId);
        doc.Slides[0].Animations.MainSequence.AddEffect(s2.ShapeId, AnimationPreset.Fly, EffectCategory.Entrance, EffectTrigger.AfterPrevious);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var effects = reloaded.Slides[0].Animations.MainSequence.Effects;
        effects.Count.ShouldBe(2);
        effects[1].Trigger.ShouldBe(EffectTrigger.AfterPrevious);
    }

    [Fact]
    public async Task ExitEmphasisCategories_RoundTrip()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0]
            .Shapes.AddShape(AutoShapeType.Rectangle, Emu.Zero, Emu.Zero, Emu.FromInches(1), Emu.FromInches(1));
        doc.Slides[0].Animations.MainSequence.AddEffect(shape.ShapeId, AnimationPreset.Fade, EffectCategory.Exit, EffectTrigger.WithPrevious);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Animations.MainSequence.Effects.Single().Category.ShouldBe(EffectCategory.Exit);
    }

    // ── OdpParser ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Odp_ImageRoundTrips()
    {
        var doc = PptxFixtures.WithSlides(1);
        var image = doc.Media.AddImage(SmallPng(), "image/png");
        doc.Slides[0].Shapes.AddPicture(image, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(2));

        var odp = await Processor.ExportOdpAsync(doc);
        var reloaded = await Processor.LoadAsync(odp);

        reloaded.Slides[0].Shapes.OfType<PictureShape>().ShouldNotBeEmpty();
        reloaded.Media.Images.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Odp_MultipleSlidesWithTextAndImage_RoundTrip()
    {
        var doc = PptxFixtures.WithSlides(2);
        doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(4), Emu.FromInches(1), "page one");
        var image = doc.Media.AddImage(SmallPng(), "image/png");
        doc.Slides[1].Shapes.AddPicture(image, Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(2));

        var odp = await Processor.ExportOdpAsync(doc);
        var reloaded = await Processor.LoadAsync(odp);

        reloaded.Slides.Count.ShouldBe(2);
        reloaded.Slides[0].GetAllText().ShouldContain("page one");
        reloaded.Slides[1].Shapes.OfType<PictureShape>().ShouldNotBeEmpty();
    }
}
