using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Engine;
using Unchained.Ooxml.Media;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Animations;
using Unchained.Pptx.Core;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;
using Unchained.Pptx.Writing;
using P = DocumentFormat.OpenXml.Presentation;
using SdkPresentationDocument = DocumentFormat.OpenXml.Packaging.PresentationDocument;
using TextWriter = Unchained.Pptx.Writing.TextWriter;

namespace Unchained.Pptx.Engine;

/// <summary>
///     Phase 2 (M5b) SDK-backed save. Persists the modelled slide content onto the document's held
///     <see cref="OoxmlEngine" /> package and saves through it, so every OPC part the model does not
///     own (chart styles, embeddings, tags, presProps, etc.) passes through unchanged — fixing the
///     part-dropping the custom writer suffers.
/// </summary>
/// <remarks>
///     Each slide is reconciled against its live XML by shape id: a shape still present keeps its
///     original element (blip relationships, unmodelled attributes, theme inheritance — all verbatim)
///     with only the model-owned sub-elements rewritten (transform, non-visual name/alt/hidden, fill,
///     line, effects, text body); a removed shape's element is dropped; an added shape is generated
///     with the custom <see cref="SlideWriter" />/<see cref="ShapeWriter" />. Fill/line are rewritten
///     only when the shape already carries an explicit element, so inherited theme fills/outlines are
///     never clobbered. Requires a live engine; callers must check <see cref="CanSave" />.
/// </remarks>
internal static class OpenXmlPresentationWriter
{
    private const string DecorativeExtensionUri = "{C183D7F6-B498-43B3-948B-1728B52AA6E4}";
    private static readonly XNamespace DrawingNs = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace PresentationNs = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private static readonly XNamespace RelationshipsNs = OoxmlNamespaces.OfficeDocument;
    private static readonly XNamespace DecorativeNs = "http://schemas.microsoft.com/office/drawing/2017/decorative";

    private static readonly XName[] FillElementNames =
    [
        DrawingNs + "noFill", DrawingNs + "solidFill", DrawingNs + "gradFill",
        DrawingNs + "blipFill", DrawingNs + "pattFill", DrawingNs + "grpFill"
    ];

    /// <summary>Whether <paramref name="document" /> can be saved through the SDK engine.</summary>
    public static bool CanSave(PresentationDocument document) => document.Engine is not null;

    /// <summary>
    ///     Persists modelled content onto the held SDK package and returns the saved bytes.
    ///     Throws when the document has no attached engine.
    /// </summary>
    public static byte[] Save(PresentationDocument document)
    {
        var engine = document.Engine
                     ?? throw new InvalidOperationException(
                         "SDK-backed save requires a document loaded via the OpenXML engine path."
                     );

        var sdkDoc = (SdkPresentationDocument)engine.Package;
        var presPart = sdkDoc.PresentationPart
                       ?? throw new PptxException("The held package has no presentation part.");

        // A presentation with no slide id list has no slides to reconcile (e.g. a template with only
        // masters/layouts) — leave it untouched.
        if (presPart.Presentation?.SlideIdList is not { } slideIdList)
            return engine.Save();

        ReconcileSlides(presPart, slideIdList, document.Slides);
        presPart.Presentation.Save();

        return engine.Save();
    }

