using Unchained.Pptx.Core.Xml;
using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Writing;

/// <summary>
/// Serializes a <see cref="MasterSlide"/> to a PresentationML <c>&lt;p:sldMaster&gt;</c> element.
/// </summary>
internal static class MasterWriter
{
    /// <summary>Returns a <c>&lt;p:sldMaster&gt;</c> root element for the given master.</summary>
    public static XElement Write(
        MasterSlide master,
        string themeUri,
        Dictionary<SlideLayout, string> layoutUris)
    {
        // Prefer round-trip fidelity if we have the original element
        if (master.RawElement != null)
            return master.RawElement;

        var pml = PmlNames.Pml;
        var r = PmlNames.Relationships;
        var dml = DmlNames.Dml;

        var sldMaster = new XElement(PmlNames.SlideMaster,
            new XAttribute(XNamespace.Xmlns + "p", pml.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "a", dml.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "r", r.NamespaceName));

        // Common slide data
        var cSld = new XElement(PmlNames.CommonSlideData);
        if (!string.IsNullOrEmpty(master.Name))
            cSld.Add(new XAttribute(PmlNames.AttributeName, master.Name));

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

        foreach (var shape in master.Shapes)
        {
            var el = ShapeWriter.Write(shape);
            if (el != null) spTree.Add(el);
        }

        cSld.Add(spTree);
        sldMaster.Add(cSld);

        // Layout ID list
        var sldLayoutIdLst = new XElement(PmlNames.SlideLayoutIdList);
        var idCounter = 2147483649u;
        foreach (var layout in master.Layouts)
        {
            sldLayoutIdLst.Add(new XElement(PmlNames.SlideLayoutId,
                new XAttribute(PmlNames.AttributeId, idCounter++),
                new XAttribute(PmlNames.RelationshipId,
                    layout.RelationshipId.Length > 0 ? layout.RelationshipId : "rId1")));
        }
        sldMaster.Add(sldLayoutIdLst);

        sldMaster.Add(new XElement(pml + "txStyles"));
        sldMaster.Add(new XElement(PmlNames.ColorMapOverride,
            new XElement(dml + "masterClrMapping")));

        return sldMaster;
    }
}
