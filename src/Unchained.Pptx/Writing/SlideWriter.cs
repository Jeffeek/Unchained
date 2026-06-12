using Unchained.Pptx.Core.Xml;
using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Writing;

/// <summary>
/// Serializes a <see cref="Slide"/> to a PresentationML <c>&lt;p:sld&gt;</c> root element.
/// </summary>
internal static class SlideWriter
{
    /// <summary>Returns a <c>&lt;p:sld&gt;</c> element for the given slide.</summary>
    public static XElement Write(Slide slide)
    {
        var pml = PmlNames.Pml;
        var r = PmlNames.Relationships;
        var dml = DmlNames.Dml;

        var sld = new XElement(PmlNames.Slide,
            new XAttribute(XNamespace.Xmlns + "p", pml.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "a", dml.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "r", r.NamespaceName));

        if (slide.IsHidden)
            sld.Add(new XAttribute(PmlNames.AttributeShow, "0"));

        // Common slide data
        var cSld = new XElement(PmlNames.CommonSlideData);
        if (!string.IsNullOrEmpty(slide.Name))
            cSld.Add(new XAttribute(PmlNames.AttributeName, slide.Name));

        // Shape tree
        var spTree = new XElement(PmlNames.ShapeTree);
        spTree.Add(WriteGroupShapeNonVisualProperties());
        spTree.Add(WriteGroupShapeProperties());

        foreach (var shape in slide.Shapes)
        {
            var el = ShapeWriter.Write(shape);
            if (el != null)
                spTree.Add(el);
        }

        cSld.Add(spTree);
        sld.Add(cSld);

        // Colour map override
        if (slide.ColorMapOverrideElement != null)
            sld.Add(slide.ColorMapOverrideElement);
        else
            sld.Add(new XElement(PmlNames.ColorMapOverride,
                new XElement(dml + "masterClrMapping")));

        // Transition (M6)
        var transitionEl = TransitionWriter.Write(slide.Transition);
        if (transitionEl != null)
            sld.Add(transitionEl);

        // Animation timing (M6)
        var timingEl = AnimationWriter.Write(slide.Animations);
        if (timingEl != null)
            sld.Add(timingEl);

        return sld;
    }

    private static XElement WriteGroupShapeNonVisualProperties()
    {
        var nvGrpSpPr = new XElement(PmlNames.NonVisualGroupShapeProperties);
        nvGrpSpPr.Add(new XElement(PmlNames.CommonNonVisualProperties,
            new XAttribute(PmlNames.AttributeId, 1),
            new XAttribute(PmlNames.AttributeName, string.Empty)));
        nvGrpSpPr.Add(new XElement(PmlNames.Pml + "cNvGrpSpPr"));
        nvGrpSpPr.Add(new XElement(PmlNames.ApplicationNonVisualProperties));
        return nvGrpSpPr;
    }

    private static XElement WriteGroupShapeProperties()
    {
        var grpSpPr = new XElement(PmlNames.GroupShapeProperties);
        var xfrm = new XElement(DmlNames.Transform);
        xfrm.Add(new XElement(DmlNames.Offset,
            new XAttribute(DmlNames.AttributeX, 0),
            new XAttribute(DmlNames.AttributeY, 0)));
        xfrm.Add(new XElement(DmlNames.Extents,
            new XAttribute(DmlNames.AttributeWidth, 0),
            new XAttribute(DmlNames.AttributeHeight, 0)));
        xfrm.Add(new XElement(DmlNames.Dml + "chOff",
            new XAttribute(DmlNames.AttributeX, 0),
            new XAttribute(DmlNames.AttributeY, 0)));
        xfrm.Add(new XElement(DmlNames.Dml + "chExt",
            new XAttribute(DmlNames.AttributeWidth, 0),
            new XAttribute(DmlNames.AttributeHeight, 0)));
        grpSpPr.Add(xfrm);
        return grpSpPr;
    }
}
