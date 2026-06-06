namespace Unchained.Ooxml.Drawing;

/// <summary>
/// Indicates whether a <see cref="ColorSpec"/> references an absolute RGB value
/// or a slot in the presentation theme's colour scheme.
/// </summary>
public enum ColorSpecType
{
    /// <summary>An absolute ARGB colour value not tied to the theme.</summary>
    Rgb,

    /// <summary>A reference to one of the twelve named slots in the theme's colour scheme.</summary>
    ThemeSlot
}