    // Matches model slides to the package's slide parts by slide id: a kept slide is patched in
    // place, a removed slide's part is deleted, an added slide gets a new part (reusing an existing
    // layout). The SlideIdList is then rebuilt in model order — giving add/delete/reorder.
    private static void ReconcileSlides(OpenXmlPartContainer presPart, OpenXmlElement slideIdList, SlideCollection modelSlides)
    {
        var existing = new Dictionary<uint, (SlidePart Part, P.SlideId SlideId)>();
        foreach (var slideId in slideIdList.Elements<P.SlideId>())
        {
            var id = slideId.Id?.Value ?? 0;
            var rId = slideId.RelationshipId?.Value;
            if (id != 0 && rId is not null && presPart.GetPartById(rId) is SlidePart part)
                existing[id] = (part, slideId);
        }

        // Any existing layout works as the template for a newly-created slide's required layout link.
        var templateLayout = existing.Values
            .Select(static v => v.Part.SlideLayoutPart)
            .FirstOrDefault(static l => l is not null);

        var ordered = new List<P.SlideId>();
        var kept = new HashSet<uint>();
        foreach (var modelSlide in modelSlides)
        {
            if (modelSlide.SlideId != 0 && existing.TryGetValue(modelSlide.SlideId, out var match))
            {
                PatchSlidePart(match.Part, modelSlide);
                ordered.Add(match.SlideId);
                kept.Add(modelSlide.SlideId);
                continue;
            }

            var newPart = CreateSlidePart(presPart, templateLayout, modelSlide);
            ordered.Add(
                new P.SlideId
                {
                    Id = modelSlide.SlideId != 0 ? modelSlide.SlideId : NextSlideId(existing.Keys, ordered),
                    RelationshipId = presPart.GetIdOfPart(newPart)
                }
            );
        }

        foreach (var (id, entry) in existing)
        {
            if (!kept.Contains(id))
                presPart.DeletePart(entry.Part);
        }

        // Rebuild the list in model order (detaches then re-attaches the kept ids, appends new ones).
        slideIdList.RemoveAllChildren<P.SlideId>();
        foreach (var slideId in ordered)
            slideIdList.AppendChild(slideId);
    }

    private static uint NextSlideId(IEnumerable<uint> existingIds, IEnumerable<P.SlideId> pending)
    {
        var max = existingIds.Aggregate(255u, Math.Max); // OOXML minimum usable slide id is 256
        max = pending.Aggregate(max, static (current, slideId) => Math.Max(current, slideId.Id?.Value ?? 0));
        return max + 1;
    }

    private static SlidePart CreateSlidePart(OpenXmlPartContainer presPart, SlideLayoutPart? layout, Slide modelSlide)
    {
        var slidePart = presPart.AddNewPart<SlidePart>();
        if (layout is not null)
            slidePart.AddPart(layout);

        slidePart.Slide = new P.Slide(SlideWriter.Write(modelSlide).ToString(SaveOptions.DisableFormatting));
        slidePart.Slide.Save();
        return slidePart;
    }

    // Patches a single slide part in place: shapes, transition, and background — preserving all other
    // slide XML (timing/animations, colour-map overrides, unmodelled content) verbatim.
    private static void PatchSlidePart(SlidePart part, Slide modelSlide)
    {
        var slideXml = XElement.Parse(part.Slide?.OuterXml ?? string.Empty);
        var spTree = slideXml.Element(PresentationNs + "cSld")?.Element(PresentationNs + "spTree");

        if (spTree is null)
        {
            part.Slide = new P.Slide(SlideWriter.Write(modelSlide).ToString(SaveOptions.DisableFormatting));
            part.Slide.Save();
            return;
        }

        ReconcileShapes(part, spTree, modelSlide.Shapes);
        PatchTransition(slideXml, modelSlide.Transition);
        PatchBackground(slideXml, modelSlide.Background);
        part.Slide = new P.Slide(slideXml.ToString(SaveOptions.DisableFormatting));
        part.Slide.Save();
    }

    // Writes the transition only when the model carries one (TransitionWriter returns null otherwise),
    // so a slide whose transition wasn't edited keeps its original element verbatim.
    private static void PatchTransition(XContainer slideXml, SlideTransition transition)
    {
        var generated = TransitionWriter.Write(transition);
        if (generated is null)
            return;

        var existing = slideXml.Element(PresentationNs + "transition");
        if (existing is not null)
        {
            existing.ReplaceWith(generated);
            return;
        }

        // In CT_Slide order, transition follows cSld/clrMapOvr and precedes timing.
        var anchor = slideXml.Element(PresentationNs + "clrMapOvr") ?? slideXml.Element(PresentationNs + "cSld");
        if (anchor is not null)
            anchor.AddAfterSelf(generated);
        else
            slideXml.AddFirst(generated);
    }

