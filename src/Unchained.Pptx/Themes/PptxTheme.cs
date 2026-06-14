using Unchained.Ooxml.Drawing;

namespace Unchained.Pptx.Themes;

/// <summary>
///     The complete theme applied to a presentation, consisting of a colour scheme,
///     a font scheme, and a format scheme (fill, line, and effect style tiers).
/// </summary>
public sealed class PptxTheme
{
    /// <summary>The display name of the theme (e.g. "Office Theme").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The twelve-slot colour scheme.</summary>
    public ColorScheme Colors { get; set; } = new();

    /// <summary>The major and minor font sets for heading and body text.</summary>
    public FontScheme Fonts { get; set; } = new();
}
