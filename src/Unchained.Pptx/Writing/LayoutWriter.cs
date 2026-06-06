using Unchained.Pptx.Core.Xml;
using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Writing;

/// <summary>
/// Serializes a <see cref="SlideLayout"/> to a PresentationML <c>&lt;p:sldLayout&gt;</c> element.
/// </summary>
internal static class LayoutWriter
{
    /// <summary>Returns a <c>&lt;p:sldLayout&gt;</c> root element for the given layout.</summary>
    public static XElement Write(SlideLayout layout)
    {
        // Prefer round-trip fidelity if we have the original element
        if (layout.RawElement != null)
            return layout.RawElement;

        var pml = PmlNames.Pml;
        var r = PmlNames.Relationships;
        var dml = DmlNames.Dml;

        var sldLayout = new XElement(PmlNames.SlideLayout,
            new XAttribute(XNamespace.Xmlns + "p", pml.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "a", dml.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "r", r.NamespaceName));

        if (!string.IsNullOrEmpty(layout.Name))
            sldLayout.Add(new XAttribute(PmlNames.AttributeName, layout.Name));

        var typeString = LayoutTypeToString(layout.LayoutType);
        if (!string.IsNullOrEmpty(typeString))
            sldLayout.Add(new XAttribute("type", typeString));

        // Common slide data
        var cSld = new XElement(PmlNames.CommonSlideData);
        if (!string.IsNullOrEmpty(layout.Name))
            cSld.Add(new XAttribute(PmlNames.AttributeName, layout.Name));

        var spTree = new XElement(PmlNames.ShapeTree);
        spTree.Add(new XElement(PmlNames.NonVisualGroupShapeProperties,
            new XElement(PmlNames.CommonNonVisualProperties,
                new XAttribute(PmlNames.AttributeId, 1),
                new XAttribute(PmlNames.AttributeName, string.Empty)),
            new XElement(pml + "cNvGrpSpPr"),
            new XElement(PmlNames.ApplicationNonVisualProperties)));
        spTree.Add(new XElement(PmlNames.GroupShapeProperties,
            new XElement(DmlNames.Transform,
                new XElement(DmlNames.Offset,
                    new XAttribute(DmlNames.AttributeX, 0),
                    new XAttribute(DmlNames.AttributeY, 0)),
                new XElement(DmlNames.Extents,
                    new XAttribute(DmlNames.AttributeWidth, 0),
                    new XAttribute(DmlNames.AttributeHeight, 0)))));

        foreach (var shape in layout.Shapes)
        {
            var el = ShapeWriter.Write(shape);
            if (el != null) spTree.Add(el);
        }

        cSld.Add(spTree);
        sldLayout.Add(cSld);

        sldLayout.Add(new XElement(PmlNames.ColorMapOverride,
            new XElement(dml + "masterClrMapping")));

        return sldLayout;
    }

    private static string LayoutTypeToString(Models.Themes.LayoutType type) => type switch
    {
        Models.Themes.LayoutType.Blank => "blank",
        Models.Themes.LayoutType.Title => "title",
        Models.Themes.LayoutType.TitleAndContent => "obj",
        Models.Themes.LayoutType.TitleAndTwoContent => "twoObj",
        Models.Themes.LayoutType.TitleOnly => "titleOnly",
        Models.Themes.LayoutType.SectionHeader => "secHead",
        Models.Themes.LayoutType.TitleSlide => "ctrTitle",
        Models.Themes.LayoutType.TwoTextColumns => "twoTx",
        Models.Themes.LayoutType.TitleAndVerticalText => "vertTx",
        Models.Themes.LayoutType.PictureWithCaption => "picTx",
        _ => string.Empty
    };
}