    // Writes an explicit slide background only when the model carries a fill, so slides that inherit
    // their background from the layout/master are left untouched.
    private static void PatchBackground(XContainer slideXml, SlideBackground background)
    {
        if (background.Fill.Type == FillType.None)
            return;

        var cSld = slideXml.Element(PresentationNs + "cSld");
        if (cSld is null)
            return;

        var bgProperties = new XElement(PresentationNs + "bgPr");
        FillWriter.Write(bgProperties, background.Fill);
        bgProperties.Add(new XElement(DrawingNs + "effectLst"));
        var bg = new XElement(PresentationNs + "bg", bgProperties);

        var existing = cSld.Element(PresentationNs + "bg");
        if (existing is not null)
            existing.ReplaceWith(bg);
        else
            cSld.AddFirst(bg); // bg is the first child of cSld
    }

    // Matches live shape elements to model shapes by id: kept shapes are patched in place, removed
    // ones are dropped, added ones are generated. The result is re-inserted in model order after the
    // tree's own non-visual/group-props header.
    private static void ReconcileShapes(SlidePart part, XContainer spTree, ShapeCollection modelShapes)
    {
        var existing = spTree.Elements().Where(IsShapeElement).ToList();

        var byId = new Dictionary<uint, XElement>();
        foreach (var element in existing)
        {
            var id = ShapeIdOf(element);
            if (id != 0)
                byId.TryAdd(id, element);
        }

        var ordered = new List<XElement>();
        foreach (var model in modelShapes)
        {
            if (model.ShapeId != 0 && byId.TryGetValue(model.ShapeId, out var matched))
            {
                PatchShape(part, matched, model);
                ordered.Add(matched);
                continue;
            }

            // A newly-added picture's image isn't in this slide part yet (its model relationship id,
            // if any, comes from the model's media store, not the SDK package) — add it and wire the
            // relationship id the generated blip will reference.
            if (model is PictureShape { Image: { } image } && !HasImagePart(part, image.RelationshipId))
                EnsureImagePart(part, image);

            if (ShapeWriter.Write(model) is not { } generated)
                continue;

            if (model is PictureShape)
                NormalizeBlipEmbed(generated);

            ordered.Add(generated);
        }

        foreach (var element in existing)
            element.Remove();

        var anchor = spTree.Elements().LastOrDefault(static e => e.Name.LocalName is "nvGrpSpPr" or "grpSpPr");
        foreach (var element in ordered)
        {
            if (anchor is not null)
            {
                anchor.AddAfterSelf(element);
                anchor = element;
            }
            else
                spTree.Add(element);
        }
    }

    private static bool IsShapeElement(XElement element) =>
        element.Name.LocalName is "sp" or "pic" or "graphicFrame" or "grpSp" or "cxnSp";

    private static uint ShapeIdOf(XContainer shapeElement)
    {
        var nv = shapeElement.Elements().FirstOrDefault(static e => e.Name.LocalName.StartsWith("nv", StringComparison.Ordinal));
        var idValue = nv?.Element(PresentationNs + PmlNames.CommonNonVisualProperties.LocalName)?.Attribute("id")?.Value;
        return uint.TryParse(idValue, out var id) ? id : 0u;
    }

