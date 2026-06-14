namespace Unchained.Ooxml.Drawing;

/// <summary>
///     Defines a set of fonts for a specific text role (major or minor) in a theme,
///     covering the Latin script, East Asian scripts, complex scripts, and per-script overrides.
/// </summary>
public sealed class ThemeFontSet
{
    /// <summary>
    ///     The font family name for Latin-script text (e.g. "Calibri", "Calibri Light").
    ///     Use <c>"+mj-lt"</c> to inherit the major Latin font, or <c>"+mn-lt"</c> for the minor Latin font.
    /// </summary>
    public string LatinFont { get; set; } = string.Empty;

    /// <summary>The font family name for East Asian script text.</summary>
    public string EastAsianFont { get; set; } = string.Empty;

    /// <summary>The font family name for complex-script text (e.g. Arabic, Hebrew).</summary>
    public string ComplexScriptFont { get; set; } = string.Empty;

    /// <summary>
    ///     Per-script font overrides, keyed by IETF script tag (e.g. <c>"Arab"</c>, <c>"Jpan"</c>).
    /// </summary>
    public Dictionary<string, string> ScriptFonts { get; } = new(StringComparer.Ordinal);
}
