using Shouldly;
using Unchained.Pptx.Animations;
using Unchained.Pptx.Parsing;
using Unchained.Pptx.Writing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Writing;

public sealed class AnimationWriterTests
{
    [Fact]
    public void Write_EmptyTimeline_ReturnsNull() => AnimationWriter.Write(new AnimationTimeline()).ShouldBeNull();

    [Fact]
    public void Write_WithEffect_ProducesTimingElement()
    {
        var timeline = new AnimationTimeline();
        // ReSharper disable RedundantArgumentDefaultValue
        timeline.MainSequence.AddEffect(5, AnimationPreset.Fade, EffectCategory.Entrance);
        // ReSharper restore RedundantArgumentDefaultValue
        var el = AnimationWriter.Write(timeline);
        el.ShouldNotBeNull();
        el.Name.LocalName.ShouldBe("timing");
    }

    [Fact]
    public void Write_EmitsBuildListEntryPerEffect()
    {
        var timeline = new AnimationTimeline();
        // ReSharper disable once RedundantArgumentDefaultValue
        timeline.MainSequence.AddEffect(5, AnimationPreset.Fade);
        timeline.MainSequence.AddEffect(6, AnimationPreset.Fly);
        var el = AnimationWriter.Write(timeline);
        el.ShouldNotBeNull();
        el.Descendants().Count(static e => e.Name.LocalName == "bldP").ShouldBe(2);
    }

    [Fact]
    public void RoundTrip_SingleEntrance_PreservesShapeAndPreset()
    {
        var timeline = new AnimationTimeline();
        // ReSharper disable once RedundantArgumentDefaultValue
        timeline.MainSequence.AddEffect(42, AnimationPreset.Fly, EffectCategory.Entrance);

        var el = AnimationWriter.Write(timeline);
        el.ShouldNotBeNull();

        var parsed = new AnimationTimeline();
        AnimationParser.Parse(el, parsed);

        parsed.MainSequence.Effects.Count.ShouldBe(1);
        var effect = parsed.MainSequence.Effects[0];
        effect.TargetShapeId.ShouldBe(42u);
        effect.Preset.ShouldBe(AnimationPreset.Fly);
        effect.Category.ShouldBe(EffectCategory.Entrance);
    }

    [Fact]
    public void RoundTrip_ExitCategory_Preserved()
    {
        var timeline = new AnimationTimeline();
        timeline.MainSequence.AddEffect(7, AnimationPreset.Fade, EffectCategory.Exit);

        var el = AnimationWriter.Write(timeline);
        el.ShouldNotBeNull();
        var parsed = new AnimationTimeline();
        AnimationParser.Parse(el, parsed);

        parsed.MainSequence.Effects[0].Category.ShouldBe(EffectCategory.Exit);
    }

    [Fact]
    public void RoundTrip_Duration_Preserved()
    {
        var timeline = new AnimationTimeline();
        var effect = timeline.MainSequence.AddEffect(3, AnimationPreset.Zoom);
        effect.Timing.DurationSeconds = 2.0;

        var el = AnimationWriter.Write(timeline);
        el.ShouldNotBeNull();
        var parsed = new AnimationTimeline();
        AnimationParser.Parse(el, parsed);

        parsed.MainSequence.Effects[0].Timing.DurationSeconds.ShouldBe(2.0, 0.001);
    }

    [Fact]
    public void RoundTrip_MultipleEffectsInClickGroup_Preserved()
    {
        var timeline = new AnimationTimeline();
        // ReSharper disable RedundantArgumentDefaultValue
        timeline.MainSequence.AddEffect(1, AnimationPreset.Fade, EffectCategory.Entrance, EffectTrigger.OnClick);
        // ReSharper restore RedundantArgumentDefaultValue
        timeline.MainSequence.AddEffect(2, AnimationPreset.Fade, EffectCategory.Entrance, EffectTrigger.WithPrevious);

        var el = AnimationWriter.Write(timeline);
        el.ShouldNotBeNull();
        var parsed = new AnimationTimeline();
        AnimationParser.Parse(el, parsed);

        parsed.MainSequence.Effects.Count.ShouldBe(2);
        parsed.MainSequence.Effects.Select(static e => e.TargetShapeId).ShouldBe([1u, 2u]);
    }

    [Fact]
    public void Write_InteractiveSequence_NestsTriggerShapeIdUnderSpTgt()
    {
        var timeline = new AnimationTimeline();
        var interactive = timeline.AddInteractiveSequence(99);
        // ReSharper disable RedundantArgumentDefaultValue
        interactive.Sequence.AddEffect(10, AnimationPreset.Fade, EffectCategory.Entrance);
        // ReSharper restore RedundantArgumentDefaultValue

        var el = AnimationWriter.Write(timeline);
        el.ShouldNotBeNull();

        // The interactive sequence carries an interactiveSeq cTn …
        el.Descendants()
            .Any(static e => e.Name.LocalName == "cTn" && (string?)e.Attribute("nodeType") == "interactiveSeq")
            .ShouldBeTrue();

        // … and the trigger shape id lives in prevCondLst/cond/tgtEl/spTgt/@spid.
        var prevCond = el.Descendants().Single(static e => e.Name.LocalName == "prevCondLst");
        var spTgt = prevCond.Descendants().Single(static e => e.Name.LocalName == "spTgt");
        spTgt.Attribute("spid")!.Value.ShouldBe("99");
    }

    [Fact]
    public void RoundTrip_InteractiveSequence_PreservesTriggerAndEffect()
    {
        var timeline = new AnimationTimeline();
        var interactive = timeline.AddInteractiveSequence(99);
        // ReSharper disable RedundantArgumentDefaultValue
        interactive.Sequence.AddEffect(10, AnimationPreset.Fade, EffectCategory.Entrance);
        // ReSharper restore RedundantArgumentDefaultValue

        var el = AnimationWriter.Write(timeline);
        el.ShouldNotBeNull();

        var parsed = new AnimationTimeline();
        AnimationParser.Parse(el, parsed);

        parsed.InteractiveSequences.Count.ShouldBe(1);
        parsed.InteractiveSequences[0].TriggerShapeId.ShouldBe(99u);
        parsed.InteractiveSequences[0].Sequence.Effects[0].TargetShapeId.ShouldBe(10u);
    }
}
