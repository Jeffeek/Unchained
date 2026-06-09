using System.Xml.Linq;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Xml;

namespace Unchained.Pptx.Writing;

/// <summary>
/// Serializes a <see cref="Shape3DFormat"/> to a DrawingML <c>&lt;a:sp3d&gt;</c> element, or
/// <see langword="null"/> when no 3-D settings are present.
/// </summary>
internal static class Shape3DWriter
{
    private static readonly XNamespace A = DmlNames.Dml;

    public static XElement? Write(Shape3DFormat threeD)
    {
        if (threeD.IsEmpty) return null;

        var sp3d = new XElement(A + "sp3d");
        if (threeD.ExtrusionHeight.Value != 0)
            sp3d.Add(new XAttribute("extrusionH", threeD.ExtrusionHeight.Value));
        if (threeD.ContourWidth.Value != 0)
            sp3d.Add(new XAttribute("contourW", threeD.ContourWidth.Value));
        if (!string.IsNullOrEmpty(threeD.Material))
            sp3d.Add(new XAttribute("prstMaterial", threeD.Material));

        if (threeD.TopBevel is { } bt) sp3d.Add(WriteBevel("bevelT", bt));
        if (threeD.BottomBevel is { } bb) sp3d.Add(WriteBevel("bevelB", bb));
        if (threeD.ExtrusionColor is { } ec)
            sp3d.Add(new XElement(A + "extrusionClr", ColorWriter.Write(ec)));
        if (threeD.ContourColor is { } cc)
            sp3d.Add(new XElement(A + "contourClr", ColorWriter.Write(cc)));

        return sp3d;
    }

    private static XElement WriteBevel(string name, BevelFormat bevel) =>
        new(A + name,
            new XAttribute("w", bevel.Width.Value),
            new XAttribute("h", bevel.Height.Value),
            new XAttribute("prst", bevel.Preset));
}
