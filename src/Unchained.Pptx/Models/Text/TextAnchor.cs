namespace Unchained.Pptx.Models.Text;

/// <summary>Vertical anchor position of text within a text frame.</summary>
public enum TextAnchor
{
    /// <summary>Text begins at the top of the frame.</summary>
    Top,
    /// <summary>Text is vertically centred within the frame.</summary>
    Middle,
    /// <summary>Text is aligned to the bottom of the frame.</summary>
    Bottom,
    /// <summary>Text begins at the top and is centred horizontally.</summary>
    TopCentered,
    /// <summary>Text is centred both vertically and horizontally.</summary>
    MiddleCentered,
    /// <summary>Text is aligned to the bottom and centred horizontally.</summary>
    BottomCentered
}
