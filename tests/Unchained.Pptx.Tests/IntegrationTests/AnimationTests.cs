using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Animations;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class AnimationTests : PptxTestBase
{
    // ── Default state ─────────────────────────────────────────────────────────

    [Fact]
    public void Timeline_DefaultMainSequence_IsEmpty()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Animations.MainSequence.Effects.Count.ShouldBe(0);
    }

    [Fact]
    public void Timeline_HasAnimations_FalseWhenEmpty()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Animations.HasAnimations.ShouldBeFalse();
    }

    // ── AddEffect ─────────────────────────────────────────────────────────────

    [Fact]
    public void AddEffect_IncreasesEffectCount()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        var shape = slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(3),
            Emu.FromInches(2));

        slide.Animations.MainSequence.AddEffect(shape.ShapeId);

        slide.Animations.MainSequence.Effects.Count.ShouldBe(1);
    }

    [Fact]
    public void AddEffect_SetsTargetShapeId()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        var shape = slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(3),
            Emu.FromInches(2));

        var effect = slide.Animations.MainSequence.AddEffect(shape.ShapeId);

        effect.TargetShapeId.ShouldBe(shape.ShapeId);
    }

    [Fact]
    public void AddEffect_DefaultsToFadeEntrance()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        var shape = slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.Zero,
            Emu.Zero,
            Emu.FromInches(2),
            Emu.FromInches(1));

        var effect = slide.Animations.MainSequence.AddEffect(shape.ShapeId);

        effect.Preset.ShouldBe(AnimationPreset.Fade);
        effect.Category.ShouldBe(EffectCategory.Entrance);
        effect.Trigger.ShouldBe(EffectTrigger.OnClick);
    }

    [Fact]
    public void AddEffect_CustomPreset_Stored()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        var shape = slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.Zero,
            Emu.Zero,
            Emu.FromInches(2),
            Emu.FromInches(1));

        var effect = slide.Animations.MainSequence.AddEffect(
            shape.ShapeId,
            AnimationPreset.Appear,
            EffectCategory.Entrance,
            EffectTrigger.WithPrevious);

        effect.Preset.ShouldBe(AnimationPreset.Appear);
        effect.Trigger.ShouldBe(EffectTrigger.WithPrevious);
    }

    [Fact]
    public void AddEffect_MultipleEffects_AllStored()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        var s1 = slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.Zero,
            Emu.Zero,
            Emu.FromInches(2),
            Emu.FromInches(1));
        var s2 = slide.Shapes.AddShape(AutoShapeType.Ellipse,
            Emu.FromInches(3),
            Emu.Zero,
            Emu.FromInches(2),
            Emu.FromInches(1));

        slide.Animations.MainSequence.AddEffect(s1.ShapeId);
        slide.Animations.MainSequence.AddEffect(s2.ShapeId,
            trigger: EffectTrigger.AfterPrevious);

        slide.Animations.MainSequence.Effects.Count.ShouldBe(2);
        slide.Animations.MainSequence.Effects[1].Trigger.ShouldBe(EffectTrigger.AfterPrevious);
    }

    [Fact]
    public void HasAnimations_TrueAfterAddEffect()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        var shape = slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.Zero,
            Emu.Zero,
            Emu.FromInches(2),
            Emu.FromInches(1));

        slide.Animations.MainSequence.AddEffect(shape.ShapeId);

        slide.Animations.HasAnimations.ShouldBeTrue();
    }

    [Fact]
    public void Remove_DecreasesEffectCount()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        var shape = slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.Zero,
            Emu.Zero,
            Emu.FromInches(2),
            Emu.FromInches(1));

        var effect = slide.Animations.MainSequence.AddEffect(shape.ShapeId);
        slide.Animations.MainSequence.Remove(effect);

        slide.Animations.MainSequence.Effects.Count.ShouldBe(0);
    }

    [Fact]
    public void Clear_RemovesAllEffects()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        var shape = slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.Zero,
            Emu.Zero,
            Emu.FromInches(2),
            Emu.FromInches(1));

        slide.Animations.MainSequence.AddEffect(shape.ShapeId);
        slide.Animations.MainSequence.AddEffect(shape.ShapeId);
        slide.Animations.MainSequence.Clear();

        slide.Animations.MainSequence.Effects.Count.ShouldBe(0);
    }

    // ── EffectTiming ──────────────────────────────────────────────────────────

    [Fact]
    public void EffectTiming_DefaultDuration_IsHalfSecond()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        var shape = slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.Zero,
            Emu.Zero,
            Emu.FromInches(2),
            Emu.FromInches(1));

        var effect = slide.Animations.MainSequence.AddEffect(shape.ShapeId);
        effect.Timing.DurationSeconds.ShouldBe(0.5);
    }

    [Fact]
    public void EffectTiming_SetDelay_Stored()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        var shape = slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.Zero,
            Emu.Zero,
            Emu.FromInches(2),
            Emu.FromInches(1));

        var effect = slide.Animations.MainSequence.AddEffect(shape.ShapeId,
            delaySeconds: 1.5);

        effect.Timing.DelaySeconds.ShouldBe(1.5);
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RoundTrip_SingleFadeEntrance_PreservesEffect()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        var shape = slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(3),
            Emu.FromInches(2));
        slide.Animations.MainSequence.AddEffect(
            shape.ShapeId);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var timeline = reloaded.Slides[0].Animations;

        timeline.MainSequence.Effects.Count.ShouldBe(1);
        timeline.MainSequence.Effects[0].Preset.ShouldBe(AnimationPreset.Fade);
        timeline.MainSequence.Effects[0].Category.ShouldBe(EffectCategory.Entrance);
    }

    [Fact]
    public async Task RoundTrip_SingleFadeEntrance_PreservesTargetShapeId()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        var shape = slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(3),
            Emu.FromInches(2));
        slide.Animations.MainSequence.AddEffect(shape.ShapeId);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        reloaded.Slides[0].Animations.MainSequence.Effects[0]
            .TargetShapeId.ShouldBe(shape.ShapeId);
    }

    [Fact]
    public async Task RoundTrip_OnClickTrigger_Preserved()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        var shape = slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.Zero,
            Emu.Zero,
            Emu.FromInches(2),
            Emu.FromInches(1));
        slide.Animations.MainSequence.AddEffect(
            shape.ShapeId,
            trigger: EffectTrigger.OnClick);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        reloaded.Slides[0].Animations.MainSequence.Effects[0]
            .Trigger.ShouldBe(EffectTrigger.OnClick);
    }

    [Fact]
    public async Task RoundTrip_MultipleEffects_CountPreserved()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        var s1 = slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.Zero,
            Emu.Zero,
            Emu.FromInches(2),
            Emu.FromInches(1));
        var s2 = slide.Shapes.AddShape(AutoShapeType.Ellipse,
            Emu.FromInches(3),
            Emu.Zero,
            Emu.FromInches(2),
            Emu.FromInches(1));

        slide.Animations.MainSequence.AddEffect(s1.ShapeId);
        slide.Animations.MainSequence.AddEffect(s2.ShapeId,
            trigger: EffectTrigger.OnClick);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Animations.MainSequence.Effects.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RoundTrip_AppearEntrance_Preserved()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        var shape = slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.Zero,
            Emu.Zero,
            Emu.FromInches(2),
            Emu.FromInches(1));
        slide.Animations.MainSequence.AddEffect(
            shape.ShapeId,
            AnimationPreset.Appear);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        reloaded.Slides[0].Animations.MainSequence.Effects[0]
            .Preset.ShouldBe(AnimationPreset.Appear);
    }

    [Fact]
    public async Task RoundTrip_ExitEffect_CategoryPreserved()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        var shape = slide.Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.Zero,
            Emu.Zero,
            Emu.FromInches(2),
            Emu.FromInches(1));
        slide.Animations.MainSequence.AddEffect(
            shape.ShapeId,
            AnimationPreset.Fade,
            EffectCategory.Exit);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        reloaded.Slides[0].Animations.MainSequence.Effects[0]
            .Category.ShouldBe(EffectCategory.Exit);
    }

    [Fact]
    public async Task RoundTrip_NoAnimations_SlideStillLoads()
    {
        var doc = PptxFixtures.WithSlides(2);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides.Count.ShouldBe(2);
        reloaded.Slides[0].Animations.HasAnimations.ShouldBeFalse();
    }

    [Fact]
    public async Task RoundTrip_AnimationOnSlide2_OtherSlideUnaffected()
    {
        var doc = PptxFixtures.WithSlides(2);
        var shape = doc.Slides[1].Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.Zero,
            Emu.Zero,
            Emu.FromInches(2),
            Emu.FromInches(1));
        doc.Slides[1].Animations.MainSequence.AddEffect(shape.ShapeId);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        reloaded.Slides[0].Animations.HasAnimations.ShouldBeFalse();
        reloaded.Slides[1].Animations.HasAnimations.ShouldBeTrue();
    }
}
