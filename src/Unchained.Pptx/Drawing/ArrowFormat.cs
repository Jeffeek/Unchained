using Unchained.Pptx.Models.Drawing;

namespace Unchained.Pptx.Drawing;

/// <summary>Arrowhead settings for the start end of a line.</summary>
public sealed class ArrowFormat
{
    /// <summary>The shape of the arrowhead. Defaults to <see cref="ArrowHeadType.None"/>.</summary>
    public ArrowHeadType HeadType { get; set; } = ArrowHeadType.None;

    /// <summary>The relative size of the arrowhead width.</summary>
    public ArrowHeadSize Width { get; set; } = ArrowHeadSize.Medium;

    /// <summary>The relative size of the arrowhead length.</summary>
    public ArrowHeadSize Length { get; set; } = ArrowHeadSize.Medium;
}
