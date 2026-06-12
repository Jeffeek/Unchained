using System.Xml.Linq;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Xml;

namespace Unchained.Pptx.Writing;

/// <summary>
///     Serializes <see cref="ColorSpec" /> values to DrawingML colour XML elements.
/// </summary>
internal static class ColorWriter
{
    /// <summary>
    ///     Returns the appropriate DrawingML colour element for <paramref name="color" />.
    /// </summary>
    public static XElement Write(ColorSpec color)
    {
        if (color.Type == ColorSpecType.ThemeSlot)
        {
            var slotName = SlotToString(color.ThemeSlot);
            var schemeEl = new XElement(DmlNames.SchemeColor,
                new XAttribute(DmlNames.AttributeValue, slotName));

            if (color.LuminanceModifier != 1.0)
            {
                schemeEl.Add(new XElement(DmlNames.LuminanceModifier,
                    new XAttribute(DmlNames.AttributeValue, (int)(color.LuminanceModifier * OoxmlScaling.PercentScale))));
            }

            if (color.LuminanceOffset != 0.0)
            {
                schemeEl.Add(new XElement(DmlNames.LuminanceOffset,
                    new XAttribute(DmlNames.AttributeValue, (int)(color.LuminanceOffset * OoxmlScaling.PercentScale))));
            }

            return schemeEl;
        }

        // RGB — emit as sRGB hex (strip alpha for the val attribute, keep as separate alpha child if < 255)
        var argb = color.Rgb;
        var alpha = (argb >> 24) & 0xFF;
        var hex = $"{argb & 0x00FFFFFF:X6}";
        var srgbEl = new XElement(DmlNames.SrgbColor,
            new XAttribute(DmlNames.AttributeValue, hex));

        if (alpha < 255)
        {
            srgbEl.Add(new XElement(DmlNames.Alpha,
                new XAttribute(DmlNames.AttributeValue, (int)Math.Round(alpha / 255.0 * OoxmlScaling.PercentScale))));
        }

        return srgbEl;
    }

    private static string SlotToString(ThemeColorSlot slot) => slot switch
    {
        ThemeColorSlot.Dark1 => "dk1",
        ThemeColorSlot.Light1 => "lt1",
        ThemeColorSlot.Dark2 => "dk2",
        ThemeColorSlot.Light2 => "lt2",
        ThemeColorSlot.Accent1 => "accent1",
        ThemeColorSlot.Accent2 => "accent2",
        ThemeColorSlot.Accent3 => "accent3",
        ThemeColorSlot.Accent4 => "accent4",
        ThemeColorSlot.Accent5 => "accent5",
        ThemeColorSlot.Accent6 => "accent6",
        ThemeColorSlot.Hyperlink => "hlink",
        ThemeColorSlot.FollowedHyperlink => "folHlink",
        _ => "dk1"
    };
}