    private static void PatchShape(OpenXmlPartContainer part, XElement element, Shape model)
    {
        PatchNonVisual(part, element, model);

        switch (element.Name.LocalName)
        {
            case "sp":
            {
                var spPr = element.Element(PresentationNs + "spPr");
                if (spPr is not null)
                {
                    PatchTransform(spPr, DrawingNs + "xfrm", model);
                    PatchFill(spPr, model.Fill);
                    PatchLine(spPr, model.Line);
                    PatchEffects(spPr, model.Effects);
                }

                if (model is AutoShape auto)
                    PatchTextBody(element, auto);
            }
            break;
            case "cxnSp":
            {
                var spPr = element.Element(PresentationNs + "spPr");
                if (spPr is not null)
                {
                    PatchTransform(spPr, DrawingNs + "xfrm", model);
                    PatchLine(spPr, model.Line);
                    PatchEffects(spPr, model.Effects);
                }
            }
            break;
            case "pic":
            {
                var spPr = element.Element(PresentationNs + "spPr");
                if (spPr is not null)
                {
                    PatchTransform(spPr, DrawingNs + "xfrm", model);
                    PatchEffects(spPr, model.Effects);
                }
            }
            break;
            case "graphicFrame":
                PatchTransform(element, PresentationNs + "xfrm", model);
            break;
            case "grpSp":
            {
                var grpSpPr = element.Element(PresentationNs + "grpSpPr");
                if (grpSpPr is not null)
                    PatchTransform(grpSpPr, DrawingNs + "xfrm", model);
            }
            break;
        }
    }

    // Name, alt-text (descr), alt-title, hidden, hyperlink, and the decorative flag all live on the
    // shape's cNvPr.
    private static void PatchNonVisual(OpenXmlPartContainer part, XContainer shapeElement, Shape model)
    {
        var nv = shapeElement.Elements().FirstOrDefault(static e => e.Name.LocalName.StartsWith("nv", StringComparison.Ordinal));
        var cNvPr = nv?.Element(PresentationNs + PmlNames.CommonNonVisualProperties.LocalName);
        if (cNvPr is null)
            return;

        cNvPr.SetAttributeValue("name", model.Name);
        cNvPr.SetAttributeValue("descr", string.IsNullOrEmpty(model.AltText) ? null : model.AltText);
        cNvPr.SetAttributeValue("title", string.IsNullOrEmpty(model.AltTextTitle) ? null : model.AltTextTitle);
        cNvPr.SetAttributeValue("hidden", model.IsHidden ? "1" : null);

        PatchHyperlink(part, cNvPr, model);
        PatchDecorative(cNvPr, model);
    }

    // Sets/clears the click hyperlink, adding an external OPC relationship for the URL when needed.
    private static void PatchHyperlink(OpenXmlPartContainer part, XContainer cNvPr, Shape model)
    {
        var existing = cNvPr.Element(DrawingNs + "hlinkClick");
        var url = model.ClickAction?.Url;

        if (string.IsNullOrEmpty(url))
        {
            existing?.Remove();
            return;
        }

        var relationshipId = EnsureHyperlinkRelationship(part, model.ClickAction!, url);

        var hlink = existing;
        if (hlink is null)
        {
            hlink = new XElement(DrawingNs + "hlinkClick");
            // hlinkClick precedes any extLst in cNvPr's child order.
            var extLst = cNvPr.Element(DrawingNs + DmlNames.Extended.LocalName);
            if (extLst is not null)
                extLst.AddBeforeSelf(hlink);
            else
                cNvPr.Add(hlink);
        }

        hlink.SetAttributeValue(RelationshipsNs + "id", relationshipId);
    }

    private static string EnsureHyperlinkRelationship(OpenXmlPartContainer part, HyperlinkAction action, string url)
    {
        if (!string.IsNullOrEmpty(action.RelationshipId)
            && part.HyperlinkRelationships.Any(r => r.Id == action.RelationshipId))
            return action.RelationshipId;

        var relationship = part.AddHyperlinkRelationship(new Uri(url, UriKind.RelativeOrAbsolute), true);
        action.RelationshipId = relationship.Id;
        return relationship.Id;
    }

