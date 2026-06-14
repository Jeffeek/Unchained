namespace Unchained.Pptx.Rendering.Engine;

/// <summary>
///     Rendering-specific constants used by <see cref="SlideRasterizer" /> and related helpers.
/// </summary>
internal static class RenderingConstants
{
    // ── DPI and scale ─────────────────────────────────────────────────────────

    /// <summary>
    ///     Points per inch. Used to convert font sizes in points to device pixels:
    ///     <c>pixelSize = fontSizePt * (dpi / PointsPerInch)</c>.
    /// </summary>
    internal const double PointsPerInch = 72.0;

    /// <summary>
    ///     Standard screen DPI. Used for HTML/CSS pixel calculations when no
    ///     explicit DPI is provided.
    /// </summary>
    internal const double StandardScreenDpi = 96.0;

    // ── Fallback fonts ────────────────────────────────────────────────────────

    /// <summary>
    ///     Fallback Latin font name used when a run specifies no font or the
    ///     embedded font cannot be resolved.
    /// </summary>
    internal const string FallbackLatinFont = "Arial";

    /// <summary>
    ///     Bold variant of the fallback Latin font.
    /// </summary>
    internal const string FallbackLatinFontBold = "Arial Bold";

    /// <summary>
    ///     Fallback font used in PDF export (PDF standard Type1 base font).
    /// </summary>
    internal const string PdfFallbackFont = "Helvetica";

    // ── Chart rendering ───────────────────────────────────────────────────────

    /// <summary>Left axis margin in pixels reserved for value-axis labels.</summary>
    internal const int ChartAxisMarginLeft = 40;

    /// <summary>Bottom margin in pixels reserved for category-axis labels.</summary>
    internal const int ChartAxisMarginBottom = 18;
}
