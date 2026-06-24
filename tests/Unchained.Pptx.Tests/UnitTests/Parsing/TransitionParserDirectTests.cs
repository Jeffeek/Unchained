using System.Xml.Linq;
using Shouldly;
using Unchained.Pptx.Animations;
using Unchained.Pptx.Parsing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Parsing;

/// <summary>
///     Branch coverage for <see cref="TransitionParser" />: explicit <c>dur</c> vs the legacy
///     <c>spd</c> fallback (slow/med/fast), advance-on-click and timed auto-advance, and the
///     element→<see cref="TransitionEffect" /> map including directional variants and the
///     unrecognised-element fallback.
/// </summary>
public sealed class TransitionParserDirectTests
{
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";

    private static SlideTransition Parse(XElement transition)
    {
        var result = new SlideTransition();
        TransitionParser.Parse(transition, result);
        return result;
    }

    private static XElement Transition(XElement? effect = null, params XAttribute[] attrs)
    {
        var el = new XElement(P + "transition", attrs.Cast<object>().ToArray());
        if (effect is not null) el.Add(effect);
        return el;
    }

    [Fact]
    public void Parse_ExplicitDuration_ConvertsMillisecondsToSeconds()
    {
        var t = Parse(Transition(attrs: new XAttribute("dur", "2000")));
        t.DurationSeconds.ShouldBe(2.0, 0.001);
    }

    [
        Theory,
        InlineData("slow", 3.0),
        InlineData("fast", 0.5),
        InlineData("med", 0.75),
        InlineData("unknown", 0.75)
    ]
    public void Parse_LegacySpeed_MapsToDuration(string spd, double expected)
    {
        var t = Parse(Transition(attrs: new XAttribute("spd", spd)));
        t.DurationSeconds.ShouldBe(expected, 0.001);
    }

    [Fact]
    public void Parse_NoSpeedAttribute_DefaultsToMedium()
    {
        var t = Parse(Transition());
        t.DurationSeconds.ShouldBe(0.75, 0.001);
    }

    [Fact]
    public void Parse_AdvanceOnClickFalse_AndAutoAdvance()
    {
        var t = Parse(Transition(attrs: [new XAttribute("advClick", "0"), new XAttribute("advTm", "5000")]));
        t.AdvanceOnClick.ShouldBeFalse();
        t.AutoAdvanceSeconds.ShouldNotBeNull();
        t.AutoAdvanceSeconds!.Value.ShouldBe(5.0, 0.001);
    }

    [Fact]
    public void Parse_NoAdvClick_DefaultsToTrue()
    {
        var t = Parse(Transition());
        t.AdvanceOnClick.ShouldBeTrue();
    }

    [
        Theory,
        InlineData("cut", null, TransitionEffect.Cut),
        InlineData("fade", null, TransitionEffect.Fade),
        InlineData("circle", null, TransitionEffect.Circle),
        InlineData("wedge", null, TransitionEffect.Wedge),
        InlineData("wheel", null, TransitionEffect.Wheel),
        InlineData("random", null, TransitionEffect.Random),
        InlineData("newsflash", null, TransitionEffect.Newsflash),
        InlineData("morph", null, TransitionEffect.Morph),
        InlineData("push", "r", TransitionEffect.PushRight),
        InlineData("push", "u", TransitionEffect.PushUp),
        InlineData("push", "d", TransitionEffect.PushDown),
        InlineData("push", "l", TransitionEffect.PushLeft),
        InlineData("wipe", "r", TransitionEffect.WipeRight),
        InlineData("wipe", "u", TransitionEffect.WipeUp),
        InlineData("wipe", "d", TransitionEffect.WipeDown),
        InlineData("wipe", null, TransitionEffect.WipeLeft),
        InlineData("cover", "r", TransitionEffect.CoverRight),
        InlineData("cover", "u", TransitionEffect.CoverUp),
        InlineData("cover", "d", TransitionEffect.CoverDown),
        InlineData("cover", null, TransitionEffect.CoverLeft),
        InlineData("uncover", "r", TransitionEffect.UncoverRight),
        InlineData("uncover", "u", TransitionEffect.UncoverUp),
        InlineData("uncover", "d", TransitionEffect.UncoverDown),
        InlineData("uncover", null, TransitionEffect.UncoverLeft),
        InlineData("zoom", "out", TransitionEffect.ZoomOut),
        InlineData("zoom", "in", TransitionEffect.ZoomIn),
        InlineData("blinds", "vert", TransitionEffect.BlindsVertical),
        InlineData("blinds", "horz", TransitionEffect.BlindsHorizontal),
        InlineData("checker", "vert", TransitionEffect.CheckerVertical),
        InlineData("checker", "horz", TransitionEffect.CheckerHorizontal),
        InlineData("comb", "vert", TransitionEffect.CombVertical),
        InlineData("comb", "horz", TransitionEffect.CombHorizontal),
        InlineData("totallyUnknown", null, TransitionEffect.Fade)
    ]
    public void Parse_EffectElement_MapsToEffect(string local, string? dir, TransitionEffect expected)
    {
        var effect = new XElement(P + local);
        if (dir is not null) effect.Add(new XAttribute("dir", dir));
        var t = Parse(Transition(effect));
        t.Effect.ShouldBe(expected);
    }
}
