namespace Unchained.Drawing.Constants;

/// <summary>
///     Standard 14 PDF font names and fallback font names used across the codebase.
///     Placed here so that <c>Unchained.Pdf</c>, <c>Unchained.Pdf.Rendering</c>,
///     and <c>Unchained.Drawing.Text</c> can share a single source of truth.
/// </summary>
public static class FontFallbackNames
{
    // ── Standard 14 PDF fonts (ISO 32000-1 §9.6.2) ────────────────────────────

    /// <summary>Helvetica Standard 14 font name.</summary>
    public const string Helvetica = "Helvetica";

    /// <summary>Helvetica-Bold Standard 14 font name.</summary>
    public const string HelveticaBold = "Helvetica-Bold";

    /// <summary>Helvetica-BoldOblique Standard 14 font name.</summary>
    public const string HelveticaBoldOblique = "Helvetica-BoldOblique";

    /// <summary>Helvetica-Oblique Standard 14 font name.</summary>
    public const string HelveticaOblique = "Helvetica-Oblique";

    /// <summary>Times-Roman Standard 14 font name.</summary>
    public const string TimesRoman = "Times-Roman";

    /// <summary>Times-Bold Standard 14 font name.</summary>
    public const string TimesBold = "Times-Bold";

    /// <summary>Times-Italic Standard 14 font name.</summary>
    public const string TimesItalic = "Times-Italic";

    /// <summary>Times-BoldItalic Standard 14 font name.</summary>
    public const string TimesBoldItalic = "Times-BoldItalic";

    /// <summary>Courier Standard 14 font name.</summary>
    public const string Courier = "Courier";

    /// <summary>Courier-Bold Standard 14 font name.</summary>
    public const string CourierBold = "Courier-Bold";

    /// <summary>Courier-Oblique Standard 14 font name.</summary>
    public const string CourierOblique = "Courier-Oblique";

    /// <summary>Courier-BoldOblique Standard 14 font name.</summary>
    public const string CourierBoldOblique = "Courier-BoldOblique";

    /// <summary>Symbol Standard 14 font name.</summary>
    public const string Symbol = "Symbol";

    /// <summary>ZapfDingbats Standard 14 font name.</summary>
    public const string ZapfDingbats = "ZapfDingbats";

    // ── Fallback font names (used by FontCache and Standard14Widths) ─────────

    /// <summary>Fallback font name for Arial (maps to DejaVu Sans).</summary>
    public const string FallbackArial = "Arial";

    /// <summary>Fallback font name for Arial-Italic (maps to DejaVu Sans Oblique).</summary>
    public const string FallbackArialItalic = "Arial-Italic";

    /// <summary>Fallback font name for Arial-Bold (maps to DejaVu Sans Bold).</summary>
    public const string FallbackArialBold = "Arial-Bold";

    /// <summary>Fallback font name for Arial-BoldItalic (maps to DejaVu Sans Bold Oblique).</summary>
    public const string FallbackArialBoldItalic = "Arial-BoldItalic";

    /// <summary>Fallback font name for Times-Italic (maps to DejaVu Serif Regular).</summary>
    public const string FallbackTimesItalic = "Times-Italic";

    /// <summary>Fallback font name for Times-BoldItalic (maps to DejaVu Serif Bold Italic).</summary>
    public const string FallbackTimesBoldItalic = "Times-BoldItalic";

    /// <summary>Fallback font name for Calibri (maps to DejaVu Sans).</summary>
    public const string FallbackCalibri = "Calibri";
}
