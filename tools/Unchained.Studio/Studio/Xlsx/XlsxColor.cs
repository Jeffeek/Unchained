using System.Globalization;
using Unchained.Ooxml.Drawing;

namespace Unchained.Studio.Studio.Xlsx;

/// <summary>
///     Converts between <see cref="ColorSpec" /> and the <c>#RRGGBB</c> hex strings used by the
///     MudBlazor colour pickers in the XLSX formatting dialogs.
/// </summary>
public static class XlsxColor
{
    /// <summary>
    ///     Returns the <c>#RRGGBB</c> hex string for <paramref name="color" />, or <paramref name="fallback" /> when
    ///     null.
    /// </summary>
    public static string ToHex(ColorSpec? color, string fallback = "#000000")
    {
        if (color is null)
            return fallback;

        var argb = color.Value.Resolve(null);
        return $"#{argb & 0x00FFFFFF:X6}";
    }

    /// <summary>Parses a <c>#RRGGBB</c> (or <c>RRGGBB</c>) hex string into an opaque <see cref="ColorSpec" />.</summary>
    public static ColorSpec FromHex(string hex)
    {
        var clean = hex.TrimStart('#');
        if (clean.Length == 8) // ARGB → take RGB
            clean = clean[2..];
        return clean.Length != 6 || !uint.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)
            ? ColorSpec.FromRgb(0, 0, 0)
            : ColorSpec.FromRgb(
                (byte)((rgb >> 16) & 0xFF),
                (byte)((rgb >> 8) & 0xFF),
                (byte)(rgb & 0xFF)
            );
    }
}
