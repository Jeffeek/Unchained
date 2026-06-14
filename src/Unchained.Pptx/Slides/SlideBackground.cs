using Unchained.Ooxml.Drawing;

namespace Unchained.Pptx.Slides;

/// <summary>
///     The background of a slide, master, or layout — defines whether the background
///     is filled with a colour, gradient, or picture.
/// </summary>
public sealed class SlideBackground
{
    /// <summary>
    ///     The fill applied to the slide background.
    ///     By default this is <see cref="FillType.None" />, meaning the background
    ///     is inherited from the slide master.
    /// </summary>
    public FillFormat Fill { get; } = new();
}
