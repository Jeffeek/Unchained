using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Media;
using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Text;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Media;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Parsing;

/// <summary>
///     Parses a single slide OPC part into a <see cref="Slide" />.
/// </summary>
internal sealed class SlideParser(
    OpcPackage package,
    MediaStore mediaStore,
    IEnumerable<MasterSlide> masters,
    CommentAuthorCollection commentAuthors
)
{
    /// <summary>
    ///     Parses the slide at <paramref name="partUri" /> and returns a <see cref="Slide" />.
    /// </summary>
    public Slide Parse(string partUri, string relationshipId, uint slideId)
    {
        var part = package.TryGetPart(partUri);
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
            var shapeParser = new ShapeParser();
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
            var notesPart = package.TryGetPart(notesUri);
            if (notesPart != null)
            {
                var notesDoc = OoXmlHelper.ParseXml(notesPart.Data);
                if (notesDoc.Root != null)
                    NotesParser.Parse(notesDoc.Root, slide.Notes);
            }
        }

        // Comments (M7)
        var commentsRel = part.FindRelationship(PmlNames.RelTypeComments);
        if (commentsRel == null) return slide;

        var commentsUri = part.ResolveUri(commentsRel.TargetUri);
        var commentsPart = package.TryGetPart(commentsUri);
        if (commentsPart == null) return slide;

        var cmDoc = OoXmlHelper.ParseXml(commentsPart.Data);
        if (cmDoc.Root != null)
            CommentParser.Parse(cmDoc.Root, slide, commentAuthors);

        return slide;
    }

    // ── Image resolution ──────────────────────────────────────────────────────

    private void ResolveImages(OpcPart slidePart, Slide slide) =>
        // Walk all shapes and resolve picture fills and PictureShape images.
        ResolveImagesInCollection(slidePart, slide.Shapes);

    private void ResolveImagesInCollection(OpcPart slidePart, ShapeCollection shapes)
    {
        foreach (var shape in shapes)
        {
            // Resolve PictureShape blipFill (p:pic/p:blipFill/a:blip r:embed)
            if (shape is PictureShape { Image: null } pictureShape)
            {
                var rawEl = pictureShape.RawElement;
                if (rawEl != null)
                {
                    var blipFill = rawEl.Element(PmlNames.BlipFill);
                    var blip = blipFill?.Element(DmlNames.Blip);
                    var rId = (string?)blip?.Attribute(PmlNames.RelationshipEmbed)
                              ?? (string?)blip?.Attribute(PmlNames.RelationshipId);
                    if (rId != null)
                        pictureShape.Image = ResolveImageRelationship(slidePart, rId);
                }
            }

            // Resolve spPr blipFill on any shape type (e.g. AutoShape background images)
            if (shape.Fill.Type == FillType.Picture &&
                shape.Fill.Picture?.Image == null &&
                shape.Fill.Picture?.RelationshipId != null)
            {
                shape.Fill.Picture.Image =
                    ResolveImageRelationship(slidePart, shape.Fill.Picture.RelationshipId);
            }

            // Recurse into groups
            if (shape is GroupShape group)
                ResolveImagesInCollection(slidePart, group.Children);
        }
    }

    private EmbeddedImage? ResolveImageRelationship(OpcPart sourcePart, string relationshipId)
    {
        var rel = sourcePart.Relationships.FirstOrDefault(r =>
            r.Id.Equals(relationshipId, StringComparison.Ordinal)
        );

        if (rel == null) return null;

        var imageUri = sourcePart.ResolveUri(rel.TargetUri);
        var imagePart = package.TryGetPart(imageUri);
        if (imagePart == null) return null;

        // Check if already in the media store (dedup by URI)
        var existing = mediaStore.Images.FirstOrDefault(img =>
            img.PartUri.Equals(imageUri, StringComparison.OrdinalIgnoreCase)
        );

        if (existing != null) return existing;

        var image = new EmbeddedImage(imagePart.ContentType, imagePart.Data)
        {
            PartUri = imageUri,
            RelationshipId = relationshipId
        };
        return mediaStore.AddImage(image);
    }

    // ── Chart resolution ──────────────────────────────────────────────────────

    private void ResolveCharts(OpcPart slidePart, Slide slide)
    {
        foreach (var shape in slide.Shapes.OfType<ChartShape>().Where(static shape => !string.IsNullOrEmpty(shape.RelationshipId)))
            LoadChartPart(slidePart, shape);
    }

    private void LoadChartPart(OpcPart slidePart, ChartShape shape)
    {
        var rel = slidePart.Relationships.FirstOrDefault(r =>
            r.Id.Equals(shape.RelationshipId, StringComparison.Ordinal)
        );
        if (rel == null) return;

        var chartUri = slidePart.ResolveUri(rel.TargetUri);
        shape.PartUri = chartUri;

        var chartPart = package.TryGetPart(chartUri);
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
        foreach (var shape in slide.Shapes.OfType<SmartArtShape>())
            LoadSmartArtParts(slidePart, shape);
    }

    private void LoadSmartArtParts(OpcPart slidePart, SmartArtShape shape)
    {
        // Data part (r:dm) — also carries the node text model and a reference to the drawing part.
        var dataPart = ResolveDiagramPart(
            slidePart,
            shape.DataRelationshipId,
            uri => shape.DataPartUri = uri
        );
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
                    var drawingPart = ResolveDiagramPart(
                        slidePart,
                        drawingRelId,
                        uri => shape.DrawingPartUri = uri
                    );
                    if (drawingPart != null)
                        shape.DrawingPartData = drawingPart.Data;
                }
            }
        }

        var layoutPart = ResolveDiagramPart(
            slidePart,
            shape.LayoutRelationshipId,
            uri => shape.LayoutPartUri = uri
        );
        if (layoutPart != null) shape.LayoutPartData = layoutPart.Data;

        var quickStylePart = ResolveDiagramPart(
            slidePart,
            shape.QuickStyleRelationshipId,
            uri => shape.QuickStylePartUri = uri
        );
        if (quickStylePart != null) shape.QuickStylePartData = quickStylePart.Data;

        var colorsPart = ResolveDiagramPart(
            slidePart,
            shape.ColorsRelationshipId,
            uri => shape.ColorsPartUri = uri
        );
        if (colorsPart != null) shape.ColorsPartData = colorsPart.Data;
    }

    private OpcPart? ResolveDiagramPart(OpcPart sourcePart, string relationshipId, Action<string> setUri)
    {
        if (string.IsNullOrEmpty(relationshipId)) return null;

        var rel = sourcePart.Relationships.FirstOrDefault(r =>
            r.Id.Equals(relationshipId, StringComparison.Ordinal)
        );
        if (rel == null) return null;

        var uri = sourcePart.ResolveUri(rel.TargetUri);
        setUri(uri);
        return package.TryGetPart(uri);
    }

    // ── Hyperlink resolution ─────────────────────────────────────────────────────

    private static void ResolveHyperlinks(OpcPart slidePart, Slide slide)
    {
        foreach (var shape in EnumerateAllShapes(slide.Shapes)
                     .Where(static shape => shape.ClickAction is not null))
            ResolveHyperlinkTarget(slidePart, shape.ClickAction!);

        // Run-level hyperlinks across every text frame on the slide.
        foreach (var run in from frame in ShapeTextWalker.EnumerateTextFrames(slide.Shapes)
                            from paragraph in frame.Paragraphs
                            from run in paragraph.Runs
                            where run.Format.Hyperlink is not null
                            select run)
            ResolveRunHyperlinkTarget(slidePart, run.Format.Hyperlink!);
    }

    private static IEnumerable<Shape> EnumerateAllShapes(IEnumerable<Shape> shapes)
    {
        foreach (var shape in shapes)
        {
            yield return shape;

            if (shape is not GroupShape group) continue;

            foreach (var child in EnumerateAllShapes(group.Children))
                yield return child;
        }
    }

    private static void ResolveHyperlinkTarget(OpcPart slidePart, HyperlinkAction action)
    {
        if (string.IsNullOrEmpty(action.RelationshipId)) return; // e.g. action-only links

        var rel = slidePart.Relationships.FirstOrDefault(r =>
            r.Id.Equals(action.RelationshipId, StringComparison.Ordinal)
        );
        if (rel == null) return;

        if (rel.IsExternal)
            action.Url = rel.TargetUri;
        else
        {
            // Internal jump to another slide; capture the part URI now and turn it into a
            // 1-based slide number once all slides are parsed (see PresentationParser).
            action.TargetSlidePartUri = slidePart.ResolveUri(rel.TargetUri);
        }
    }

    private static void ResolveRunHyperlinkTarget(OpcPart slidePart, RunHyperlink link)
    {
        if (string.IsNullOrEmpty(link.RelationshipId)) return;

        var rel = slidePart.Relationships.FirstOrDefault(r =>
            r.Id.Equals(link.RelationshipId, StringComparison.Ordinal)
        );
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
        var layoutPlaceholders = layout.Shapes is { } ls ? CollectPlaceholders(ls) : [];
        var masterPlaceholders = layout.Master.Shapes is { } ms ? CollectPlaceholders(ms) : [];

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

    private static List<Shape> CollectPlaceholders(IEnumerable<Shape> shapes) =>
        EnumerateAllShapes(shapes).Where(static s => s.IsPlaceholder).ToList();

    // Matches a slide placeholder to its layout/master definition: prefer an exact index match,
    // then a type match, then (for the common single-body case) a compatible body/content/object.
    private static Shape? MatchPlaceholder(Shape target, IReadOnlyCollection<Shape> candidates)
    {
        if (candidates.Count == 0) return null;

        if (target.PlaceholderIndex is { } idx)
        {
            var byIdx = candidates.FirstOrDefault(c => c.PlaceholderIndex == idx
                                                       && c.Width.Value > 0 && c.Height.Value > 0
            );
            if (byIdx is not null) return byIdx;
        }

        var byType = candidates.FirstOrDefault(c => c.PlaceholderType == target.PlaceholderType
                                                    && c.Width.Value > 0 && c.Height.Value > 0
        );
        if (byType is not null) return byType;

        // Title family and body/content/object family are interchangeable across slide↔layout.
        if (IsTitle(target.PlaceholderType))
        {
            return candidates.FirstOrDefault(static c => IsTitle(c.PlaceholderType)
                                                         && c.Width.Value > 0 && c.Height.Value > 0
            );
        }

#pragma warning disable IDE0046
        if (IsBodyLike(target.PlaceholderType))
#pragma warning restore IDE0046
        {
            return candidates.FirstOrDefault(static c => IsBodyLike(c.PlaceholderType)
                                                         && c.Width.Value > 0 && c.Height.Value > 0
            );
        }

        return null;
    }

    private static bool IsTitle(PlaceholderType t) =>
        t is PlaceholderType.Title or PlaceholderType.CenteredTitle;

    private static bool IsBodyLike(PlaceholderType t) =>
        t is PlaceholderType.Body or PlaceholderType.Content
            or PlaceholderType.Object or PlaceholderType.Subtitle;

    // ── Layout resolution ─────────────────────────────────────────────────────

    private SlideLayout? FindLayout(string partUri) =>
        masters
            .SelectMany(static m => m.Layouts)
            .FirstOrDefault(l =>
                l.PartUri.Equals(partUri, StringComparison.OrdinalIgnoreCase)
            );

    private SlideLayout GetFallbackLayout()
    {
        var layout = masters.FirstOrDefault()?.Layouts.FirstOrDefault();
        if (layout != null) return layout;

        // Minimal fallback layout used when no masters have been parsed yet
        var fallback = new SlideLayout { Name = "Default" };
        var fallbackMaster = new MasterSlide { Name = "Default" };
        fallback.Master = fallbackMaster;
        return fallback;
    }
}
