namespace Unchained.Ooxml;

/// <summary>
/// OOXML integer scaling factors used throughout PresentationML parsing and writing.
/// ECMA-376 stores many values as integers scaled by a fixed factor.
/// </summary>
internal static class OoxmlScaling
{
    /// <summary>
    /// Scale factor for percentage-like values: luminance modifiers, gradient stop positions,
    /// animation acceleration/deceleration, alpha channel values.
    /// 100 % = 100 000; 0 % = 0. Divide by this to get a 0.0–1.0 fraction.
    /// </summary>
    internal const int PercentScale = 100_000;

    /// <summary>Alpha: fully opaque = 255 in 0–255 space → 100 000 in OOXML space.</summary>
    internal const int AlphaScale = 100_000;

    /// <summary>
    /// Grey fallback ARGB (0xFF808080) returned when a theme colour slot cannot be resolved
    /// (e.g. the colour scheme has not been initialised).
    /// </summary>
    internal const uint UnresolvedThemeColorArgb = 0xFF808080u;

    /// <summary>Theme major Latin font reference token (<c>+mj-lt</c>).</summary>
    internal const string ThemeMajorLatinFont = "+mj-lt";

    /// <summary>Theme minor (body) Latin font reference token (<c>+mn-lt</c>).</summary>
    internal const string ThemeMinorLatinFont = "+mn-lt";
}
