namespace Unchained.Pdf.Rendering.Rendering;

/// <summary>
///     Named constants for the PDF page rasterizer — fixed-point scales, colour/alpha limits,
///     geometric tolerances, text-rendering modes, and the colour-space / blend-mode name
///     strings defined by ISO 32000-1. Promoting the repeated and non-obvious literals here
///     keeps <see cref="PageRenderer" /> readable and the values in one auditable place.
/// </summary>
internal static class RenderingConstants
{
    // ── Fixed-point / font scales ───────────────────────────────────────────

    /// <summary>HarfBuzz reports glyph metrics in 26.6 fixed-point; divide by 64 for pixels.</summary>
    internal const double HarfBuzzFixed = 64.0;

    /// <summary>FreeType <c>FT_Get_Advance</c> returns 16.16 fixed-point; divide by 65536 for pixels.</summary>
    internal const double FreeTypeFixed = 65536.0;

    /// <summary>CIDFont <c>/W</c> widths are in glyph-space units, 1000 per em.</summary>
    internal const double CidEmUnits = 1000.0;

    /// <summary>Text <c>Tz</c> horizontal scaling is a percentage; divide by 100 for a factor.</summary>
    internal const double HorizontalScalePercent = 100.0;

    // ── Colour / alpha ──────────────────────────────────────────────────────

    /// <summary>Maximum 8-bit channel value; also fully-opaque alpha.</summary>
    internal const byte OpaqueAlpha = 255;

    /// <summary>Maximum 8-bit channel value (clamp ceiling for colour conversion).</summary>
    internal const int ByteMax = 255;

    // Integer luminance weights for the /Luminosity soft-mask formula
    // (luma = (LumaR·R + LumaG·G + LumaB·B) >> LumaShift). ISO 32000-1 §11.6.5.

    /// <summary>Red luminance weight (≈0.30 × 256).</summary>
    internal const int LumaR = 77;

    /// <summary>Green luminance weight (≈0.59 × 256).</summary>
    internal const int LumaG = 150;

    /// <summary>Blue luminance weight (≈0.11 × 256).</summary>
    internal const int LumaB = 29;

    /// <summary>Right-shift applied after the weighted luminance sum (÷256).</summary>
    internal const int LumaShift = 8;

    // ── Geometric tolerances ────────────────────────────────────────────────

    /// <summary>General near-zero tolerance for length / scale comparisons.</summary>
    internal const double Epsilon = 1e-6;

    /// <summary>Near-zero tolerance for matrix determinant / degeneracy checks.</summary>
    internal const double DeterminantEpsilon = 1e-9;

    /// <summary>Tighter tolerance guarding the matrix-inverse determinant.</summary>
    internal const double MatrixInverseEpsilon = 1e-12;

    // ── Text rendering modes (ISO 32000-1 §9.3.6, the Tr operator) ───────────

    internal const int TextModeFill = 0;
    internal const int TextModeStroke = 1;
    internal const int TextModeFillStroke = 2;
    internal const int TextModeInvisible = 3;
    internal const int TextModeFillClip = 4;
    internal const int TextModeStrokeClip = 5;
    internal const int TextModeFillStrokeClip = 6;
    internal const int TextModeClip = 7;

    // ── Colour-space / blend-mode / soft-mask name strings ───────────────────

    internal const string DeviceGray = "DeviceGray";
    internal const string DeviceRgb = "DeviceRGB";
    internal const string DeviceCmyk = "DeviceCMYK";

    /// <summary>Default blend mode — simple alpha compositing (ISO 32000-1 §11.3.5).</summary>
    internal const string BlendNormal = "Normal";

    /// <summary>Soft-mask subtype that derives opacity from rendered luminance.</summary>
    internal const string SoftMaskLuminosity = "Luminosity";
}