    // Adds/removes the Office 2017 "decorative" extension in cNvPr's extLst.
    private static void PatchDecorative(XContainer cNvPr, Shape model)
    {
        var extLst = cNvPr.Element(DrawingNs + DmlNames.Extended.LocalName);
        var decorativeExt = extLst?.Elements(DrawingNs + "ext")
            .FirstOrDefault(static e => (string?)e.Attribute("uri") == DecorativeExtensionUri);

        if (model.IsDecorative)
        {
            if (decorativeExt is not null)
                return;

            if (extLst is null)
            {
                extLst = new XElement(DrawingNs + "extLst");
                cNvPr.Add(extLst);
            }

            extLst.Add(
                new XElement(
                    DrawingNs + "ext",
                    new XAttribute("uri", DecorativeExtensionUri),
                    new XElement(DecorativeNs + "decorative", new XAttribute("val", "1"))
                )
            );

            return;
        }

        decorativeExt?.Remove();
        if (extLst is not null && !extLst.HasElements)
            extLst.Remove();
    }

    private static bool HasImagePart(OpenXmlPartContainer part, string relationshipId) =>
        !string.IsNullOrEmpty(relationshipId)
        && part.Parts.Any(p => p.RelationshipId == relationshipId && p.OpenXmlPart is ImagePart);

    private static void EnsureImagePart(SlidePart part, EmbeddedImage image)
    {
        var imagePart = part.AddImagePart(image.ContentType);
        using var stream = new MemoryStream(image.Data.ToArray());
        imagePart.FeedData(stream);
        image.RelationshipId = part.GetIdOfPart(imagePart);
    }

    // ShapeWriter emits the blip relationship as r:id, but a DrawingML <a:blip> references its image
    // through r:embed — rewrite it so the added picture resolves in the SDK package (and PowerPoint).
    private static void NormalizeBlipEmbed(XContainer pictureElement)
    {
        var blip = pictureElement.Element(PresentationNs + "blipFill")?.Element(DrawingNs + "blip");
        var idAttribute = blip?.Attribute(RelationshipsNs + "id");
        if (idAttribute is null)
            return;

        blip!.SetAttributeValue(RelationshipsNs + "embed", idAttribute.Value);
        idAttribute.Remove();
    }

    // Sets the shape's offset/extents (and, for an a:xfrm, rotation/flips) from the model. A group's
    // child offsets (chOff/chExt) are left untouched.
    private static void PatchTransform(XContainer container, XName transformName, Shape model)
    {
        var transform = container.Element(transformName);
        if (transform is null)
        {
            transform = new XElement(transformName);
            container.AddFirst(transform);
        }

        // Rotation and flips live on the a:xfrm only — a graphicFrame's p:xfrm has no such attributes.
        if (transformName == DrawingNs + "xfrm")
        {
            transform.SetAttributeValue("rot", model.RotationDegrees != 0 ? (int)Math.Round(model.RotationDegrees * 60000) : null);
            transform.SetAttributeValue("flipH", model.FlipHorizontal ? "1" : null);
            transform.SetAttributeValue("flipV", model.FlipVertical ? "1" : null);
        }

        var offset = transform.Element(DrawingNs + "off");
        if (offset is null)
        {
            offset = new XElement(DrawingNs + "off");
            transform.AddFirst(offset);
        }

        offset.SetAttributeValue("x", model.X.Value);
        offset.SetAttributeValue("y", model.Y.Value);

        var extents = transform.Element(DrawingNs + "ext");
        if (extents is null)
        {
            extents = new XElement(DrawingNs + "ext");
            offset.AddAfterSelf(extents);
        }

        extents.SetAttributeValue("cx", model.Width.Value);
        extents.SetAttributeValue("cy", model.Height.Value);
    }

