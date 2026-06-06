using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Animations;
using Unchained.Pptx.Core.Xml;

namespace Unchained.Pptx.Parsing;

/// <summary>
/// Parses a <c>&lt;p:timing&gt;</c> element into an <see cref="AnimationTimeline"/>.
/// Extracts preset-based effects from the main sequence and interactive sequences;
/// complex custom animations that cannot be represented in the model are silently ignored
/// (the raw element is preserved for round-trip by the caller).
/// </summary>
internal static class AnimationParser
{
    /// <summary>
    /// Populates <paramref name="timeline"/> from the <c>&lt;p:timing&gt;</c> element.
    /// </summary>
    public static void Parse(XElement timingEl, AnimationTimeline timeline)
    {
        var pml = PmlNames.Pml;

        // Navigate to the main sequence
        var tnLst = timingEl.Element(pml + "tnLst");
        var rootPar = tnLst?.Element(pml + "par");
        var rootCtn = rootPar?.Element(pml + "cTn");
        var rootChildren = rootCtn?.Element(pml + "childTnLst");
        if (rootChildren == null) return;

        // Main sequence: the first <p:seq> inside the root
        var mainSeq = rootChildren.Element(pml + "seq");
        if (mainSeq != null)
            ParseMainSequence(mainSeq, timeline.MainSequence, pml);

        // Interactive sequences: subsequent <p:seq> elements
        foreach (var interSeq in rootChildren.Elements(pml + "seq").Skip(1))
        {
            var triggerShapeId = GetInteractiveTriggerShapeId(interSeq, pml);
            if (triggerShapeId == 0) continue;

            var interactive = timeline.AddInteractiveSequence(triggerShapeId);
            var seqCtn = interSeq.Element(pml + "cTn");
            var seqChildren = seqCtn?.Element(pml + "childTnLst");
            if (seqChildren != null)
                ParseClickGroups(seqChildren, interactive.Sequence, pml);
        }
    }

    // ── Main sequence ─────────────────────────────────────────────────────────

    private static void ParseMainSequence(XElement seqEl, AnimationSequence sequence, XNamespace pml)
    {
        var ctn = seqEl.Element(pml + "cTn");
        var children = ctn?.Element(pml + "childTnLst");
        if (children == null) return;

        ParseClickGroups(children, sequence, pml);
    }

    private static void ParseClickGroups(XElement childrenEl, AnimationSequence sequence, XNamespace pml)
    {
        // Each <p:par> inside children is a click group
        foreach (var clickGroup in childrenEl.Elements(pml + "par"))
        {
            var groupCtn = clickGroup.Element(pml + "cTn");
            var groupChildren = groupCtn?.Element(pml + "childTnLst");
            if (groupChildren == null) continue;

            // Determine if this is an OnClick group (has delay="indefinite" condition)
            var isOnClick = IsClickGroup(groupCtn!, pml);

            // Each inner <p:par> is one effect
            var isFirst = true;
            foreach (var effectPar in groupChildren.Elements(pml + "par"))
            {
                var effect = ParseEffect(effectPar, pml);
                if (effect == null) continue;

                // Assign trigger based on group type and position
                if (isFirst && isOnClick)
                    effect.Trigger = EffectTrigger.OnClick;
                else
                    effect.Trigger = GetTrigger(effectPar.Element(pml + "cTn"), pml);

                sequence.AddEffect(
                    effect.TargetShapeId,
                    effect.Preset,
                    effect.Category,
                    effect.Trigger,
                    effect.Timing.DelaySeconds);
                isFirst = false;
            }
        }
    }

    private static AnimationEffect? ParseEffect(XElement effectPar, XNamespace pml)
    {
        var ctn = effectPar.Element(pml + "cTn");
        if (ctn == null) return null;

        // Read preset metadata
        var presetIdRaw = ctn.GetAttr("presetID");
        if (!int.TryParse(presetIdRaw, out var presetId)) return null;

        var presetClass = ctn.GetAttr("presetClass", "entr");
        var durRaw = ctn.GetAttr("dur");
        double durationSeconds = 0.5;
        if (durRaw != null && int.TryParse(durRaw, out var durMs) && durMs > 0)
            durationSeconds = durMs / 1000.0;

        // Find the target shape ID from nested elements
        var targetSpid = FindTargetShapeId(ctn, pml);
        if (targetSpid == 0) return null;

        // Delay from the condition
        double delay = 0;
        var condDelay = ctn.Element(pml + "stCondLst")?.Element(pml + "cond")
                           ?.GetAttr("delay");
        if (condDelay != null && int.TryParse(condDelay, out var delayMs) && delayMs > 0)
            delay = delayMs / 1000.0;

        var category = presetClass switch
        {
            "exit" => EffectCategory.Exit,
            "emph" => EffectCategory.Emphasis,
            "path" => EffectCategory.Motion,
            _ => EffectCategory.Entrance,
        };

        var preset = (AnimationPreset)presetId;

        var effect = new AnimationEffect
        {
            TargetShapeId = targetSpid,
            Preset = preset,
            Category = category,
            Trigger = EffectTrigger.OnClick,
        };
        effect.Timing.DurationSeconds = durationSeconds;
        effect.Timing.DelaySeconds = delay;

        return effect;
    }

    private static uint FindTargetShapeId(XElement ctn, XNamespace pml)
    {
        // Target shape is in a nested <p:spTgt spid="N"/> element anywhere in descendants
        var spTgt = ctn.Descendants(pml + "spTgt").FirstOrDefault();
        if (spTgt == null) return 0;

        var spid = spTgt.GetAttr("spid");
        return spid != null && uint.TryParse(spid, out var id) ? id : 0;
    }

    private static bool IsClickGroup(XElement groupCtn, XNamespace pml)
    {
        var cond = groupCtn.Element(pml + "stCondLst")?.Element(pml + "cond");
        return cond?.GetAttr("delay") == "indefinite";
    }

    private static EffectTrigger GetTrigger(XElement? ctn, XNamespace pml)
    {
        if (ctn == null) return EffectTrigger.WithPrevious;
        var nodeType = ctn.GetAttr("nodeType", string.Empty);
        return nodeType switch
        {
            "afterEffect" or "afterEffectSeq" => EffectTrigger.AfterPrevious,
            "clickEffect" => EffectTrigger.OnClick,
            _ => EffectTrigger.WithPrevious
        };
    }

    private static uint GetInteractiveTriggerShapeId(XElement seqEl, XNamespace pml)
    {
        // The trigger shape ID is in the seq's prevCondLst or in a nested cond/@spid
        var prevCond = seqEl.Element(pml + "prevCondLst")?.Element(pml + "cond");
        if (prevCond == null) return 0;
        var spid = prevCond.GetAttr("spid");
        return spid != null && uint.TryParse(spid, out var id) ? id : 0;
    }
}
