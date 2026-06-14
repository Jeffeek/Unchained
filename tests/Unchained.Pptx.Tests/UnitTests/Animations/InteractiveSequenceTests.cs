using Shouldly;
using Unchained.Pptx.Animations;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Animations;

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
