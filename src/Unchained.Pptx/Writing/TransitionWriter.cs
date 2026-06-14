using System.Xml.Linq;
using Unchained.Pptx.Animations;
using Unchained.Pptx.Core.Xml;

namespace Unchained.Pptx.Writing;

/// <summary>
///     Serializes a <see cref="SlideTransition" /> into a <c>&lt;p:transition&gt;</c> element.
///     Returns <see langword="null" /> when the transition has no effect and no timing overrides
///     (nothing needs to be written).
/// </summary>
internal static class TransitionWriter
{
    /// <summary>
    ///     Writes the transition model to XML. Returns <see langword="null" /> if the transition
    ///     does not need a <c>&lt;p:transition&gt;</c> element (no effect, default click-advance,
    ///     no auto-advance timer).
    /// </summary>
    public static XElement? Write(SlideTransition transition)
    {
        // No element needed if everything is default / no effect configured
        if (transition is { Effect: TransitionEffect.None, AdvanceOnClick: true, AutoAdvanceSeconds: null })
            return null;

        var pml = PmlNames.Pml;
        var el = new XElement(PmlNames.Transition);

        // Duration in milliseconds
        var durMs = (int)(transition.DurationSeconds * 1000);
        if (durMs > 0)
            el.Add(new XAttribute("dur", durMs));

        // Advance conditions
        el.Add(new XAttribute("advClick", transition.AdvanceOnClick ? "1" : "0"));

        if (transition.AutoAdvanceSeconds.HasValue)
        {
            var advTmMs = (int)(transition.AutoAdvanceSeconds.Value * 1000);
            el.Add(new XAttribute("advTm", advTmMs));
        }

        // Effect child element
        var effectEl = WriteEffectElement(transition.Effect, pml);
        if (effectEl != null)
            el.Add(effectEl);

        return el;
    }

    private static XElement? WriteEffectElement(TransitionEffect effect, XNamespace pml) =>
        effect switch
        {
            TransitionEffect.None => null,
            TransitionEffect.Cut => new XElement(pml + "cut"),
            TransitionEffect.Fade => new XElement(pml + "fade"),
            TransitionEffect.Circle => new XElement(pml + "circle"),
            TransitionEffect.Wedge => new XElement(pml + "wedge"),
            TransitionEffect.Wheel => new XElement(pml + "wheel"),
            TransitionEffect.Random => new XElement(pml + "random"),
            TransitionEffect.Newsflash => new XElement(pml + "newsflash"),
            TransitionEffect.Morph => new XElement(pml + "morph"),

            TransitionEffect.PushLeft => new XElement(pml + "push", new XAttribute("dir", "l")),
            TransitionEffect.PushRight => new XElement(pml + "push", new XAttribute("dir", "r")),
            TransitionEffect.PushUp => new XElement(pml + "push", new XAttribute("dir", "u")),
            TransitionEffect.PushDown => new XElement(pml + "push", new XAttribute("dir", "d")),

            TransitionEffect.WipeLeft => new XElement(pml + "wipe", new XAttribute("dir", "l")),
            TransitionEffect.WipeRight => new XElement(pml + "wipe", new XAttribute("dir", "r")),
            TransitionEffect.WipeUp => new XElement(pml + "wipe", new XAttribute("dir", "u")),
            TransitionEffect.WipeDown => new XElement(pml + "wipe", new XAttribute("dir", "d")),

            TransitionEffect.CoverLeft => new XElement(pml + "cover", new XAttribute("dir", "l")),
            TransitionEffect.CoverRight => new XElement(pml + "cover", new XAttribute("dir", "r")),
            TransitionEffect.CoverUp => new XElement(pml + "cover", new XAttribute("dir", "u")),
            TransitionEffect.CoverDown => new XElement(pml + "cover", new XAttribute("dir", "d")),

            TransitionEffect.UncoverLeft => new XElement(pml + "uncover", new XAttribute("dir", "l")),
            TransitionEffect.UncoverRight => new XElement(pml + "uncover", new XAttribute("dir", "r")),
            TransitionEffect.UncoverUp => new XElement(pml + "uncover", new XAttribute("dir", "u")),
            TransitionEffect.UncoverDown => new XElement(pml + "uncover", new XAttribute("dir", "d")),

            TransitionEffect.ZoomIn => new XElement(pml + "zoom", new XAttribute("dir", "in")),
            TransitionEffect.ZoomOut => new XElement(pml + "zoom", new XAttribute("dir", "out")),

            TransitionEffect.BlindsHorizontal => new XElement(pml + "blinds", new XAttribute("dir", "horz")),
            TransitionEffect.BlindsVertical => new XElement(pml + "blinds", new XAttribute("dir", "vert")),

            TransitionEffect.CheckerHorizontal => new XElement(pml + "checker", new XAttribute("dir", "horz")),
            TransitionEffect.CheckerVertical => new XElement(pml + "checker", new XAttribute("dir", "vert")),

            TransitionEffect.CombHorizontal => new XElement(pml + "comb", new XAttribute("dir", "horz")),
            TransitionEffect.CombVertical => new XElement(pml + "comb", new XAttribute("dir", "vert")),

            _ => null
        };
}