    // Writes the model's fill (solid/none/gradient/pattern). An existing explicit fill is replaced;
    // when a shape has none it inherits from the theme, so a fill is inserted only when the model
    // actually carries one — leaving genuine inheritance untouched. Picture/group fills reference
    // package relationships, so their original element is preserved in place.
    private static void PatchFill(XContainer spPr, FillFormat fill)
    {
        if (fill.Type is FillType.Picture or FillType.Group)
            return;

        var existing = spPr.Elements().FirstOrDefault(static e => FillElementNames.Contains(e.Name));
        if (existing is null && fill.Type == FillType.None)
            return;

        var holder = new XElement("holder");
        FillWriter.Write(holder, fill);
        if (holder.Elements().FirstOrDefault() is not { } replacement)
            return;

        if (existing is not null)
            existing.ReplaceWith(replacement);
        else
            InsertFill(spPr, replacement);
    }

    // Writes the model's outline. An existing outline is replaced; when a shape has none it inherits,
    // so an outline is inserted only when the model carries an explicit one.
    private static void PatchLine(XContainer spPr, LineFormat line)
    {
        var existing = spPr.Element(DrawingNs + "ln");
        if (existing is null && !HasExplicitLine(line))
            return;

        var holder = new XElement("holder");
        LineWriter.Write(holder, line);
        if (holder.Element(DrawingNs + "ln") is not { } replacement)
            return;

        if (existing is not null)
            existing.ReplaceWith(replacement);
        else
            InsertLine(spPr, replacement);
    }

    private static bool HasExplicitLine(LineFormat line) =>
        line.Fill.Type != FillType.None || line.WidthPoints.HasValue;

    // Fill follows the geometry (and precedes the outline) in the spPr schema order.
    private static void InsertFill(XContainer spPr, XElement fill)
    {
        var anchor = spPr.Element(DrawingNs + "prstGeom")
                     ?? spPr.Element(DrawingNs + "custGeom")
                     ?? spPr.Element(DrawingNs + "xfrm");
        if (anchor is not null)
            anchor.AddAfterSelf(fill);
        else
            spPr.AddFirst(fill);
    }

    // Outline follows the fill (or the geometry) in the spPr schema order.
    private static void InsertLine(XContainer spPr, XElement line)
    {
        var anchor = spPr.Elements().LastOrDefault(static e => FillElementNames.Contains(e.Name))
                     ?? spPr.Element(DrawingNs + "prstGeom")
                     ?? spPr.Element(DrawingNs + "custGeom")
                     ?? spPr.Element(DrawingNs + "xfrm");
        if (anchor is not null)
            anchor.AddAfterSelf(line);
        else
            spPr.Add(line);
    }

    // Effects are read into the model on the SDK load path, so re-emitting is faithful: replace or
    // insert when the model has effects, remove when it has none.
    private static void PatchEffects(XContainer spPr, EffectFormat effects)
    {
        var generated = EffectWriter.Write(effects);
        var existing = spPr.Element(DrawingNs + "effectLst");

        if (generated is null)
        {
            existing?.Remove();
            return;
        }

        if (existing is not null)
        {
            existing.ReplaceWith(generated);
            return;
        }

        // effectLst follows the outline in the spPr schema order.
        var anchor = spPr.Element(DrawingNs + "ln")
                     ?? spPr.Elements().LastOrDefault(static e => FillElementNames.Contains(e.Name))
                     ?? spPr.Element(DrawingNs + "prstGeom")
                     ?? spPr.Element(DrawingNs + "custGeom")
                     ?? spPr.Element(DrawingNs + "xfrm");
        if (anchor is not null)
            anchor.AddAfterSelf(generated);
        else
            spPr.Add(generated);
    }

    // Replaces an existing text body with the model's — never adds one to a shape that has none.
    private static void PatchTextBody(XContainer shapeElement, AutoShape model)
    {
        var existing = shapeElement.Element(PresentationNs + "txBody");
        existing?.ReplaceWith(TextWriter.WriteAsShape(model.TextFrame));
    }
}
