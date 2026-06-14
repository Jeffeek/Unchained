using Shouldly;
using Unchained.Pptx.Animations;
using Unchained.Pptx.Parsing;
using Unchained.Pptx.Writing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Writing;

public sealed class TransitionWriterTests
{
    public static IEnumerable<object[]> AllTransitions =>
        Enum.GetValues<TransitionEffect>()
            .Where(static e => e != TransitionEffect.None)
            .Select(static e => new object[] { e });

    [Fact]
    public void Write_DefaultNoEffect_ReturnsNull()
    {
        var transition = new SlideTransition();
        TransitionWriter.Write(transition).ShouldBeNull();
    }

    [Fact]
    public void Write_Effect_ProducesTransitionElement()
    {
        var transition = new SlideTransition { Effect = TransitionEffect.Fade };
        var el = TransitionWriter.Write(transition);
        el.ShouldNotBeNull();
        el.Name.LocalName.ShouldBe("transition");
        el.Elements().Single().Name.LocalName.ShouldBe("fade");
    }

    [Fact]
    public void Write_Duration_EmittedInMilliseconds()
    {
        var transition = new SlideTransition { Effect = TransitionEffect.Cut, DurationSeconds = 1.5 };
        var el = TransitionWriter.Write(transition);
        el.ShouldNotBeNull();
        el.Attribute("dur")!.Value.ShouldBe("1500");
    }

    [Fact]
    public void Write_AdvanceFlags_Emitted()
    {
        var transition = new SlideTransition
        {
            Effect = TransitionEffect.Cut,
            AdvanceOnClick = false,
            AutoAdvanceSeconds = 2.0
        };
        var el = TransitionWriter.Write(transition);
        el.ShouldNotBeNull();
        el.Attribute("advClick")!.Value.ShouldBe("0");
        el.Attribute("advTm")!.Value.ShouldBe("2000");
    }

    [Fact]
    public void Write_AutoAdvanceOnly_ProducesElement()
    {
        var transition = new SlideTransition { AutoAdvanceSeconds = 5.0 };
        TransitionWriter.Write(transition).ShouldNotBeNull();
    }

    [
        Theory,
        MemberData(nameof(AllTransitions))
    ]
    public void RoundTrip_Effect_ThroughWriterAndParser(TransitionEffect effect)
    {
        var original = new SlideTransition { Effect = effect };
        var el = TransitionWriter.Write(original);
        el.ShouldNotBeNull();

        var parsed = new SlideTransition();
        TransitionParser.Parse(el, parsed);
        parsed.Effect.ShouldBe(effect);
    }
}
