using Shouldly;
using Unchained.Pptx.Animations;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class TransitionTests : PptxTestBase
{
    // ── Default state ─────────────────────────────────────────────────────────

    [Fact]
    public void Transition_DefaultEffect_IsNone()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Transition.Effect.ShouldBe(TransitionEffect.None);
    }

    [Fact]
    public void Transition_DefaultAdvanceOnClick_IsTrue()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Transition.AdvanceOnClick.ShouldBeTrue();
    }

    [Fact]
    public void Transition_DefaultAutoAdvance_IsNull()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Transition.AutoAdvanceSeconds.ShouldBeNull();
    }

    // ── Setting transitions ───────────────────────────────────────────────────

    [Fact]
    public void Transition_SetFade_StoresEffect()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Transition.Effect = TransitionEffect.Fade;
        doc.Slides[0].Transition.Effect.ShouldBe(TransitionEffect.Fade);
    }

    [Fact]
    public void Transition_SetPushLeft_StoresEffect()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Transition.Effect = TransitionEffect.PushLeft;
        doc.Slides[0].Transition.Effect.ShouldBe(TransitionEffect.PushLeft);
    }

    [Fact]
    public void Transition_SetAutoAdvance_StoresDuration()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Transition.AutoAdvanceSeconds = 3.0;
        doc.Slides[0].Transition.AutoAdvanceSeconds.ShouldBe(3.0);
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RoundTrip_Fade_PreservesEffect()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Transition.Effect = TransitionEffect.Fade;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Transition.Effect.ShouldBe(TransitionEffect.Fade);
    }

    [Fact]
    public async Task RoundTrip_PushLeft_PreservesEffect()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Transition.Effect = TransitionEffect.PushLeft;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Transition.Effect.ShouldBe(TransitionEffect.PushLeft);
    }

    [Fact]
    public async Task RoundTrip_PushRight_PreservesEffect()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Transition.Effect = TransitionEffect.PushRight;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Transition.Effect.ShouldBe(TransitionEffect.PushRight);
    }

    [Fact]
    public async Task RoundTrip_WipeLeft_PreservesEffect()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Transition.Effect = TransitionEffect.WipeLeft;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Transition.Effect.ShouldBe(TransitionEffect.WipeLeft);
    }

    [Fact]
    public async Task RoundTrip_Circle_PreservesEffect()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Transition.Effect = TransitionEffect.Circle;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Transition.Effect.ShouldBe(TransitionEffect.Circle);
    }

    [Fact]
    public async Task RoundTrip_ZoomIn_PreservesEffect()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Transition.Effect = TransitionEffect.ZoomIn;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Transition.Effect.ShouldBe(TransitionEffect.ZoomIn);
    }

    [Fact]
    public async Task RoundTrip_Morph_PreservesEffect()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Transition.Effect = TransitionEffect.Morph;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Transition.Effect.ShouldBe(TransitionEffect.Morph);
    }

    [Fact]
    public async Task RoundTrip_Duration_PreservesValue()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Transition.Effect = TransitionEffect.Fade;
        doc.Slides[0].Transition.DurationSeconds = 2.0;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Transition.DurationSeconds.ShouldBe(2.0);
    }

    [Fact]
    public async Task RoundTrip_AdvanceOnClick_PreservesValue()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Transition.Effect = TransitionEffect.Fade;
        doc.Slides[0].Transition.AdvanceOnClick = false;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Transition.AdvanceOnClick.ShouldBeFalse();
    }

    [Fact]
    public async Task RoundTrip_AutoAdvanceSeconds_PreservesValue()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Transition.Effect = TransitionEffect.Fade;
        doc.Slides[0].Transition.AutoAdvanceSeconds = 5.0;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Transition.AutoAdvanceSeconds.ShouldBe(5.0);
    }

    [Fact]
    public async Task RoundTrip_NoTransition_SlideCountPreserved()
    {
        var doc = PptxFixtures.WithSlides(3);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides.Count.ShouldBe(3);
    }

    [Fact]
    public async Task RoundTrip_TransitionOnSlide2_OtherSlidesUnaffected()
    {
        var doc = PptxFixtures.WithSlides(3);
        doc.Slides[1].Transition.Effect = TransitionEffect.WipeRight;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Transition.Effect.ShouldBe(TransitionEffect.None);
        reloaded.Slides[1].Transition.Effect.ShouldBe(TransitionEffect.WipeRight);
        reloaded.Slides[2].Transition.Effect.ShouldBe(TransitionEffect.None);
    }

    [Fact]
    public async Task RoundTrip_BlindsHorizontal_PreservesEffect()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Transition.Effect = TransitionEffect.BlindsHorizontal;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Transition.Effect.ShouldBe(TransitionEffect.BlindsHorizontal);
    }

    [Fact]
    public async Task RoundTrip_CoverDown_PreservesEffect()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Transition.Effect = TransitionEffect.CoverDown;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].Transition.Effect.ShouldBe(TransitionEffect.CoverDown);
    }
}
