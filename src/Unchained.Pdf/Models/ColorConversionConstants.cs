namespace Unchained.Pdf.Models;

/// <summary>Color space conversion constants (IEC 61966-2-1 sRGB, CIE XYZ, CIE Lab).</summary>
internal static class ColorConversionConstants
{
    // ── XYZ → sRGB matrix (IEC 61966-2-1, D65 adaptation) ───────────────────

    /// <summary>XYZ→sRGB matrix row 0, col 0.</summary>
    internal const double XyzToSRgbM00 =  3.2404542;
    /// <summary>XYZ→sRGB matrix row 0, col 1.</summary>
    internal const double XyzToSRgbM01 = -1.5371385;
    /// <summary>XYZ→sRGB matrix row 0, col 2.</summary>
    internal const double XyzToSRgbM02 = -0.4985314;
    /// <summary>XYZ→sRGB matrix row 1, col 0.</summary>
    internal const double XyzToSRgbM10 = -0.9692660;
    /// <summary>XYZ→sRGB matrix row 1, col 1.</summary>
    internal const double XyzToSRgbM11 =  1.8760108;
    /// <summary>XYZ→sRGB matrix row 1, col 2.</summary>
    internal const double XyzToSRgbM12 =  0.0415560;
    /// <summary>XYZ→sRGB matrix row 2, col 0.</summary>
    internal const double XyzToSRgbM20 =  0.0556434;
    /// <summary>XYZ→sRGB matrix row 2, col 1.</summary>
    internal const double XyzToSRgbM21 = -0.2040259;
    /// <summary>XYZ→sRGB matrix row 2, col 2.</summary>
    internal const double XyzToSRgbM22 =  1.0572252;

    // ── sRGB gamma transfer function (IEC 61966-2-1) ─────────────────────────

    /// <summary>Upper bound of the sRGB linear segment; values ≤ this use the linear formula.</summary>
    internal const double SRgbGammaThreshold   = 0.0031308;
    /// <summary>Slope of the sRGB linear segment: output = SRgbGammaLinearSlope × input.</summary>
    internal const double SRgbGammaLinearSlope = 12.92;
    /// <summary>Scale factor of the sRGB power-law segment.</summary>
    internal const double SRgbGammaScale       = 1.055;
    /// <summary>Offset subtracted in the sRGB power-law segment.</summary>
    internal const double SRgbGammaOffset      = 0.055;
    /// <summary>Exponent of the sRGB power-law segment (reciprocal is 1/2.4 ≈ 0.4167).</summary>
    internal const double SRgbGammaExponent    = 2.4;

    // ── CIE D65 illuminant XYZ ───────────────────────────────────────────────

    /// <summary>CIE D65 illuminant X tristimulus value (≈ 0.9505).</summary>
    internal const double D65WhiteX = 0.9505;
    /// <summary>CIE D65 illuminant Y tristimulus value (= 1.0 by definition).</summary>
    internal const double D65WhiteY = 1.0000;
    /// <summary>CIE D65 illuminant Z tristimulus value (≈ 1.0890).</summary>
    internal const double D65WhiteZ = 1.0890;

    // ── CIE Lab → XYZ f(t) function ──────────────────────────────────────────

    /// <summary>
    /// Lab f(t) crossover threshold (≈ (6/29)³ ≈ 0.206897).
    /// Above this value the cube-root branch is used; below, the linear branch.
    /// </summary>
    internal const double LabCubeRootThreshold = 0.206897;

    /// <summary>Lab f(t) linear segment slope (= 29²/108 ≈ 7.787).</summary>
    internal const double LabLinearSlope = 7.787;

    /// <summary>Lab f(t) linear offset numerator.</summary>
    internal const double LabLinearOffsetNumerator = 16.0;

    /// <summary>Lab f(t) linear offset denominator; offset = 16/116.</summary>
    internal const double LabLinearOffsetDenominator = 116.0;
}
