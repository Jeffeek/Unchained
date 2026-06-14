namespace Unchained.Ooxml.Text;

/// <summary>
///     Controls how a text frame resizes itself or its text when the content
///     does not fit within the frame's current dimensions.
/// </summary>
public enum TextAutofit
{
    /// <summary>No automatic sizing; text may overflow the frame.</summary>
    None,
    /// <summary>The font size is reduced automatically until the text fits.</summary>
    ShrinkText,
    /// <summary>The frame is resized to fit the text content.</summary>
    ResizeShape
}
