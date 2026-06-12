namespace Unchained.Ooxml.Text;

/// <summary>
///     Default text layout values used across text frame rendering, export writers,
///     and the rasterizer. These mirror the PowerPoint default body margins and
///     fallback font sizes defined in ECMA-376.
/// </summary>
internal static class TextConstants
{
    // ── Default font size ─────────────────────────────────────────────────────

    /// <summary>
    ///     Fallback font size in points when a run has no explicit <c>sz</c> attribute.
    ///     PowerPoint uses 12 pt as the default body text size.
    /// </summary>
    internal const double DefaultFontSizePt = 12.0;

    // ── Line spacing ──────────────────────────────────────────────────────────

    /// <summary>
    ///     Default line height multiplier (line height = font size × this factor).
    ///     Matches the CSS <c>line-height: 1.25</c> used in HTML export and the
    ///     paragraph spacing assumed in SVG/PDF export.
    /// </summary>
    internal const double DefaultLineHeightFactor = 1.25;

    // ── Text frame internal margins ───────────────────────────────────────────

    /// <summary>
    ///     Default left/right internal margin in points (PowerPoint default = 7.2 pt = 0.1 inch).
    ///     Matches <c>TextFrameFormat.MarginLeft</c> / <c>MarginRight</c> defaults.
    /// </summary>
    internal const double DefaultMarginHorizontalPt = 7.2;

    /// <summary>
    ///     Default top/bottom internal margin in points (PowerPoint default = 3.6 pt = 0.05 inch).
    ///     Matches <c>TextFrameFormat.MarginTop</c> / <c>MarginBottom</c> defaults.
    /// </summary>
    internal const double DefaultMarginVerticalPt = 3.6;

    /// <summary>
    ///     Minimum inset applied in the rasterizer and export writers when no explicit
    ///     margin is available (4 px / 4 pt depending on context).
    /// </summary>
    internal const double MinTextInset = 4.0;

    // ── Fallback fonts ────────────────────────────────────────────────────────

    /// <summary>
    ///     Fallback Latin font name when a run specifies no font or the embedded
    ///     font cannot be resolved.
    /// </summary>
    internal const string FallbackLatinFont = "Arial";

    /// <summary>Bold variant of <see cref="FallbackLatinFont" />.</summary>
    internal const string FallbackLatinFontBold = "Arial Bold";
}
