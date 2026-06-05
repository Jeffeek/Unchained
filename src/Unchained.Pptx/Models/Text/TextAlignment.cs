namespace Unchained.Pptx.Models.Text;

/// <summary>Horizontal alignment of text within a paragraph.</summary>
public enum TextAlignment
{
    /// <summary>Text is aligned to the left edge.</summary>
    Left,
    /// <summary>Text is centred between the left and right edges.</summary>
    Center,
    /// <summary>Text is aligned to the right edge.</summary>
    Right,
    /// <summary>Text is fully justified (aligned to both left and right edges).</summary>
    Justify,
    /// <summary>Justified alignment with the last line aligned to the left (low-mode).</summary>
    JustifyLow,
    /// <summary>Text is distributed evenly across the line width.</summary>
    Distributed,
    /// <summary>Thai-language distributed alignment.</summary>
    ThaiDistributed
}
