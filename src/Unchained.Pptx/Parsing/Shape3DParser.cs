using System.Xml.Linq;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Core.Xml;

namespace Unchained.Pptx.Parsing;

/// <summary>
///     Parses the DrawingML <c>&lt;a:sp3d&gt;</c> 3-D format element into a <see cref="Shape3DFormat" />.
/// </summary>
internal static class Shape3DParser
{
    private static readonly XNamespace A = DmlNames.Dml;

    public static void Parse(XElement? spPr, Shape3DFormat threeD)
    {
        var sp3d = spPr?.Element(A + "sp3d");
        if (sp3d is null) return;

        threeD.ExtrusionHeight = new Emu(sp3d.GetAttrLong("extrusionH", 0));
        threeD.ContourWidth = new Emu(sp3d.GetAttrLong("contourW", 0));
        threeD.Material = sp3d.GetAttr(PmlNames.AttributePrstMaterial);

        if (sp3d.Element(A + "bevelT") is { } bt) threeD.TopBevel = ParseBevel(bt);
        if (sp3d.Element(A + "bevelB") is { } bb) threeD.BottomBevel = ParseBevel(bb);
        if (sp3d.Element(A + "extrusionClr") is { } ec) threeD.ExtrusionColor = ColorParser.Parse(ec);
        if (sp3d.Element(A + "contourClr") is { } cc) threeD.ContourColor = ColorParser.Parse(cc);
    }

    private static BevelFormat ParseBevel(XElement el) => new()
    {
        Width = new Emu(el.GetAttrLong("w", 0)),
        Height = new Emu(el.GetAttrLong("h", 0)),
        Preset = el.GetAttr("prst", "circle")
    };
}
