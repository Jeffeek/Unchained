using System.Xml.Linq;
using Shouldly;
using Unchained.Pptx.Animations;
using Unchained.Pptx.Parsing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Parsing;

/// <summary>
///     Branch coverage for <see cref="AnimationParser" /> driven directly with hand-built
///     <c>&lt;p:timing&gt;</c> trees: missing-element early returns, main vs interactive sequences,
///     click vs non-click groups, every trigger node-type, every preset-class category, and the
///     delay / accel / decel / auto-reverse / repeat-count timing branches.
/// </summary>
public sealed class AnimationParserDirectTests
{
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";

    private static XElement Cond(string delay) =>
        new(P + "stCondLst", new XElement(P + "cond", new XAttribute("delay", delay)));

    private static XElement SpTgt(uint spid) =>
        new(P + "tgtEl", new XElement(P + "spTgt", new XAttribute("spid", spid)));

    private static XElement EffectPar(
        int presetId,
        string presetClass,
        uint spid,
        params XAttribute[] extraAttrs
    )
    {
        var cTn = new XElement(
            P + "cTn",
            new XAttribute("presetID", presetId),
            new XAttribute("presetClass", presetClass)
        );
        foreach (var a in extraAttrs)
            cTn.Add(a);
        cTn.Add(SpTgt(spid));
        return new XElement(P + "par", cTn);
    }

    // Wraps click-group child <p:par> effects inside a group, optionally an OnClick group.
    private static XElement ClickGroup(bool onClick, params XElement[] effectPars)
    {
        var groupCtn = new XElement(P + "cTn");
        if (onClick)
            groupCtn.Add(Cond("indefinite"));
        groupCtn.Add(new XElement(P + "childTnLst", effectPars.Cast<object>().ToArray()));
        return new XElement(P + "par", groupCtn);
    }

    private static XElement MainSeq(params XElement[] clickGroups)
    {
        var seqCtn = new XElement(P + "cTn", new XElement(P + "childTnLst", clickGroups.Cast<object>().ToArray()));
        return new XElement(P + "seq", seqCtn);
    }

    private static XElement Timing(params XElement[] seqElements)
    {
        var rootChildren = new XElement(P + "childTnLst", seqElements.Cast<object>().ToArray());
        var rootCtn = new XElement(P + "cTn", rootChildren);
        var rootPar = new XElement(P + "par", rootCtn);
        var tnLst = new XElement(P + "tnLst", rootPar);
        return new XElement(P + "timing", tnLst);
    }

    private static AnimationTimeline Parse(XElement timing)
    {
        var timeline = new AnimationTimeline();
        AnimationParser.Parse(timing, timeline);
        return timeline;
    }

    // ── Early-return guards ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_NoTnLst_NoEffects()
    {
        var timeline = Parse(new XElement(P + "timing"));
        timeline.HasAnimations.ShouldBeFalse();
    }

    [Fact]
    public void Parse_NoRootChildren_NoEffects()
    {
        var timing = new XElement(P + "timing", new XElement(P + "tnLst", new XElement(P + "par", new XElement(P + "cTn"))));
        Parse(timing).HasAnimations.ShouldBeFalse();
    }

