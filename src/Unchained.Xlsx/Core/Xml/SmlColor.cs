using System.Globalization;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Xml;

namespace Unchained.Xlsx.Core.Xml;

/// <summary>
///     Converts between SpreadsheetML hex ARGB colour strings (e.g. <c>"FF4472C4"</c>) and
///     <see cref="ColorSpec" /> values. SpreadsheetML colours may also be theme-indexed
///     (<c>theme</c>/<c>tint</c>) or palette-indexed (<c>indexed</c>); this helper handles the
///     common <c>rgb</c> and <c>theme</c> forms.
/// </summary>
internal static class SmlColor
{
    /// <summary>
    ///     Parses an 8-digit hex ARGB string into a <see cref="ColorSpec" />.
    ///     Returns <see langword="null" /> when <paramref name="hex" /> is null or malformed.
    /// </summary>
    public static ColorSpec? FromHexArgb(string? hex)
    {
        if (!OoXmlHelper.TryParseHexArgb(hex!, out var argb))
            return null;

        return ColorSpec.FromArgb(
            (byte)((argb >> 24) & 0xFF),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF)
        );
    }

    /// <summary>
    ///     Renders the RGB value of <paramref name="color" /> as an 8-digit hex ARGB string.
    ///     Theme-slot colours are resolved to a neutral value first.
    /// </summary>
    public static string ToHexArgb(ColorSpec color) =>
        color.Resolve(null).ToString("X8", CultureInfo.InvariantCulture);
}
