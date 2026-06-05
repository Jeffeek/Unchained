namespace Unchained.Pptx.Models.Text;

/// <summary>The writing direction of text within a text frame.</summary>
public enum TextDirection
{
    /// <summary>Standard left-to-right horizontal text.</summary>
    Horizontal,
    /// <summary>Text rotated 90° clockwise (reads top-to-bottom).</summary>
    Vertical90,
    /// <summary>Text rotated 270° clockwise (reads bottom-to-top).</summary>
    Vertical270,
    /// <summary>
    /// Stacked text — each character is placed upright and stacked vertically,
    /// typically used for East Asian scripts.
    /// </summary>
    Stacked
}
