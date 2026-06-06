namespace Unchained.Ooxml.Drawing;

/// <summary>Arrowhead settings for the end (tail) of a line.</summary>
public sealed class TailArrow
{
    /// <summary>The shape of the arrowhead. Defaults to <see cref="ArrowHeadType.None"/>.</summary>
    public ArrowHeadType HeadType { get; set; } = ArrowHeadType.None;

    /// <summary>The relative size of the arrowhead width.</summary>
    public ArrowHeadSize Width { get; set; } = ArrowHeadSize.Medium;

    /// <summary>The relative size of the arrowhead length.</summary>
    public ArrowHeadSize Length { get; set; } = ArrowHeadSize.Medium;
}
