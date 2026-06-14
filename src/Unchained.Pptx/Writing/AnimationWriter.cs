using System.Globalization;
using System.Xml.Linq;
using Unchained.Ooxml;
using Unchained.Pptx.Animations;
using Unchained.Pptx.Core.Xml;

namespace Unchained.Pptx.Writing;

/// <summary>
///     Serializes an <see cref="AnimationTimeline" /> into a <c>&lt;p:timing&gt;</c> element.
///     Uses the standard OOXML click-group nesting required by PowerPoint.
/// </summary>
internal static class AnimationWriter
{
    private static readonly XNamespace Pml = PmlNames.Pml;

    /// <summary>
    ///     Writes the animation timeline to XML. Returns <see langword="null" /> when there are no
    ///     animations and no element needs to be written.
    /// </summary>
    public static XElement? Write(AnimationTimeline timeline)
    {
        if (!timeline.HasAnimations) return null;

        var timing = new XElement(PmlNames.Timing);
        timing.Add(WriteTnLst(timeline));
        timing.Add(WriteBldLst(timeline));
        return timing;
    }

    // ── tnLst (time node list) ────────────────────────────────────────────────

    private static XElement WriteTnLst(AnimationTimeline timeline)
    {
        var nextId = new IdCounter();

        var rootCtn = new XElement(
            Pml + "cTn",
            new XAttribute("id", nextId.Next()),
            new XAttribute("dur", "indefinite"),
            new XAttribute("restart", "whenNotActive"),
            new XAttribute("nodeType", "tmRoot")
        );

        var rootChildren = new XElement(Pml + "childTnLst");
        rootCtn.Add(rootChildren);

        // Main sequence
        rootChildren.Add(WriteMainSeq(timeline.MainSequence, nextId));

        // Interactive sequences
        foreach (var interactive in timeline.InteractiveSequences)
            rootChildren.Add(WriteInteractiveSeq(interactive, nextId));

        var rootPar = new XElement(Pml + "par", rootCtn);
        return new XElement(Pml + "tnLst", rootPar);
    }

    // ── Main sequence ─────────────────────────────────────────────────────────

    private static XElement WriteMainSeq(AnimationSequence sequence, IdCounter ids)
    {
        var seqCtn = new XElement(
            Pml + "cTn",
            new XAttribute("id", ids.Next()),
            new XAttribute("dur", "indefinite"),
            new XAttribute("nodeType", "mainSeq")
        );
        var seqChildren = new XElement(Pml + "childTnLst");
        seqCtn.Add(seqChildren);

        WriteClickGroups(sequence.Effects, seqChildren, ids);

        return new XElement(
            Pml + "seq",
            new XAttribute("concurrent", "1"),
            new XAttribute("nextAc", "seek"),
            seqCtn
        );
    }

    private static void WriteClickGroups(
        IEnumerable<AnimationEffect> effects,
        XContainer parent,
        IdCounter ids
    )
    {
        // Group effects by click group: OnClick starts a new group
        var groups = new List<List<AnimationEffect>>();
        List<AnimationEffect>? current = null;

        foreach (var effect in effects)
        {
            if (effect.Trigger == EffectTrigger.OnClick || current == null)
            {
                current = [];
                groups.Add(current);
            }

            current.Add(effect);
        }

        for (var g = 0; g < groups.Count; g++)
        {
            var group = groups[g];
            var groupPar = WriteClickGroup(group, g, ids);
            parent.Add(groupPar);
        }
    }

    private static XElement WriteClickGroup(
        IReadOnlyList<AnimationEffect> effects,
        int groupIndex,
        IdCounter ids
    )
    {
        var outerCtn = new XElement(
            Pml + "cTn",
            new XAttribute("id", ids.Next()),
            new XAttribute("fill", "hold")
        );

        // OnClick group has delay="indefinite"
        outerCtn.Add(
            new XElement(
                Pml + "stCondLst",
                new XElement(
                    Pml + "cond",
                    new XAttribute("delay", "indefinite")
                )
            )
        );

        var outerChildren = new XElement(Pml + "childTnLst");
        outerCtn.Add(outerChildren);

        for (var i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            var nodeType = i == 0
                ? "clickEffect"
                : effect.Trigger == EffectTrigger.AfterPrevious
                    ? "afterEffect"
                    : "withEffect";

            var effectDelay = i == 0 ? 0 : (int)(effect.Timing.DelaySeconds * 1000);
            var effectPar = WriteEffect(effect, nodeType, effectDelay, groupIndex, ids);
            outerChildren.Add(effectPar);
        }

        return new XElement(Pml + "par", outerCtn);
    }

