using System.Xml.Linq;
using Unchained.Ooxml;
using Unchained.Ooxml.Xml;
using Unchained.Ooxml.Drawing;

namespace Unchained.Pptx.Parsing;

/// <summary>
/// Parses DrawingML colour elements into <see cref="ColorSpec"/> values.
/// </summary>
internal static class ColorParser
{
    /// <summary>
    /// Reads the first recognised colour child element from <paramref name="parent"/>
    /// and returns the corresponding <see cref="ColorSpec"/>.
    /// Returns a default mid-grey when no colour element is found.
    /// </summary>
    public static ColorSpec Parse(XElement parent)
    {
        // sRGB absolute colour
        var srgb = parent.Element(DmlNames.SrgbColor);
        if (srgb != null)
        {
            var hex = srgb.GetAttr(DmlNames.AttributeValue, "000000");
            if (TryParseHex(hex, out var argb))
                return ColorSpec.FromArgb(
                    ReadAlpha(srgb),
                    (byte)((argb >> 16) & 0xFF),
                    (byte)((argb >> 8) & 0xFF),
                    (byte)(argb & 0xFF));
        }

        // Theme slot reference
        var scheme = parent.Element(DmlNames.SchemeColor);
        if (scheme != null)
        {
            var slotName = scheme.GetAttr(DmlNames.AttributeValue, string.Empty);
            var slot = ParseThemeSlot(slotName);
            var luminanceModifier = ReadTransformValue(scheme, DmlNames.LuminanceModifier);
            var luminanceOffset = ReadTransformValue(scheme, DmlNames.LuminanceOffset);
            return ColorSpec.FromTheme(slot, luminanceModifier, luminanceOffset);
        }

        // System colour (e.g. windowText) — use lastClr as fallback
        var sysClr = parent.Element(DmlNames.SystemColor);
        if (sysClr != null)
        {
            var lastClr = sysClr.GetAttr("lastClr", "000000");
            if (TryParseHex(lastClr, out var argb))
                return ColorSpec.FromRgb(
                    (byte)((argb >> 16) & 0xFF),
                    (byte)((argb >> 8) & 0xFF),
                    (byte)(argb & 0xFF));
        }

        // Preset colour — map to a neutral value for now
        var prstClr = parent.Element(DmlNames.PresetColor);
        if (prstClr != null)
        {
            var name = prstClr.GetAttr(DmlNames.AttributeValue, "black");
            return ParsePresetColor(name);
        }

        return ColorSpec.FromRgb(0x80, 0x80, 0x80); // neutral grey fallback
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static byte ReadAlpha(XElement colorElement)
    {
        var alphaEl = colorElement.Element(DmlNames.Alpha);
        if (alphaEl == null)
            return 255;

        var raw = alphaEl.GetAttrInt(DmlNames.AttributeValue);
        if (raw == null)
            return 255;

        return (byte)Math.Clamp((int)Math.Round(raw.Value / (double)OoxmlScaling.PercentScale * 255), 0, 255);
    }

    private static double ReadTransformValue(XElement parent, XName childName)
    {
        var child = parent.Element(childName);
        if (child == null) return childName == DmlNames.LuminanceModifier ? 1.0 : 0.0;
        var raw = child.GetAttrInt(DmlNames.AttributeValue);
        if (raw == null) return childName == DmlNames.LuminanceModifier ? 1.0 : 0.0;
        return raw.Value / (double)OoxmlScaling.PercentScale;
    }

    private static bool TryParseHex(string hex, out uint value)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
            hex = "FF" + hex;
        return uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out value);
    }

    private static ThemeColorSlot ParseThemeSlot(string name) => name switch
    {
        "dk1" => ThemeColorSlot.Dark1,
        "lt1" => ThemeColorSlot.Light1,
        "dk2" => ThemeColorSlot.Dark2,
        "lt2" => ThemeColorSlot.Light2,
        "accent1" => ThemeColorSlot.Accent1,
        "accent2" => ThemeColorSlot.Accent2,
        "accent3" => ThemeColorSlot.Accent3,
        "accent4" => ThemeColorSlot.Accent4,
        "accent5" => ThemeColorSlot.Accent5,
        "accent6" => ThemeColorSlot.Accent6,
        "hlink" => ThemeColorSlot.Hyperlink,
        "folHlink" => ThemeColorSlot.FollowedHyperlink,
        _ => ThemeColorSlot.Dark1
    };

    private static ColorSpec ParsePresetColor(string name) => name switch
    {
        "white" => ColorSpec.FromRgb(0xFF, 0xFF, 0xFF),
        "black" => ColorSpec.FromRgb(0x00, 0x00, 0x00),
        "red" => ColorSpec.FromRgb(0xFF, 0x00, 0x00),
        "green" => ColorSpec.FromRgb(0x00, 0x80, 0x00),
        "blue" => ColorSpec.FromRgb(0x00, 0x00, 0xFF),
        "yellow" => ColorSpec.FromRgb(0xFF, 0xFF, 0x00),
        "cyan" => ColorSpec.FromRgb(0x00, 0xFF, 0xFF),
        "magenta" => ColorSpec.FromRgb(0xFF, 0x00, 0xFF),
        "orange" => ColorSpec.FromRgb(0xFF, 0xA5, 0x00),
        "purple" => ColorSpec.FromRgb(0x80, 0x00, 0x80),
        "gray" or "grey" => ColorSpec.FromRgb(0x80, 0x80, 0x80),
        _ => ColorSpec.FromRgb(0x80, 0x80, 0x80)
    };
}
