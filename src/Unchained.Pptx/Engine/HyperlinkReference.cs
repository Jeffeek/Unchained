using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Engine;

/// <summary>
///     A single hyperlink found in a presentation, paired with the slide and shape that carry it.
///     Returned by the hyperlink-enumeration helpers so callers can inspect, retarget, or remove
///     links across an entire deck.
/// </summary>
public sealed class HyperlinkReference
{
    internal HyperlinkReference(Slide slide, Shape shape, HyperlinkAction action)
    {
        Slide = slide;
        Shape = shape;
        Action = action;
    }

    /// <summary>The slide that contains the shape carrying this hyperlink.</summary>
    public Slide Slide { get; }

    /// <summary>The shape the hyperlink is attached to.</summary>
    public Shape Shape { get; }

    /// <summary>The hyperlink action itself — edit <see cref="HyperlinkAction.Url" /> or retarget in place.</summary>
    public HyperlinkAction Action { get; }

    /// <summary>Removes the hyperlink from its owning shape.</summary>
    public void Remove() => Shape.ClickAction = null;
}
