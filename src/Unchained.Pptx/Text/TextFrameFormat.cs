using Unchained.Pptx.Core;
using Unchained.Pptx.Models.Text;

namespace Unchained.Pptx.Text;

/// <summary>
/// Controls how text is laid out within a <see cref="TextFrame"/>.
/// </summary>
public sealed class TextFrameFormat
{
    /// <summary>
    /// Vertical anchor position of the text within the frame.
    /// </summary>
    public TextAnchor VerticalAnchor { get; set; } = TextAnchor.Top;

    /// <summary>
    /// Writing direction of the text.
    /// </summary>
    public TextDirection Direction { get; set; } = TextDirection.Horizontal;

    /// <summary>
    /// How the frame or text resizes when the content does not fit.
    /// </summary>
    public TextAutofit Autofit { get; set; } = TextAutofit.None;

    /// <summary>Left internal margin in EMU.</summary>
    public Emu MarginLeft { get; set; } = Emu.FromPoints(7.2);

    /// <summary>Right internal margin in EMU.</summary>
    public Emu MarginRight { get; set; } = Emu.FromPoints(7.2);

    /// <summary>Top internal margin in EMU.</summary>
    public Emu MarginTop { get; set; } = Emu.FromPoints(3.6);

    /// <summary>Bottom internal margin in EMU.</summary>
    public Emu MarginBottom { get; set; } = Emu.FromPoints(3.6);

    /// <summary>
    /// <see langword="true"/> when text wraps at the frame boundary (the default);
    /// <see langword="false"/> when text overflows horizontally without wrapping.
    /// </summary>
    public bool WrapText { get; set; } = true;

    /// <summary>
    /// Number of text columns inside the frame. Defaults to 1.
    /// </summary>
    public int ColumnCount { get; set; } = 1;

    /// <summary>
    /// Spacing between columns in EMU. Only meaningful when <see cref="ColumnCount"/> &gt; 1.
    /// </summary>
    public Emu ColumnSpacing { get; set; } = Emu.FromPoints(36);
}
