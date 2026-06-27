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

    // ── Text rendering ────────────────────────────────────────────────────────

    /// <summary>Default font size (points) for text frames without explicit sizing.</summary>
    internal const double DefaultFontSizePt = 12.0;

    // ── WordArt warp ──────────────────────────────────────────────────────────

    /// <summary>Maximum WordArt warp offset as a fraction of text height (0.25).</summary>
    internal const double WarpAmplitudeFraction = 0.25;

    // ── Drop shadow ───────────────────────────────────────────────────────────

    /// <summary>Maximum drop-shadow blur radius in pixels.</summary>
    internal const int ShadowBlurMaxPx = 12;

    // ── Fill / background colours (RGB) ───────────────────────────────────────

    /// <summary>White (255).</summary>
    internal const byte White = 255;

    /// <summary>Light grey for image placeholders (235).</summary>
    internal const byte PlaceholderFill = 235;

    /// <summary>Muted grey for borders (180).</summary>
    internal const byte BorderMuted = 180;

    /// <summary>Light grey for table grid lines (200).</summary>
    internal const byte BorderLight = 200;

    /// <summary>Dark grey for axis tick labels (100).</summary>
    internal const byte TextAxis = 100;

    /// <summary>Medium grey for chart labels (80).</summary>
    internal const byte LabelGrey = 80;

    /// <summary>Deep blue for undecoded image placeholder (40, 100, 220).</summary>
    internal const byte UndecodedBlueR = 40;
    internal const byte UndecodedBlueG = 100;
    internal const byte UndecodedBlueB = 220;

    // ── Bevel ─────────────────────────────────────────────────────────────────

    /// <summary>Highlight line alpha at the bevel edge (180).</summary>
    internal const byte BevelHighlightAlpha = 180;

    /// <summary>Highlight line alpha decay per pixel (20).</summary>
    internal const byte BevelHighlightDecay = 20;

    /// <summary>Shadow line alpha at the bevel edge (140).</summary>
    internal const byte BevelShadowAlpha = 140;

    /// <summary>Shadow line alpha decay per pixel (15).</summary>
    internal const byte BevelShadowDecay = 15;

    // ── Other ─────────────────────────────────────────────────────────────────

    /// <summary>WordArt "non-white" pixel threshold — pixels brighter than this are skipped.</summary>
    internal const byte WarpSkipBrightThreshold = 250;
}
