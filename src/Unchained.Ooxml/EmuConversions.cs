namespace Unchained.Ooxml;

/// <summary>
///     English Metric Unit (EMU) conversion ratios.
///     All OOXML coordinates and sizes are stored as integer EMU values.
/// </summary>
internal static class EmuConversions
{
    /// <summary>EMU per inch: 1 inch = 914 400 EMU.</summary>
    internal const long EmuPerInch = 914_400;

    /// <summary>EMU per point: 1 pt = 12 700 EMU.</summary>
    internal const long EmuPerPoint = 12_700;

    /// <summary>
    ///     EMU per CSS/screen pixel at 96 DPI: 1 px = 914 400 / 96 = 9 525 EMU.
    ///     Used when converting slide coordinates to HTML/CSS pixel values.
    /// </summary>
    internal const long EmuPerPixel96Dpi = 9_525;

    /// <summary>Multiplier to convert EMU to points: 1.0 / 12 700.</summary>
    internal const double EmuToPoints = 1.0 / EmuPerPoint;

    /// <summary>Multiplier to convert EMU to CSS pixels at 96 DPI: 1.0 / 9 525.</summary>
    internal const double EmuToCssPx = 1.0 / EmuPerPixel96Dpi;
}
