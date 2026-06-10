using Unchained.Pptx.Core.Xml;
using System.Xml.Linq;
using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Animations;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Media;
using Unchained.Ooxml.Media;
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
    private readonly CommentAuthorCollection _commentAuthors;

    public SlideParser(
        OpcPackage package,
        MediaStore mediaStore,
        IReadOnlyList<MasterSlide> masters,
        CommentAuthorCollection commentAuthors)
    {
        _package = package;
        _mediaStore = mediaStore;
        _masters = masters;
        _commentAuthors = commentAuthors;
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

        // Parse transition and animations (M6)
        var transitionEl = root.Element(PmlNames.Transition);
        if (transitionEl != null)
            TransitionParser.Parse(transitionEl, slide.Transition);

        var timingEl = root.Element(PmlNames.Timing);
        if (timingEl != null)
            AnimationParser.Parse(timingEl, slide.Animations);

        slide.ColorMapOverrideElement = root.Element(PmlNames.ColorMapOverride);

        // Resolve embedded images (second pass)
        ResolveImages(part, slide);

        // Resolve chart parts (second pass)
        ResolveCharts(part, slide);

        // Resolve SmartArt diagram parts (second pass)
        ResolveSmartArt(part, slide);

        // Resolve shape click-hyperlink targets (second pass)
        ResolveHyperlinks(part, slide);

        // Inherit placeholder geometry from the layout/master (second pass). Title/body
        // placeholders on a content slide usually have an empty spPr and take their position
        // and size from the matching placeholder on the layout — without this they are
        // zero-sized and never rendered.
        ResolvePlaceholderGeometry(slide);

        // Notes slide (M7)
        var notesRel = part.FindRelationship(PmlNames.RelTypeNotesSlide);
        if (notesRel != null)
        {
            var notesUri = part.ResolveUri(notesRel.TargetUri);
            var notesPart = _package.TryGetPart(notesUri);
            if (notesPart != null)
            {
                var notesDoc = OoXmlHelper.ParseXml(notesPart.Data);
                if (notesDoc.Root != null)
                    NotesParser.Parse(notesDoc.Root, slide.Notes);
            }
        }

        // Comments (M7)
        var commentsRel = part.FindRelationship(PmlNames.RelTypeComments);
        if (commentsRel != null)
        {
            var commentsUri = part.ResolveUri(commentsRel.TargetUri);
            var commentsPart = _package.TryGetPart(commentsUri);
            if (commentsPart != null)
            {
                var cmDoc = OoXmlHelper.ParseXml(commentsPart.Data);
                if (cmDoc.Root != null)
                    CommentParser.Parse(cmDoc.Root, slide, _commentAuthors);
            }
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
            // The blip references its image via r:embed (not r:id). Fall back to r:id defensively.
            var rId = (string?)blip?.Attribute(PmlNames.RelationshipEmbed)
                      ?? (string?)blip?.Attribute(PmlNames.RelationshipId);
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

    // ── SmartArt resolution ─────────────────────────────────────────────────────

    private void ResolveSmartArt(OpcPart slidePart, Slide slide)
    {
        foreach (var shape in slide.Shapes.OfType<Shapes.SmartArtShape>())
            LoadSmartArtParts(slidePart, shape);
    }

    private void LoadSmartArtParts(OpcPart slidePart, Shapes.SmartArtShape shape)
    {
        // Data part (r:dm) — also carries the node text model and a reference to the drawing part.
        var dataPart = ResolveDiagramPart(slidePart, shape.DataRelationshipId,
            uri => shape.DataPartUri = uri);
        if (dataPart != null)
        {
            shape.DataPartData = dataPart.Data;

            var dataDoc = OoXmlHelper.ParseXml(dataPart.Data);
            var dataModel = dataDoc.Root;
            if (dataModel != null)
            {
                shape.DiagramDataDocument = dataDoc;
                SmartArtParser.Parse(dataModel, shape);

                // The pre-rendered drawing part is referenced via dsp:dataModelExt/@relId.
                // Since the data part carries no relationships of its own, that id resolves
                // against the slide's relationships (where the diagramDrawing rel lives).
                var ext = dataModel.Descendants()
                    .FirstOrDefault(static e => e.Name.LocalName == "dataModelExt");
                var drawingRelId = (string?)ext?.Attribute("relId");
                if (!string.IsNullOrEmpty(drawingRelId))
                {
                    shape.DrawingRelationshipId = drawingRelId;
                    var drawingPart = ResolveDiagramPart(slidePart, drawingRelId,
                        uri => shape.DrawingPartUri = uri);
                    if (drawingPart != null)
                        shape.DrawingPartData = drawingPart.Data;
                }
            }
        }

        var layoutPart = ResolveDiagramPart(slidePart, shape.LayoutRelationshipId,
            uri => shape.LayoutPartUri = uri);
        if (layoutPart != null) shape.LayoutPartData = layoutPart.Data;

        var quickStylePart = ResolveDiagramPart(slidePart, shape.QuickStyleRelationshipId,
            uri => shape.QuickStylePartUri = uri);
        if (quickStylePart != null) shape.QuickStylePartData = quickStylePart.Data;

        var colorsPart = ResolveDiagramPart(slidePart, shape.ColorsRelationshipId,
            uri => shape.ColorsPartUri = uri);
        if (colorsPart != null) shape.ColorsPartData = colorsPart.Data;
    }

    private OpcPart? ResolveDiagramPart(OpcPart sourcePart, string relationshipId, Action<string> setUri)
    {
        if (string.IsNullOrEmpty(relationshipId)) return null;

        var rel = sourcePart.Relationships.FirstOrDefault(r =>
            r.Id.Equals(relationshipId, StringComparison.Ordinal));
        if (rel == null) return null;

        var uri = sourcePart.ResolveUri(rel.TargetUri);
        setUri(uri);
        return _package.TryGetPart(uri);
    }

    // ── Hyperlink resolution ─────────────────────────────────────────────────────

    private void ResolveHyperlinks(OpcPart slidePart, Slide slide)
    {
        foreach (var shape in EnumerateAllShapes(slide.Shapes))
        {
            if (shape.ClickAction is { } action)
                ResolveHyperlinkTarget(slidePart, action);
        }

        // Run-level hyperlinks across every text frame on the slide.
        foreach (var frame in ShapeTextWalker.EnumerateTextFrames(slide.Shapes))
        foreach (var paragraph in frame.Paragraphs)
        foreach (var run in paragraph.Runs)
        {
            if (run.Format.Hyperlink is { } link)
                ResolveRunHyperlinkTarget(slidePart, link);
        }
    }

    private static IEnumerable<Shapes.Shape> EnumerateAllShapes(IEnumerable<Shapes.Shape> shapes)
    {
        foreach (var shape in shapes)
        {
            yield return shape;
            if (shape is Shapes.GroupShape group)
                foreach (var child in EnumerateAllShapes(group.Children))
                    yield return child;
        }
    }

    private void ResolveHyperlinkTarget(OpcPart slidePart, HyperlinkAction action)
    {
        if (string.IsNullOrEmpty(action.RelationshipId)) return; // e.g. action-only links

        var rel = slidePart.Relationships.FirstOrDefault(r =>
            r.Id.Equals(action.RelationshipId, StringComparison.Ordinal));
        if (rel == null) return;

        if (rel.IsExternal)
        {
            action.Url = rel.TargetUri;
        }
        else
        {
            // Internal jump to another slide; capture the part URI now and turn it into a
            // 1-based slide number once all slides are parsed (see PresentationParser).
            action.TargetSlidePartUri = slidePart.ResolveUri(rel.TargetUri);
        }
    }

    private void ResolveRunHyperlinkTarget(OpcPart slidePart, Ooxml.Text.RunHyperlink link)
    {
        if (string.IsNullOrEmpty(link.RelationshipId)) return;

        var rel = slidePart.Relationships.FirstOrDefault(r =>
            r.Id.Equals(link.RelationshipId, StringComparison.Ordinal));
        if (rel == null) return;

        if (rel.IsExternal)
            link.Url = rel.TargetUri;
        else
            link.TargetPartUri = slidePart.ResolveUri(rel.TargetUri);
    }

    // ── Placeholder geometry inheritance ─────────────────────────────────────────

    private static void ResolvePlaceholderGeometry(Slide slide)
    {
        // Gather candidate placeholder definitions from the layout, then the master, so a
        // slide placeholder with no geometry of its own can inherit it.
        var layout = slide.Layout;
        var layoutPlaceholders = layout?.Shapes is { } ls ? CollectPlaceholders(ls) : [];
        var masterPlaceholders = layout?.Master?.Shapes is { } ms ? CollectPlaceholders(ms) : [];

        foreach (var shape in EnumerateAllShapes(slide.Shapes))
        {
            if (!shape.IsPlaceholder) continue;
            if (shape.Width.Value > 0 && shape.Height.Value > 0) continue; // already positioned

            var source = MatchPlaceholder(shape, layoutPlaceholders)
                      ?? MatchPlaceholder(shape, masterPlaceholders);
            if (source is null) continue;

            shape.X = source.X;
            shape.Y = source.Y;
            shape.Width = source.Width;
            shape.Height = source.Height;
        }
    }

    private static List<Shapes.Shape> CollectPlaceholders(IEnumerable<Shapes.Shape> shapes)
    {
        var result = new List<Shapes.Shape>();
        foreach (var s in EnumerateAllShapes(shapes))
            if (s.IsPlaceholder)
                result.Add(s);
        return result;
    }

    // Matches a slide placeholder to its layout/master definition: prefer an exact index match,
    // then a type match, then (for the common single-body case) a compatible body/content/object.
    private static Shapes.Shape? MatchPlaceholder(Shapes.Shape target, List<Shapes.Shape> candidates)
    {
        if (candidates.Count == 0) return null;

        if (target.PlaceholderIndex is { } idx)
        {
            var byIdx = candidates.FirstOrDefault(c => c.PlaceholderIndex == idx
                                                       && c.Width.Value > 0 && c.Height.Value > 0);
            if (byIdx is not null) return byIdx;
        }

        var byType = candidates.FirstOrDefault(c => c.PlaceholderType == target.PlaceholderType
                                                    && c.Width.Value > 0 && c.Height.Value > 0);
        if (byType is not null) return byType;

        // Title family and body/content/object family are interchangeable across slide↔layout.
        if (IsTitle(target.PlaceholderType))
            return candidates.FirstOrDefault(c => IsTitle(c.PlaceholderType)
                                                  && c.Width.Value > 0 && c.Height.Value > 0);
        if (IsBodyLike(target.PlaceholderType))
            return candidates.FirstOrDefault(c => IsBodyLike(c.PlaceholderType)
                                                  && c.Width.Value > 0 && c.Height.Value > 0);
        return null;
    }

    private static bool IsTitle(Shapes.PlaceholderType t) =>
        t is Shapes.PlaceholderType.Title or Shapes.PlaceholderType.CenteredTitle;

    private static bool IsBodyLike(Shapes.PlaceholderType t) =>
        t is Shapes.PlaceholderType.Body or Shapes.PlaceholderType.Content
          or Shapes.PlaceholderType.Object or Shapes.PlaceholderType.Subtitle;

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
