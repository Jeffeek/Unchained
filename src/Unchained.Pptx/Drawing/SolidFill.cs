using Unchained.Pptx.Themes;

namespace Unchained.Pptx.Drawing;

/// <summary>A fill that paints the entire area with a single, uniform colour.</summary>
public sealed class SolidFill
{
    /// <summary>The colour used for this fill.</summary>
    public ColorSpec Color { get; set; }
}
