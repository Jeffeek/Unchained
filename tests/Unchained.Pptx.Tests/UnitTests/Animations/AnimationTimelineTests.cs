using System;
using Shouldly;
using Unchained.Pptx.Animations;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Animations;

public sealed class AnimationTimelineTests
{
    [Fact]
    public void Default_NoAnimations()
    {
        var timeline = new AnimationTimeline();
        timeline.MainSequence.ShouldNotBeNull();
        timeline.InteractiveSequences.ShouldBeEmpty();
        timeline.HasAnimations.ShouldBeFalse();
    }

    [Fact]
    public void AddInteractiveSequence_AddsAndSetsTrigger()
    {
        var timeline = new AnimationTimeline();
        var seq = timeline.AddInteractiveSequence(7);
        seq.TriggerShapeId.ShouldBe(7u);
        timeline.InteractiveSequences.Count.ShouldBe(1);
        timeline.HasAnimations.ShouldBeTrue();
    }

    [Fact]
    public void RemoveInteractiveSequence_Removes()
    {
        var timeline = new AnimationTimeline();
        var seq = timeline.AddInteractiveSequence(1);
        timeline.RemoveInteractiveSequence(seq);
        timeline.InteractiveSequences.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveInteractiveSequence_NotInTimeline_Throws()
    {
        var timeline = new AnimationTimeline();
        var alien = new InteractiveSequence();
        Should.Throw<ArgumentException>(() => timeline.RemoveInteractiveSequence(alien));
    }
}

public sealed class InteractiveSequenceTests
{
    [Fact]
    public void Default_HasEmptySequenceAndZeroTrigger()
    {
        var seq = new InteractiveSequence();
        seq.TriggerShapeId.ShouldBe(0u);
        seq.Sequence.ShouldNotBeNull();
        seq.Sequence.Effects.ShouldBeEmpty();
    }

    [Fact]
    public void TriggerShapeId_RoundTrips()
    {
        var seq = new InteractiveSequence { TriggerShapeId = 99 };
        seq.TriggerShapeId.ShouldBe(99u);
    }
}
