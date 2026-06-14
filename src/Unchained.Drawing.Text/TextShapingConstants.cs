namespace Unchained.Drawing.Text;

/// <summary>Constants for HarfBuzz text shaping shared by the rendering assemblies.</summary>
internal static class TextShapingConstants
{
    /// <summary>
    ///     HarfBuzz fixed-point divisor (26.6 format). Glyph advances and offsets are reported
    ///     in 1/64ths of a unit. Divide by this to recover whole units.
    ///     Declared as <see langword="int" />; callers needing floating-point division cast to
    ///     <see langword="double" /> at the use site.
    /// </summary>
    internal const int HarfBuzzFixed = 64;
}
