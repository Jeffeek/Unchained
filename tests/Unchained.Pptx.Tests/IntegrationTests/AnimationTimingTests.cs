using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     M-E: animation timing detail — acceleration/deceleration, auto-reverse, and repeat count
///     round-trip through save/reload. (The core timeline/sequence/effect model already existed.)
/// </summary>
public sealed class AnimationTimingTests : PptxTestBase
{
    [Fact]
    public async Task EaseAutoReverseRepeat_RoundTrip()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0]
            .Shapes.AddShape(
                AutoShapeType.Rectangle,
                Emu.Zero,
                Emu.Zero,
                Emu.FromInches(2),
                Emu.FromInches(1)
            );
        var effect = doc.Slides[0]
            .Animations.MainSequence.AddEffect(
                shape.ShapeId
            );
        effect.Timing.DurationSeconds = 1.0;
        effect.Timing.AccelerationPercent = 0.3;
        effect.Timing.DecelerationPercent = 0.2;
        effect.Timing.AutoReverse = true;
        effect.Timing.RepeatCount = 3;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var e = reloaded.Slides[0].Animations.MainSequence.Effects.ShouldHaveSingleItem();
        Math.Round(e.Timing.AccelerationPercent, 2).ShouldBe(0.3);
        Math.Round(e.Timing.DecelerationPercent, 2).ShouldBe(0.2);
        e.Timing.AutoReverse.ShouldBeTrue();
        e.Timing.RepeatCount.ShouldBe(3);
    }

    [Fact]
    public async Task IndefiniteRepeat_RoundTrips()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0]
            .Shapes.AddShape(
                AutoShapeType.Ellipse,
                Emu.Zero,
                Emu.Zero,
                Emu.FromInches(2),
                Emu.FromInches(1)
            );
        var effect = doc.Slides[0].Animations.MainSequence.AddEffect(shape.ShapeId);
        effect.Timing.RepeatCount = -1; // indefinite

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var e = reloaded.Slides[0].Animations.MainSequence.Effects.ShouldHaveSingleItem();
        e.Timing.RepeatCount.ShouldBe(-1);
    }
}
