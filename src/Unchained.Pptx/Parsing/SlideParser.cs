using Unchained.Pptx.Core.Xml;
using System.Xml.Linq;
using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Media;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Parsing;

/// <summary>
/// Parses a single slide OPC part into a <see cref="Slide"/>.
/// </summary>
internal sealed class SlideParser
{
    private readonly OpcPackage _package;
    private readonly MediaStore _mediaStore;
    private readonly IReadOnlyList<MasterSlide> _masters;

    public SlideParser(
        OpcPackage package,
        MediaStore mediaStore,
        IReadOnlyList<MasterSlide> masters)
    {
        _package = package;
        _mediaStore = mediaStore;
        _masters = masters;
    }

    /// <summary>
    /// Parses the slide at <paramref name="partUri"/> and returns a <see cref="Slide"/>.
    /// </summary>
    public Slide Parse(string partUri, string relationshipId, uint slideId)
    {
        var part = _package.TryGetPart(partUri);
        if (part == null)
        {
            return new Slide
            {
                PartUri = partUri,
                RelationshipId = relationshipId,
                SlideId = slideId,
                Layout = GetFallbackLayout()
            };
        }

        var doc = OoXmlHelper.ParseXml(part.Data);
        var root = doc.Root;

        var slide = new Slide
        {
            PartUri = partUri,
            RelationshipId = relationshipId,
            SlideId = slideId,
            Layout = GetFallbackLayout()
        };

        if (root == null) return slide;

        // Visibility (show="0" means hidden)
        var show = root.GetAttrBool(PmlNames.AttributeShow);
        if (show.HasValue)
            slide.IsHidden = !show.Value;

        // Resolve layout relationship
        var layoutRel = part.FindRelationship(PmlNames.RelTypeSlideLayout);
        if (layoutRel != null)
        {
            var layoutUri = part.ResolveUri(layoutRel.TargetUri);
            var resolvedLayout = FindLayout(layoutUri);
            if (resolvedLayout != null)
                slide.Layout = resolvedLayout;
        }

        // Common slide data — shapes
        var cSld = root.Element(PmlNames.CommonSlideData);
        slide.Name = cSld?.GetAttr(PmlNames.AttributeName, string.Empty) ?? string.Empty;

        var spTree = cSld?.Element(PmlNames.ShapeTree);
        if (spTree != null)
        {
            var shapeParser = new ShapeParser(_package, _mediaStore);
            shapeParser.ParseTree(spTree, slide.Shapes);
        }

        // Preserve round-trip blobs
        slide.TransitionElement = root.Element(PmlNames.Transition);
        slide.TimingElement = root.Element(PmlNames.Timing);
        slide.ColorMapOverrideElement = root.Element(PmlNames.ColorMapOverride);

        // Resolve embedded images (second pass)
        ResolveImages(part, slide);

        // Resolve chart parts (second pass)
        ResolveCharts(part, slide);

        // Notes slide
        var notesRel = part.FindRelationship(PmlNames.RelTypeNotesSlide);
        if (notesRel != null)
        {
            var notesUri = part.ResolveUri(notesRel.TargetUri);
            var notesPart = _package.TryGetPart(notesUri);
            if (notesPart != null)
                slide.Notes.RawElement = OoXmlHelper.ParseXml(notesPart.Data).Root;
        }

        return slide;
    }

    // ── Image resolution ──────────────────────────────────────────────────────

    private void ResolveImages(OpcPart slidePart, Slide slide)
    {
        // Walk all PictureShape instances and resolve their images from relationships.
        foreach (var shape in slide.Shapes.OfType<Shapes.PictureShape>())
        {
            if (shape.Image != null) continue;
            var rawEl = shape.RawElement;
            if (rawEl == null) continue;

            var blipFill = rawEl.Element(PmlNames.BlipFill);
            var blip = blipFill?.Element(DmlNames.Blip);
            var rId = (string?)blip?.Attribute(PmlNames.RelationshipId);
            if (rId == null) continue;

            shape.Image = ResolveImageRelationship(slidePart, rId);
        }
    }

    private EmbeddedImage? ResolveImageRelationship(OpcPart sourcePart, string relationshipId)
    {
        var rel = sourcePart.Relationships.FirstOrDefault(r =>
            r.Id.Equals(relationshipId, StringComparison.Ordinal));

        if (rel == null) return null;

        var imageUri = sourcePart.ResolveUri(rel.TargetUri);
        var imagePart = _package.TryGetPart(imageUri);
        if (imagePart == null) return null;

        // Check if already in the media store (dedup by URI)
        var existing = _mediaStore.Images.FirstOrDefault(img =>
            img.PartUri.Equals(imageUri, StringComparison.OrdinalIgnoreCase));

        if (existing != null) return existing;

        var image = new EmbeddedImage(imagePart.ContentType, imagePart.Data)
        {
            PartUri = imageUri,
            RelationshipId = relationshipId
        };
        return _mediaStore.AddImage(image);
    }

    // ── Chart resolution ──────────────────────────────────────────────────────

    private void ResolveCharts(OpcPart slidePart, Slide slide)
    {
        foreach (var shape in slide.Shapes.OfType<ChartShape>())
        {
            if (!string.IsNullOrEmpty(shape.RelationshipId))
                LoadChartPart(slidePart, shape);
        }
    }

    private void LoadChartPart(OpcPart slidePart, ChartShape shape)
    {
        var rel = slidePart.Relationships.FirstOrDefault(r =>
            r.Id.Equals(shape.RelationshipId, StringComparison.Ordinal));
        if (rel == null) return;

        var chartUri = slidePart.ResolveUri(rel.TargetUri);
        shape.PartUri = chartUri;

        var chartPart = _package.TryGetPart(chartUri);
        if (chartPart == null) return;

        // Preserve raw bytes for lossless round-trip
        shape.ChartPartData = chartPart.Data;

        // Parse into ChartModel so callers can inspect and modify chart data
        var chartDoc = OoXmlHelper.ParseXml(chartPart.Data);
        if (chartDoc.Root != null)
            ChartParser.Parse(chartDoc.Root, shape.Chart);
    }

    // ── Layout resolution ─────────────────────────────────────────────────────

    private SlideLayout? FindLayout(string partUri) =>
        _masters
            .SelectMany(static m => m.Layouts)
            .FirstOrDefault(l =>
                l.PartUri.Equals(partUri, StringComparison.OrdinalIgnoreCase));

    private SlideLayout GetFallbackLayout()
    {
        var layout = _masters.FirstOrDefault()?.Layouts.FirstOrDefault();
        if (layout != null) return layout;

        // Minimal fallback layout used when no masters have been parsed yet
        var fallback = new SlideLayout { Name = "Default" };
        var fallbackMaster = new MasterSlide { Name = "Default" };
        fallback.Master = fallbackMaster;
        return fallback;
    }
}
