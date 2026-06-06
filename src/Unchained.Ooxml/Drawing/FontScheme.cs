namespace Unchained.Ooxml.Drawing;

/// <summary>
/// Defines the font families used for headings (major font) and body text (minor font)
/// in a presentation theme.
/// </summary>
public sealed class FontScheme
{
    /// <summary>The name of the font scheme.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The major (heading) font set. Typically used for titles and section headers.</summary>
    public ThemeFontSet MajorFont { get; set; } = new();

    /// <summary>The minor (body) font set. Typically used for paragraph text, captions, and labels.</summary>
    public ThemeFontSet MinorFont { get; set; } = new();
}