    private static XElement WriteEffect(
        AnimationEffect effect,
        string nodeType,
        int delayMs,
        int grpId,
        IdCounter ids
    )
    {
        var durMs = Math.Max(1, (int)(effect.Timing.DurationSeconds * 1000));
        var presetClass = effect.Category switch
        {
            EffectCategory.Exit => "exit",
            EffectCategory.Emphasis => "emph",
            EffectCategory.Motion => "path",
            _ => "entr"
        };

        var ctn = new XElement(
            Pml + "cTn",
            new XAttribute("id", ids.Next()),
            new XAttribute("presetID", (int)effect.Preset),
            new XAttribute("presetClass", presetClass),
            new XAttribute("presetSubtype", "0"),
            new XAttribute("fill", "hold"),
            new XAttribute("grpId", grpId),
            new XAttribute("nodeType", nodeType),
            new XAttribute("dur", durMs)
        );

        // Acceleration / deceleration (ease-in / ease-out) as 1000ths of a percent.
        if (effect.Timing.AccelerationPercent > 0)
            ctn.SetAttributeValue("accel", (int)Math.Round(effect.Timing.AccelerationPercent * OoxmlScaling.PercentScale));
        if (effect.Timing.DecelerationPercent > 0)
            ctn.SetAttributeValue("decel", (int)Math.Round(effect.Timing.DecelerationPercent * OoxmlScaling.PercentScale));
        if (effect.Timing.AutoReverse)
            ctn.SetAttributeValue("autoRev", "1");
        if (effect.Timing.RepeatCount != 0)
        {
            ctn.SetAttributeValue(
                "repeatCount",
                effect.Timing.RepeatCount < 0
                    ? "indefinite"
                    : (effect.Timing.RepeatCount * 1000).ToString(CultureInfo.InvariantCulture)
            );
        }

        ctn.Add(
            new XElement(
                Pml + "stCondLst",
                new XElement(
                    Pml + "cond",
                    new XAttribute("delay", delayMs)
                )
            )
        );

        var children = new XElement(Pml + "childTnLst");
        ctn.Add(children);

        switch (effect.Category)
        {
            // Visibility set (entrance → visible; exit → hidden)
            case EffectCategory.Entrance:
                children.Add(WriteVisibilitySet(effect.TargetShapeId, "visible", ids));
            break;
            case EffectCategory.Exit:
                children.Add(WriteVisibilitySet(effect.TargetShapeId, "hidden", ids));
            break;
            case EffectCategory.Emphasis:
            case EffectCategory.Motion:
            break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        // Animation filter (for effects that have a visual filter)
        var filter = GetAnimFilter(effect.Preset);
        if (!string.IsNullOrEmpty(filter))
        {
            children.Add(
                WriteAnimEffect(
                    effect.TargetShapeId,
                    effect.Category == EffectCategory.Exit ? "out" : "in",
                    filter,
                    durMs,
                    ids
                )
            );
        }

        return new XElement(Pml + "par", ctn);
    }

    private static XElement WriteVisibilitySet(uint spid, string visValue, IdCounter ids)
    {
        var setBhvr = new XElement(
            Pml + "cBhvr",
            new XElement(
                Pml + "cTn",
                new XAttribute("id", ids.Next()),
                new XAttribute("dur", "1"),
                new XAttribute("fill", "hold")
            ),
            new XElement(
                Pml + "tgtEl",
                new XElement(Pml + "spTgt", new XAttribute("spid", spid))
            ),
            new XElement(
                Pml + "attrNameLst",
                new XElement(Pml + "attrName", "style.visibility")
            )
        );

        return new XElement(
            Pml + "set",
            setBhvr,
            new XElement(
                Pml + "to",
                new XElement(Pml + "strVal", new XAttribute("val", visValue))
            )
        );
    }

    private static XElement WriteAnimEffect(
        uint spid,
        string transition,
        string filter,
        int durMs,
        IdCounter ids
    )
    {
        var bhvr = new XElement(
            Pml + "cBhvr",
            new XElement(
                Pml + "cTn",
                new XAttribute("id", ids.Next()),
                new XAttribute("dur", durMs)
            ),
            new XElement(
                Pml + "tgtEl",
                new XElement(Pml + "spTgt", new XAttribute("spid", spid))
            )
        );

        return new XElement(
            Pml + "animEffect",
            new XAttribute("transition", transition),
            new XAttribute("filter", filter),
            bhvr
        );
    }

    private static string GetAnimFilter(AnimationPreset preset) =>
        // Entrance/Exit presets that use animEffect with a filter name
        preset switch
        {
            AnimationPreset.Fade or AnimationPreset.Dissolve => "fade",
            AnimationPreset.Wipe => "wipe(right)",
            AnimationPreset.Wedge => "wedge",
            AnimationPreset.Wheel => "wheel(1)",
            AnimationPreset.RandomBars => "randombar(horizontal)",
            AnimationPreset.Strips => "strips(rightDown)",
            AnimationPreset.Fly => "fly",
            AnimationPreset.Zoom => "zoom",
            // ReSharper disable PatternIsRedundant
            AnimationPreset.Appear or AnimationPreset.GrowAndTurn or _ => string.Empty
            // ReSharper restore PatternIsRedundant
        };

    // ── Interactive sequences ─────────────────────────────────────────────────

    private static XElement WriteInteractiveSeq(InteractiveSequence interactive, IdCounter ids)
    {
        var seqCtn = new XElement(
            Pml + "cTn",
            new XAttribute("id", ids.Next()),
            new XAttribute("dur", "indefinite"),
            new XAttribute("nodeType", "interactiveSeq")
        );
        var seqChildren = new XElement(Pml + "childTnLst");
        seqCtn.Add(seqChildren);

        WriteClickGroups(interactive.Sequence.Effects, seqChildren, ids);

        // The trigger condition references the shape that was clicked
        var prevCond = new XElement(
            Pml + "prevCondLst",
            new XElement(
                Pml + "cond",
                new XAttribute("evt", "onBegin"),
                new XAttribute("delay", "0"),
                new XElement(
                    Pml + "tgtEl",
                    new XElement(
                        Pml + "spTgt",
                        new XAttribute("spid", interactive.TriggerShapeId)
                    )
                )
            )
        );

        return new XElement(
            Pml + "seq",
            new XAttribute("concurrent", "1"),
            new XAttribute("nextAc", "seek"),
            prevCond,
            seqCtn
        );
    }

    // ── bldLst (build list) ───────────────────────────────────────────────────

    private static XElement WriteBldLst(AnimationTimeline timeline)
    {
        var bldLst = new XElement(Pml + "bldLst");
        var grpId = 0;

        foreach (var effect in timeline.MainSequence.Effects)
        {
            bldLst.Add(
                new XElement(
                    Pml + "bldP",
                    new XAttribute("spid", effect.TargetShapeId),
                    new XAttribute("grpId", grpId),
                    new XAttribute("build", "allAtOnce")
                )
            );

            if (effect.Trigger == EffectTrigger.OnClick)
                grpId++;
        }

        foreach (var effect in timeline.InteractiveSequences.SelectMany(static interactive => interactive.Sequence.Effects))
        {
            bldLst.Add(
                new XElement(
                    Pml + "bldP",
                    new XAttribute("spid", effect.TargetShapeId),
                    new XAttribute("grpId", grpId),
                    new XAttribute("build", "allAtOnce")
                )
            );

            if (effect.Trigger == EffectTrigger.OnClick)
                grpId++;
        }

        return bldLst;
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private sealed class IdCounter
    {
        private int _value;
        public int Next() => ++_value;
    }
}