    [Fact]
    public void Parse_NoMainSeq_NoEffects()
    {
        // rootChildren present but no <p:seq> child.
        var rootChildren = new XElement(P + "childTnLst");
        var timing = new XElement(P + "timing", new XElement(P + "tnLst", new XElement(P + "par", new XElement(P + "cTn", rootChildren))));
        Parse(timing).MainSequence.Effects.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_MainSeqWithoutChildTnLst_NoEffects()
    {
        var seq = new XElement(P + "seq", new XElement(P + "cTn"));
        Parse(Timing(seq)).MainSequence.Effects.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_ClickGroupWithoutChildTnLst_Skipped()
    {
        var emptyGroup = new XElement(P + "par", new XElement(P + "cTn"));
        var seq = MainSeq();
        seq.Element(P + "cTn")!.Element(P + "childTnLst")!.Add(emptyGroup);
        Parse(Timing(seq)).MainSequence.Effects.Count.ShouldBe(0);
    }

    // ── Effect parse failures ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_EffectWithoutCTn_Skipped()
    {
        var badEffect = new XElement(P + "par"); // no cTn
        var group = ClickGroup(true, badEffect);
        Parse(Timing(MainSeq(group))).MainSequence.Effects.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_EffectWithNonNumericPresetId_Skipped()
    {
        var effect = new XElement(
            P + "par",
            new XElement(P + "cTn", new XAttribute("presetID", "notanumber"), new XAttribute("presetClass", "entr"), SpTgt(5))
        );
        Parse(Timing(MainSeq(ClickGroup(true, effect)))).MainSequence.Effects.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_EffectWithoutTargetShape_Skipped()
    {
        var effect = new XElement(
            P + "par",
            new XElement(P + "cTn", new XAttribute("presetID", "1"), new XAttribute("presetClass", "entr"))
        );
        Parse(Timing(MainSeq(ClickGroup(true, effect)))).MainSequence.Effects.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_EffectWithZeroSpid_Skipped()
    {
        var effect = EffectPar(1, "entr", 0);
        Parse(Timing(MainSeq(ClickGroup(true, effect)))).MainSequence.Effects.Count.ShouldBe(0);
    }

    // ── Preset-class → category mapping ─────────────────────────────────────────────

    [
        Theory,
        InlineData("entr", EffectCategory.Entrance),
        InlineData("exit", EffectCategory.Exit),
        InlineData("emph", EffectCategory.Emphasis),
        InlineData("path", EffectCategory.Motion),
        InlineData("unknown", EffectCategory.Entrance)
    ]
    public void Parse_PresetClass_MapsCategory(string presetClass, EffectCategory expected)
    {
        var effect = EffectPar(1, presetClass, 7);
        var e = Parse(Timing(MainSeq(ClickGroup(true, effect)))).MainSequence.Effects.ShouldHaveSingleItem();
        e.Category.ShouldBe(expected);
        e.TargetShapeId.ShouldBe(7u);
    }

    // ── Duration / delay branches ───────────────────────────────────────────────────

    [Fact]
    public void Parse_Effect_ValidDuration_Converted()
    {
        var effect = EffectPar(1, "entr", 3, new XAttribute("dur", "2000"));
        Parse(Timing(MainSeq(ClickGroup(true, effect)))).MainSequence.Effects[0].Timing.DurationSeconds.ShouldBe(2.0);
    }

    [Fact]
    public void Parse_Effect_ZeroDuration_KeepsDefault()
    {
        var effect = EffectPar(1, "entr", 3, new XAttribute("dur", "0"));
        Parse(Timing(MainSeq(ClickGroup(true, effect)))).MainSequence.Effects[0].Timing.DurationSeconds.ShouldBe(0.5);
    }

    [Fact]
    public void Parse_Effect_WithStartDelay_Converted()
    {
        var cTn = new XElement(
            P + "cTn",
            new XAttribute("presetID", "1"),
            new XAttribute("presetClass", "entr"),
            Cond("1500"),
            SpTgt(9)
        );
        var effect = new XElement(P + "par", cTn);
        Parse(Timing(MainSeq(ClickGroup(true, effect)))).MainSequence.Effects[0].Timing.DelaySeconds.ShouldBe(1.5);
    }

    [Fact]
    public void Parse_Effect_ZeroDelay_StaysZero()
    {
        var cTn = new XElement(
            P + "cTn",
            new XAttribute("presetID", "1"),
            new XAttribute("presetClass", "entr"),
            Cond("0"),
            SpTgt(9)
        );
        var effect = new XElement(P + "par", cTn);
        Parse(Timing(MainSeq(ClickGroup(true, effect)))).MainSequence.Effects[0].Timing.DelaySeconds.ShouldBe(0.0);
    }

    // ── accel / decel / autoRev / repeat ─────────────────────────────────────────────

    [Fact]
    public void Parse_Effect_AccelDecelAutoRev_Mapped()
    {
        var effect = EffectPar(
            1,
            "entr",
            4,
            new XAttribute("accel", "30000"),
            new XAttribute("decel", "20000"),
            new XAttribute("autoRev", "1")
        );
        var timing = Parse(Timing(MainSeq(ClickGroup(true, effect)))).MainSequence.Effects[0].Timing;
        Math.Round(timing.AccelerationPercent, 2).ShouldBe(0.3);
        Math.Round(timing.DecelerationPercent, 2).ShouldBe(0.2);
        timing.AutoReverse.ShouldBeTrue();
    }

    [Fact]
    public void Parse_Effect_RepeatIndefinite_MinusOne()
    {
        var effect = EffectPar(1, "entr", 4, new XAttribute("repeatCount", "indefinite"));
        Parse(Timing(MainSeq(ClickGroup(true, effect)))).MainSequence.Effects[0].Timing.RepeatCount.ShouldBe(-1);
    }

    [Fact]
    public void Parse_Effect_RepeatNumeric_DividedByThousand()
    {
        var effect = EffectPar(1, "entr", 4, new XAttribute("repeatCount", "3000"));
        Parse(Timing(MainSeq(ClickGroup(true, effect)))).MainSequence.Effects[0].Timing.RepeatCount.ShouldBe(3);
    }

    [Fact]
    public void Parse_Effect_NoRepeat_StaysZero()
    {
        var effect = EffectPar(1, "entr", 4);
        Parse(Timing(MainSeq(ClickGroup(true, effect)))).MainSequence.Effects[0].Timing.RepeatCount.ShouldBe(0);
    }

    // ── Trigger assignment ──────────────────────────────────────────────────────────

    [Fact]
    public void Parse_FirstEffectInOnClickGroup_IsOnClick()
    {
        var effect = EffectPar(1, "entr", 4);
        Parse(Timing(MainSeq(ClickGroup(true, effect)))).MainSequence.Effects[0].Trigger.ShouldBe(EffectTrigger.OnClick);
    }

    [
        Theory,
        InlineData("afterEffect", EffectTrigger.AfterPrevious),
        InlineData("afterEffectSeq", EffectTrigger.AfterPrevious),
        InlineData("clickEffect", EffectTrigger.OnClick),
        InlineData("withEffect", EffectTrigger.WithPrevious)
    ]
    public void Parse_SecondEffect_TriggerFromNodeType(string nodeType, EffectTrigger expected)
    {
        var first = EffectPar(1, "entr", 4);
        var second = EffectPar(2, "entr", 5, new XAttribute("nodeType", nodeType));
        var effects = Parse(Timing(MainSeq(ClickGroup(true, first, second)))).MainSequence.Effects;
        effects.Count.ShouldBe(2);
        effects[1].Trigger.ShouldBe(expected);
    }

    [Fact]
    public void Parse_NonClickGroup_FirstEffectUsesNodeTypeTrigger()
    {
        // Non-OnClick group: even the first effect's trigger derives from its node type.
        var effect = EffectPar(1, "entr", 4, new XAttribute("nodeType", "afterEffect"));
        Parse(Timing(MainSeq(ClickGroup(false, effect)))).MainSequence.Effects[0].Trigger.ShouldBe(EffectTrigger.AfterPrevious);
    }

    // ── Interactive sequences ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_InteractiveSequence_WithTrigger_Added()
    {
        var mainSeq = MainSeq(ClickGroup(true, EffectPar(1, "entr", 4)));

        var interCtn = new XElement(P + "cTn", new XElement(P + "childTnLst", ClickGroup(true, EffectPar(2, "exit", 8))));
        var prevCond = new XElement(P + "prevCondLst", new XElement(P + "cond", SpTgt(99)));
        var interSeq = new XElement(P + "seq", prevCond, interCtn);

        var timeline = Parse(Timing(mainSeq, interSeq));
        timeline.InteractiveSequences.Count.ShouldBe(1);
        timeline.InteractiveSequences[0].TriggerShapeId.ShouldBe(99u);
        timeline.InteractiveSequences[0].Sequence.Effects.ShouldNotBeEmpty();
    }

    [Fact]
    public void Parse_InteractiveSequence_NoTrigger_Skipped()
    {
        var mainSeq = MainSeq(ClickGroup(true, EffectPar(1, "entr", 4)));
        // Interactive seq with no prevCondLst/spTgt → trigger id 0 → skipped.
        var interSeq = new XElement(P + "seq", new XElement(P + "cTn"));
        Parse(Timing(mainSeq, interSeq)).InteractiveSequences.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_InteractiveSequence_ZeroSpidTrigger_Skipped()
    {
        var mainSeq = MainSeq(ClickGroup(true, EffectPar(1, "entr", 4)));
        var prevCond = new XElement(P + "prevCondLst", new XElement(P + "cond", SpTgt(0)));
        var interSeq = new XElement(P + "seq", prevCond, new XElement(P + "cTn"));
        Parse(Timing(mainSeq, interSeq)).InteractiveSequences.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_InteractiveSequence_TriggerButNoChildTnLst_AddedWithNoEffects()
    {
        var mainSeq = MainSeq(ClickGroup(true, EffectPar(1, "entr", 4)));
        var prevCond = new XElement(P + "prevCondLst", new XElement(P + "cond", SpTgt(50)));
        var interSeq = new XElement(P + "seq", prevCond, new XElement(P + "cTn"));
        var timeline = Parse(Timing(mainSeq, interSeq));
        timeline.InteractiveSequences.Count.ShouldBe(1);
        timeline.InteractiveSequences[0].Sequence.Effects.Count.ShouldBe(0);
    }
}
