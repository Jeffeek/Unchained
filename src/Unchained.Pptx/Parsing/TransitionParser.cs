using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Animations;

namespace Unchained.Pptx.Parsing;

/// <summary>
///     Parses a <c>&lt;p:transition&gt;</c> element into a <see cref="SlideTransition" />.
/// </summary>
internal static class TransitionParser
{
    /// <summary>
    ///     Populates <paramref name="transition" /> from the <c>&lt;p:transition&gt;</c> element.
    ///     Unknown transition types are silently ignored (the object stays at its defaults).
    /// </summary>
    public static void Parse(XElement transitionEl, SlideTransition transition)
    {
        // Duration / speed
        var durRaw = transitionEl.GetAttr("dur");
        if (durRaw != null && int.TryParse(durRaw, out var durMs))
            transition.DurationSeconds = durMs / 1000.0;
        else
        {
            // Legacy spd attribute: "slow"=3000, "med"=750, "fast"=500 ms
            var spd = transitionEl.GetAttr("spd", "med");
            transition.DurationSeconds = spd switch
            {
                "slow" => 3.0,
                "fast" => 0.5,
                _ => 0.75
            };
        }

        // Advance conditions
        var advClick = transitionEl.GetAttrBool("advClick");
        transition.AdvanceOnClick = advClick ?? true;

        var advTmRaw = transitionEl.GetAttr("advTm");
        if (advTmRaw != null && int.TryParse(advTmRaw, out var advTmMs))
            transition.AutoAdvanceSeconds = advTmMs / 1000.0;

        // Effect — determined by the first child element
        var child = transitionEl.Elements().FirstOrDefault();
        if (child != null)
            transition.Effect = MapElement(child);
    }

    private static TransitionEffect MapElement(XElement el)
    {
        var local = el.Name.LocalName;
        var dir = (string?)el.Attribute("dir") ?? string.Empty;

        return local switch
        {
            "cut" => TransitionEffect.Cut,
            "fade" => TransitionEffect.Fade,
            "circle" => TransitionEffect.Circle,
            "wedge" => TransitionEffect.Wedge,
            "wheel" => TransitionEffect.Wheel,
            "random" => TransitionEffect.Random,
            "newsflash" => TransitionEffect.Newsflash,
            "morph" => TransitionEffect.Morph,

            "push" => dir switch
            {
                "r" => TransitionEffect.PushRight,
                "u" => TransitionEffect.PushUp,
                "d" => TransitionEffect.PushDown,
                _ => TransitionEffect.PushLeft
            },

            "wipe" => dir switch
            {
                "r" => TransitionEffect.WipeRight,
                "u" => TransitionEffect.WipeUp,
                "d" => TransitionEffect.WipeDown,
                _ => TransitionEffect.WipeLeft
            },

            "cover" => dir switch
            {
                "r" => TransitionEffect.CoverRight,
                "u" => TransitionEffect.CoverUp,
                "d" => TransitionEffect.CoverDown,
                _ => TransitionEffect.CoverLeft
            },

            "uncover" => dir switch
            {
                "r" => TransitionEffect.UncoverRight,
                "u" => TransitionEffect.UncoverUp,
                "d" => TransitionEffect.UncoverDown,
                _ => TransitionEffect.UncoverLeft
            },

            "zoom" => dir == "out" ? TransitionEffect.ZoomOut : TransitionEffect.ZoomIn,

            "blinds" => dir == "vert"
                ? TransitionEffect.BlindsVertical
                : TransitionEffect.BlindsHorizontal,

            "checker" => dir == "vert"
                ? TransitionEffect.CheckerVertical
                : TransitionEffect.CheckerHorizontal,

            "comb" => dir == "vert"
                ? TransitionEffect.CombVertical
                : TransitionEffect.CombHorizontal,

            _ => TransitionEffect.Fade // safe fallback for unrecognized types
        };
    }
}
