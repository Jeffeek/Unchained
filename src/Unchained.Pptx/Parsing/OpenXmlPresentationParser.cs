using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Packaging;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Engine;
using Unchained.Ooxml.Media;
using Unchained.Ooxml.Text;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Core;
using Unchained.Pptx.Media;
using Unchained.Pptx.Models;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Models.Themes;
using Unchained.Pptx.Security;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using Shape = Unchained.Pptx.Shapes.Shape;

namespace Unchained.Pptx.Parsing;

/// <summary>
///     Phase 2 OpenXML-SDK-backed presentation reader. Produces the same <see cref="ParsedPresentation" />
///     typed model as the legacy custom <see cref="PresentationParser" />, but maps it from the SDK's
///     validated typed DOM (<c>DocumentFormat.OpenXml</c>) instead of hand-rolled XElement walking.
/// </summary>
/// <remarks>
///     This runs in parallel with the custom parser (selected via <c>OpenOptions.UseOpenXmlEngine</c>)
///     and is being grown to full parity. It currently maps: slide size, core document properties,
///     slide structure (real id / name / hidden), and every shape-tree element to its concrete
///     <see cref="Shapes.Shape" /> subtype (autoshape, picture, table, chart, group, connector) with geometry,
///     text, and table cell content. Masters/layouts are represented minimally so the model is
///     internally consistent; fills, themes, and image bytes are later parity steps.
/// </remarks>
internal static class OpenXmlPresentationParser
{
    public static ParsedPresentation Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        // Opened editable and kept open: ownership passes to ParsedPresentation -> the document,
        // so a later SDK-backed save (M5) can mutate this same package in place (parts that the
        // model does not touch pass through unchanged). Disposed here only if parsing fails.
        var engine = OoxmlEngine.Open(data);
        try
        {
            return ParseWithEngine(engine);
        }
        catch
        {
            engine.Dispose();
            throw;
        }
    }

    private static ParsedPresentation ParseWithEngine(OoxmlEngine engine)
    {
        if (engine.Format != OoxmlFormat.Presentation)
            throw new PptxException($"Expected a presentation package but found {engine.Format}.");

        var doc = (PresentationDocument)engine.Package;
        var presPart = doc.PresentationPart
                       ?? throw new PptxException("The presentation package has no presentation part.");

        var masters = new MasterSlideCollection();
        var slides = new SlideCollection();
        var mediaStore = new MediaStore();
        var commentAuthors = new CommentAuthorCollection();
        var sections = new SectionCollection();

        // De-duplicates embedded images across slides by image-part URI, mirroring the custom
        // parser (which keys MediaStore entries by PartUri). One physical image part shared by
        // many pictures yields a single EmbeddedImage.
        var imageCache = new Dictionary<string, EmbeddedImage>(StringComparer.OrdinalIgnoreCase);

        var slideSize = ReadSlideSize(presPart);
        var properties = ReadProperties(doc);

        // Masters, their themes, and layouts. layoutMap lets slides resolve their own layout
        // by SlideLayoutPart so the slide -> layout -> master chain matches the custom parser.
        var layoutMap = new Dictionary<SlideLayoutPart, SlideLayout>();
        foreach (var master in presPart.SlideMasterParts.Select(masterPart => ReadMaster(masterPart, layoutMap)))
            masters.Add(master);

        // Every slide needs a Layout whose Master resolves; fall back to the first available
        // layout (or a synthesized blank) when a slide has no layout part.
        var fallbackLayout = masters.SelectMany(static m => ToList(m.Layouts)).FirstOrDefault()
                             ?? SynthesizeFallback(masters);

        // Slides, in presentation order, linked to their real layout.
        foreach (var (slidePart, slideId) in EnumerateSlidesInOrder(presPart))
        {
            var layout = slidePart.SlideLayoutPart is { } lp && layoutMap.TryGetValue(lp, out var l)
                ? l
                : fallbackLayout;
            var slide = ReadSlide(slidePart, layout, slideId, mediaStore, imageCache);
            slides.AddParsed(slide);
        }

        properties.SlideCount = slides.Count;
        properties.HiddenSlideCount = slides.Count(static s => s.IsHidden);

        // Sections (presentation extLst) and comment authors — reuse the shared parsers.
        if (presPart.Presentation is { } presentation)
        {
            var presXml = XElement.Parse(presentation.OuterXml, LoadOptions.None);
            SectionParser.Parse(presXml, sections);
        }

        if (presPart.CommentAuthorsPart?.CommentAuthorList is not { } authorList)
        {
            return new ParsedPresentation(
                null, // the SDK engine owns the real package; not consumed downstream
                slides,
                masters,
                mediaStore,
                properties,
                new ProtectionInfo(),
                slideSize,
                commentAuthors,
                sections
            )
            {
                // Hand the still-open engine to the parsed result so the document can keep the
                // source package alive for an in-place SDK-backed save (M5b).
                Engine = engine
            };
        }

        var authorsXml = XElement.Parse(authorList.OuterXml, LoadOptions.None);
        CommentAuthorParser.Parse(authorsXml, commentAuthors);

        return new ParsedPresentation(
            null, // the SDK engine owns the real package; not consumed downstream
            slides,
            masters,
            mediaStore,
            properties,
            new ProtectionInfo(),
            slideSize,
            commentAuthors,
            sections
        )
        {
            // Hand the still-open engine to the parsed result so the document can keep the
            // source package alive for an in-place SDK-backed save (M5b).
            Engine = engine
        };
    }

    // ── Slide size ───────────────────────────────────────────────────────────────

    private static SlideSize ReadSlideSize(PresentationPart presPart)
    {
        var sz = presPart.Presentation?.SlideSize;
        return sz?.Cx is null || sz.Cy is null ? SlideSize.Widescreen : new SlideSize(new Emu(sz.Cx!.Value), new Emu(sz.Cy!.Value));
    }

    // ── Masters / layouts / themes ─────────────────────────────────────────────────

    private static MasterSlide ReadMaster(
        SlideMasterPart masterPart,
        IDictionary<SlideLayoutPart, SlideLayout> layoutMap
    )
    {
        var master = new MasterSlide
        {
            Name = "Office Theme",
            PartUri = masterPart.Uri.ToString()
        };

        // Preserve the full master XML so a custom-writer save keeps elements the typed model
        // does not capture (txStyles, clrMap, header/footer placeholders). The writers honour
        // RawElement when present.
        if (masterPart.SlideMaster is { } sdkMaster)
            master.RawElement = XElement.Parse(sdkMaster.OuterXml, LoadOptions.None);

        // Theme — reuse the shared ThemeParser on the theme part's XML.
        if (masterPart.ThemePart?.Theme is { } sdkTheme)
        {
            var themeXml = XElement.Parse(sdkTheme.OuterXml, LoadOptions.None);
            master.Theme = ThemeParser.Parse(themeXml);
        }

        // Master shape tree.
        var masterTree = masterPart.SlideMaster?.CommonSlideData?.ShapeTree;
        if (masterTree is not null)
            ReadShapeTreeNoSlide(masterTree, master.Shapes);

        // Layouts owned by this master.
        foreach (var layoutPart in masterPart.SlideLayoutParts)
        {
            var layout = ReadLayout(layoutPart, master);
            master.Layouts.Add(layout);
            layoutMap[layoutPart] = layout;
        }

        return master;
    }

    private static SlideLayout ReadLayout(
        SlideLayoutPart layoutPart,
        MasterSlide master
    )
    {
        var sdkLayout = layoutPart.SlideLayout;
        var layout = new SlideLayout
        {
            Master = master,
            PartUri = layoutPart.Uri.ToString(),
            Name = sdkLayout?.CommonSlideData?.Name?.Value ?? string.Empty,
            LayoutType = MapLayoutType(sdkLayout?.Type?.InnerText)
        };

        // Preserve the full layout XML for lossless custom-writer save (placeholders the typed
        // model does not capture).
        if (sdkLayout is not null)
            layout.RawElement = XElement.Parse(sdkLayout.OuterXml, LoadOptions.None);

        var tree = sdkLayout?.CommonSlideData?.ShapeTree;
        if (tree is not null)
            ReadShapeTreeNoSlide(tree, layout.Shapes);

        return layout;
    }

    // Masters/layouts have no SlidePart context for blip resolution; image parts on those parts
    // are uncommon and resolved lazily elsewhere. Reuse the shape mapping without picture bytes.
    private static void ReadShapeTreeNoSlide(
        OpenXmlElement tree,
        ShapeCollection target
    )
    {
        foreach (var shape in tree.ChildElements
                     .Select(static child => child switch
                         {
                             P.Shape sp => ReadAutoShape(sp, null, null, null),
                             P.GraphicFrame gf => gf.Graphic?.GraphicData?.Uri?.Value == DmlNames.GraphicDataTableUri
                                 ? ReadGraphicFrame(gf, null)
                                 : null,
                             P.ConnectionShape cxn => ReadConnector(cxn),
                             _ => null
                         }
                     )
                     .Where(static shape => shape is not null))
            target.AddParsed(shape!);
    }

    private static LayoutType MapLayoutType(string? type) => type switch
    {
        "blank" => LayoutType.Blank,
        "title" => LayoutType.Title,
        "tx" or "obj" or "twoObj" => LayoutType.TitleAndContent,
        "twoTxTwoObj" => LayoutType.TitleAndTwoContent,
        "titleOnly" => LayoutType.TitleOnly,
        "secHead" => LayoutType.SectionHeader,
        "twoTx" => LayoutType.TwoTextColumns,
        "vertTx" => LayoutType.TitleAndVerticalText,
        "picTx" => LayoutType.PictureWithCaption,
        "ctrTitle" => LayoutType.TitleSlide,
        _ => LayoutType.Custom
    };

    private static SlideLayout SynthesizeFallback(MasterSlideCollection masters)
    {
        var master = new MasterSlide { Name = "Office Theme" };
        var layout = new SlideLayout
        {
            Name = "Blank",
            LayoutType = LayoutType.Blank,
            Master = master
        };
        master.Layouts.Add(layout);
        masters.Add(master);
        return layout;
    }

    private static IEnumerable<SlideLayout> ToList(SlideLayoutCollection layouts)
    {
        var list = new List<SlideLayout>(layouts.Count);
        list.AddRange(layouts);
        return list;
    }

    // ── Document properties ────────────────────────────────────────────────────────

    private static DocumentProperties ReadProperties(OpenXmlPackage doc)
    {
        var props = new DocumentProperties();
        var core = doc.PackageProperties;
        props.Title = core.Title;
        props.Subject = core.Subject;
        props.Author = core.Creator;
        props.Keywords = core.Keywords;
        props.Description = core.Description;
        props.LastModifiedBy = core.LastModifiedBy;
        props.Category = core.Category;
        props.ContentStatus = core.ContentStatus;
        if (core.Created is { } created) props.Created = created;
        if (core.Modified is { } modified) props.Modified = modified;
        return props;
    }

    // ── Slides ───────────────────────────────────────────────────────────────────

    private static IEnumerable<(SlidePart Part, uint SlideId)> EnumerateSlidesInOrder(PresentationPart presPart)
    {
        // Presentation.SlideIdList preserves authoring order and the real p:sldId/@id; map each
        // r:id to its SlidePart. Fall back to the part collection (with synthetic ids) if absent.
        var idList = presPart.Presentation?.SlideIdList;
        if (idList is null)
        {
            uint synthetic = 256;
            foreach (var sp in presPart.SlideParts)
                yield return (sp, synthetic++);

            yield break;
        }

        foreach (var slideId in idList.Elements<P.SlideId>())
        {
            var rId = slideId.RelationshipId?.Value;
            if (rId is null) continue;

            if (presPart.GetPartById(rId) is SlidePart sp)
                yield return (sp, slideId.Id?.Value ?? 256);
        }
    }

    private static Slide ReadSlide(
        SlidePart slidePart,
        SlideLayout layout,
        uint slideId,
        MediaStore mediaStore,
        Dictionary<string, EmbeddedImage> imageCache
    )
    {
        var slide = new Slide
        {
            Layout = layout,
            SlideId = slideId
        };

        // <p:sld show="0"> means hidden; absence means visible.
        var show = slidePart.Slide?.Show;
        slide.IsHidden = show is not null && !show.Value;

        var tree = slidePart.Slide?.CommonSlideData?.ShapeTree;
        if (tree is not null)
            ReadShapeTree(tree, slide.Shapes, slidePart, mediaStore, imageCache);

        // Speaker notes — reuse the shared NotesParser on the notes-slide XML.
        if (slidePart.NotesSlidePart?.NotesSlide is not { } sdkNotes) return slide;

        var notesXml = XElement.Parse(sdkNotes.OuterXml, LoadOptions.None);
        NotesParser.Parse(notesXml, slide.Notes);

        return slide;
    }

    // ── Shapes ───────────────────────────────────────────────────────────────────

    // Walks a shape-tree (or group) child collection in document order, mapping each element to
    // its concrete Shape subtype. Skips the non-visual group properties container.
    private static void ReadShapeTree(
        OpenXmlElement tree,
        ShapeCollection target,
        SlidePart slidePart,
        MediaStore mediaStore,
        Dictionary<string, EmbeddedImage> imageCache
    )
    {
        foreach (var shape in tree.ChildElements
                     .Select(child => child switch
                         {
                             P.Shape sp => ReadAutoShape(sp, slidePart, mediaStore, imageCache),
                             P.Picture pic => ReadPicture(pic, slidePart, mediaStore, imageCache),
                             P.GraphicFrame gf => ReadGraphicFrame(gf, slidePart),
                             P.GroupShape grp => ReadGroup(grp, slidePart, mediaStore, imageCache),
                             P.ConnectionShape cxn => ReadConnector(cxn),
                             _ => null // nvGrpSpPr, grpSpPr, and unknown elements are not shapes
                         }
                     )
                     .Where(static shape => shape is not null))
            target.AddParsed(shape!);
    }

    private static AutoShape ReadAutoShape(
        P.Shape sp,
        SlidePart? slidePart,
        MediaStore? mediaStore,
        Dictionary<string, EmbeddedImage>? imageCache
    )
    {
        var shape = new AutoShape();
        ReadCommon(sp.NonVisualShapeProperties?.NonVisualDrawingProperties, shape);
        ReadGeometry(sp.ShapeProperties?.Transform2D, shape);
        ReadFillAndLine(sp.ShapeProperties, shape);

        // Resolve spPr blipFill image (AutoShape with picture fill, e.g. group background images)
        if (slidePart is not null && mediaStore is not null && imageCache is not null)
        {
            var blipFill = sp.ShapeProperties?.GetFirstChild<D.BlipFill>();
            var embedId = blipFill?.Blip?.Embed?.Value;
            if (!string.IsNullOrEmpty(embedId) && slidePart.GetPartById(embedId) is ImagePart imagePart)
            {
                shape.Fill.Type = FillType.Picture;
                shape.Fill.Picture = new PictureFill
                {
                    Image = LoadImage(imagePart, mediaStore, imageCache)
                };
            }
        }

        if (sp.TextBody is not { } body) return shape;

        shape.IsTextBox = true;
        ReadTextBody(body, shape.TextFrame);

        return shape;
    }

    private static PictureShape ReadPicture(
        P.Picture pic,
        OpenXmlPartContainer slidePart,
        MediaStore mediaStore,
        IDictionary<string, EmbeddedImage> imageCache
    )
    {
        var shape = new PictureShape();
        ReadCommon(pic.NonVisualPictureProperties?.NonVisualDrawingProperties, shape);
        ReadGeometry(pic.ShapeProperties?.Transform2D, shape);

        // Resolve the embedded image bytes from the blip r:embed relationship into the store,
        // de-duplicating by image-part URI so a shared image yields one EmbeddedImage.
        var embedId = pic.BlipFill?.Blip?.Embed?.Value;
        if (!string.IsNullOrEmpty(embedId) && slidePart.GetPartById(embedId) is ImagePart imagePart)
            shape.Image = LoadImage(imagePart, mediaStore, imageCache);

        return shape;
    }

    private static Shape ReadGraphicFrame(P.GraphicFrame gf, SlidePart? slidePart)
    {
        var uri = gf.Graphic?.GraphicData?.Uri?.Value;

        switch (uri)
        {
            case DmlNames.GraphicDataTableUri:
            {
                var table = new TableShape();
                ReadCommon(gf.NonVisualGraphicFrameProperties?.NonVisualDrawingProperties, table);
                ReadFrameGeometry(gf.Transform, table);
                ReadTable(gf.Graphic?.GraphicData?.GetFirstChild<D.Table>(), table);
                return table;
            }
            // Chart resolution needs the owning slide part; masters/layouts pass null and skip it.
            case DmlNames.GraphicDataChartUri:
            {
                var chart = new ChartShape();
                ReadCommon(gf.NonVisualGraphicFrameProperties?.NonVisualDrawingProperties, chart);
                ReadFrameGeometry(gf.Transform, chart);
                if (slidePart is not null)
                    ReadChart(gf, slidePart, chart);
                return chart;
            }
            // SmartArt diagram — represent the shape type so counts/sequences stay consistent with
            // the custom parser. Full part/node resolution lives on the custom parse path.
            case DmlNames.GraphicDataDiagramUri:
            {
                var smartArt = new SmartArtShape();
                ReadCommon(gf.NonVisualGraphicFrameProperties?.NonVisualDrawingProperties, smartArt);
                ReadFrameGeometry(gf.Transform, smartArt);
                // Preserve the frame XML (incl. <dgm:relIds>) so the SDK save path can re-emit it.
                smartArt.RawElement = XElement.Parse(gf.OuterXml, LoadOptions.None);
                return smartArt;
            }
        }

        // Unknown graphic type — represent as a generic autoshape so counts stay consistent.
        var stub = new AutoShape { ShapeType = AutoShapeType.Rectangle };
        ReadCommon(gf.NonVisualGraphicFrameProperties?.NonVisualDrawingProperties, stub);
        ReadFrameGeometry(gf.Transform, stub);
        return stub;
    }

    private static GroupShape ReadGroup(
        P.GroupShapeType grp,
        SlidePart slidePart,
        MediaStore mediaStore,
        Dictionary<string, EmbeddedImage> imageCache
    )
    {
        var shape = new GroupShape();
        ReadCommon(grp.NonVisualGroupShapeProperties?.NonVisualDrawingProperties, shape);

        var xfrmGroup = grp.GroupShapeProperties?.TransformGroup;
        if (xfrmGroup?.Offset is { } off)
        {
            shape.X = new Emu(off.X ?? 0);
            shape.Y = new Emu(off.Y ?? 0);
        }

        if (xfrmGroup?.Extents is { } ext)
        {
            shape.Width = new Emu(ext.Cx ?? 0);
            shape.Height = new Emu(ext.Cy ?? 0);
        }

        if (xfrmGroup?.ChildOffset is { } chOff)
        {
            shape.ChildOffsetX = new Emu(chOff.X ?? 0);
            shape.ChildOffsetY = new Emu(chOff.Y ?? 0);
        }

        if (xfrmGroup?.ChildExtents is { } chExt)
        {
            shape.ChildExtentWidth = new Emu(chExt.Cx ?? 0);
            shape.ChildExtentHeight = new Emu(chExt.Cy ?? 0);
        }

        ReadShapeTree(grp, shape.Children, slidePart, mediaStore, imageCache);
        return shape;
    }

    private static ConnectorShape ReadConnector(P.ConnectionShape cxn)
    {
        var shape = new ConnectorShape();
        ReadCommon(cxn.NonVisualConnectionShapeProperties?.NonVisualDrawingProperties, shape);
        ReadGeometry(cxn.ShapeProperties?.Transform2D, shape);
        ReadFillAndLine(cxn.ShapeProperties, shape);

        var prst = cxn.ShapeProperties?.GetFirstChild<D.PresetGeometry>()?.Preset?.InnerText;
        shape.ConnectorType = prst switch
        {
            not null when prst.StartsWith("bentConnector", StringComparison.Ordinal) => ConnectorType.Bent,
            not null when prst.StartsWith("curvedConnector", StringComparison.Ordinal) => ConnectorType.Curved,
            _ => ConnectorType.Straight
        };
        return shape;
    }

    // ── Shape helpers ──────────────────────────────────────────────────────────────

    private static void ReadCommon(P.NonVisualDrawingProperties? nv, Shape shape)
    {
        if (nv is null) return;

        shape.Name = nv.Name?.Value ?? string.Empty;
        shape.AltText = nv.Description?.Value;
    }

    // Maps the shape's <p:spPr> fill and outline to the model by reusing the shared FillParser
    // and LineParser. Feeding the SDK element's OuterXml through the same mapping the custom
    // parser uses keeps solid/gradient/pattern/blip fills, theme/scheme colours, and line
    // width/dash/caps/arrowheads identical across both paths — no divergence.
    private static void ReadFillAndLine(P.ShapeProperties? spPr, Shape shape)
    {
        if (spPr is null)
            return;

        var element = XElement.Parse(spPr.OuterXml, LoadOptions.None);
        FillParser.Parse(element, shape.Fill);
        LineParser.Parse(element, shape.Line);
        EffectParser.Parse(element, shape.Effects);
        Shape3DParser.Parse(element, shape.ThreeD);
    }

    // Reads image bytes from the SDK ImagePart, de-duplicating by image-part URI so a single
    // physical image referenced by multiple pictures yields one shared EmbeddedImage.
    private static EmbeddedImage LoadImage(
        OpenXmlPart imagePart,
        MediaStore mediaStore,
        IDictionary<string, EmbeddedImage> imageCache
    )
    {
        var key = imagePart.Uri.ToString();
        if (imageCache.TryGetValue(key, out var existing))
            return existing;

        using var stream = imagePart.GetStream();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var image = mediaStore.AddImage(ms.ToArray(), imagePart.ContentType);
        imageCache[key] = image;
        return image;
    }

    private static void ReadGeometry(D.Transform2D? xfrm, Shape shape)
    {
        if (xfrm?.Offset is { } off)
        {
            shape.X = new Emu(off.X ?? 0);
            shape.Y = new Emu(off.Y ?? 0);
        }

        if (xfrm?.Extents is not { } ext) return;

        shape.Width = new Emu(ext.Cx ?? 0);
        shape.Height = new Emu(ext.Cy ?? 0);
    }

    private static void ReadFrameGeometry(P.Transform? xfrm, Shape shape)
    {
        if (xfrm?.Offset is { } off)
        {
            shape.X = new Emu(off.X ?? 0);
            shape.Y = new Emu(off.Y ?? 0);
        }

        if (xfrm?.Extents is not { } ext) return;

        shape.Width = new Emu(ext.Cx ?? 0);
        shape.Height = new Emu(ext.Cy ?? 0);
    }

    private static void ReadTable(D.Table? table, TableShape shape)
    {
        if (table is null) return;

        var columnWidths = table.TableGrid?.Elements<D.GridColumn>()
            .Select(static c => new Emu(c.Width?.Value ?? 0))
            .ToArray() ?? [];
        var rows = table.Elements<D.TableRow>().ToList();

        foreach (var w in columnWidths)
            shape.Grid.AddColumnWidth(w);

        foreach (var tr in rows)
        {
            var height = new Emu(tr.Height?.Value ?? 0);
            var cells = tr.Elements<D.TableCell>().Select(ReadTableCell).ToList();
            shape.Grid.AddRowWithCells(height, cells);
        }
    }

    private static TableCell ReadTableCell(D.TableCell tc)
    {
        var cell = new TableCell();
        if (tc.TextBody is { } body)
            ReadTextBody(body, cell.TextFrame);
        return cell;
    }

    // Resolves the chart part referenced by the graphic frame and reuses the existing ChartParser
    // to populate the chart model (type/title/data/legend) — the SDK exposes the chart XML; the
    // mapping logic is shared with the custom parser to avoid divergence.
    private static void ReadChart(P.GraphicFrame gf, OpenXmlPartContainer slidePart, ChartShape shape)
    {
        var chartRef = gf.Graphic?.GraphicData
            ?.GetFirstChild<ChartReference>();
        var rId = chartRef?.Id?.Value;
        if (string.IsNullOrEmpty(rId))
            return;

        if (slidePart.GetPartById(rId) is not ChartPart chartPart)
            return;

        shape.RelationshipId = rId;
        shape.PartUri = chartPart.Uri.ToString();

        using var stream = chartPart.GetStream();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();
        shape.ChartPartData = bytes; // preserve raw bytes for round-trip parity

        var chartDoc = OoXmlHelper.ParseXml(bytes);
        if (chartDoc.Root != null)
            ChartParser.Parse(chartDoc.Root, shape.Chart);
    }

    // Maps a shape/cell text body (P.TextBody for shapes, D.TextBody for table cells — both use
    // the a: paragraph/run vocabulary) into the model. Reuses the shared TextParser so run/
    // paragraph formatting (bold/italic/size/font/colour/underline/caps, alignment, bullets,
    // spacing, body anchor/margins) maps identically to the custom parser — no divergence.
    private static void ReadTextBody(OpenXmlElement body, TextFrame frame)
    {
        var element = XElement.Parse(body.OuterXml, LoadOptions.None);
        var parsed = TextParser.ParseTextBody(element);
        frame.AbsorbFrom(parsed);
    }
}
