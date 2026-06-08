using DocumentFormat.OpenXml.Packaging;
using Unchained.Ooxml;
using Unchained.Ooxml.Engine;
using Unchained.Ooxml.Text;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Core;
using Unchained.Pptx.Media;
using Unchained.Pptx.Models;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Security;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;
using Unchained.Pptx.Themes;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace Unchained.Pptx.Parsing;

/// <summary>
/// Phase 2 OpenXML-SDK-backed presentation reader. Produces the same <see cref="ParsedPresentation"/>
/// typed model as the legacy custom <see cref="PresentationParser"/>, but maps it from the SDK's
/// validated typed DOM (<c>DocumentFormat.OpenXml</c>) instead of hand-rolled XElement walking.
/// </summary>
/// <remarks>
/// This runs in parallel with the custom parser (selected via <c>OpenOptions.UseOpenXmlEngine</c>)
/// and is being grown to full parity. It currently maps: slide size, core document properties,
/// slide structure (real id / name / hidden), and every shape-tree element to its concrete
/// <see cref="Shape"/> subtype (autoshape, picture, table, chart, group, connector) with geometry,
/// text, and table cell content. Masters/layouts are represented minimally so the model is
/// internally consistent; fills, themes, and image bytes are later parity steps.
/// </remarks>
internal static class OpenXmlPresentationParser
{
    public static ParsedPresentation Parse(byte[] data, OpenOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(data);

        using var engine = OoxmlEngine.Open(data, editable: false);
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

        var slideSize = ReadSlideSize(presPart);
        var properties = ReadProperties(doc);

        // A single minimal master + layout keeps the model self-consistent (every slide needs a
        // Layout whose Master resolves). Full master/layout mapping is a later parity step.
        var master = new MasterSlide { Name = "Office Theme", Theme = new PptxTheme() };
        var fallbackLayout = new SlideLayout
        {
            Name = "Blank",
            LayoutType = Models.Themes.LayoutType.Blank,
            Master = master
        };
        master.Layouts.Add(fallbackLayout);
        masters.Add(master);

        // Slides, in presentation order.
        foreach (var (slidePart, slideId) in EnumerateSlidesInOrder(presPart))
        {
            var slide = ReadSlide(slidePart, fallbackLayout, slideId);
            slides.AddParsed(slide);
        }

        properties.SlideCount = slides.Count;
        properties.HiddenSlideCount = slides.Count(static s => s.IsHidden);

        return new ParsedPresentation(
            package: null, // the SDK engine owns the real package; not consumed downstream
            slides,
            masters,
            mediaStore,
            properties,
            protection: new ProtectionInfo(),
            slideSize,
            commentAuthors,
            sections);
    }

    // ── Slide size ───────────────────────────────────────────────────────────────

    private static SlideSize ReadSlideSize(PresentationPart presPart)
    {
        var sz = presPart.Presentation?.SlideSize;
        if (sz?.Cx is null || sz.Cy is null)
            return SlideSize.Widescreen;
        return new SlideSize(new Emu(sz.Cx!.Value), new Emu(sz.Cy!.Value));
    }

    // ── Document properties ────────────────────────────────────────────────────────

    private static DocumentProperties ReadProperties(PresentationDocument doc)
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
            foreach (var sp in presPart.SlideParts) yield return (sp, synthetic++);
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

    private static Slide ReadSlide(SlidePart slidePart, SlideLayout layout, uint slideId)
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
            ReadShapeTree(tree, slide.Shapes);

        return slide;
    }

    // ── Shapes ───────────────────────────────────────────────────────────────────

    // Walks a shape-tree (or group) child collection in document order, mapping each element to
    // its concrete Shape subtype. Skips the non-visual group properties container.
    private static void ReadShapeTree(DocumentFormat.OpenXml.OpenXmlCompositeElement tree, ShapeCollection target)
    {
        foreach (var child in tree.ChildElements)
        {
            Shape? shape = child switch
            {
                P.Shape sp => ReadAutoShape(sp),
                P.Picture pic => ReadPicture(pic),
                P.GraphicFrame gf => ReadGraphicFrame(gf),
                P.GroupShape grp => ReadGroup(grp),
                P.ConnectionShape cxn => ReadConnector(cxn),
                _ => null // nvGrpSpPr, grpSpPr, and unknown elements are not shapes
            };
            if (shape is not null)
                target.AddParsed(shape);
        }
    }

    private static AutoShape ReadAutoShape(P.Shape sp)
    {
        var shape = new AutoShape();
        ReadCommon(sp.NonVisualShapeProperties?.NonVisualDrawingProperties, shape);
        ReadGeometry(sp.ShapeProperties?.Transform2D, shape);

        if (sp.TextBody is { } body)
        {
            shape.IsTextBox = true;
            ReadTextBody(body, shape.TextFrame);
        }

        return shape;
    }

    private static PictureShape ReadPicture(P.Picture pic)
    {
        var shape = new PictureShape();
        ReadCommon(pic.NonVisualPictureProperties?.NonVisualDrawingProperties, shape);
        ReadGeometry(pic.ShapeProperties?.Transform2D, shape);
        // Image bytes are resolved from the blip r:embed relationship in a later parity step.
        return shape;
    }

    private static Shape ReadGraphicFrame(P.GraphicFrame gf)
    {
        var uri = gf.Graphic?.GraphicData?.Uri?.Value;

        if (uri == DmlNames.GraphicDataTableUri)
        {
            var table = new TableShape();
            ReadCommon(gf.NonVisualGraphicFrameProperties?.NonVisualDrawingProperties, table);
            ReadFrameGeometry(gf.Transform, table);
            ReadTable(gf.Graphic?.GraphicData?.GetFirstChild<D.Table>(), table);
            return table;
        }

        if (uri == DmlNames.GraphicDataChartUri)
        {
            var chart = new ChartShape();
            ReadCommon(gf.NonVisualGraphicFrameProperties?.NonVisualDrawingProperties, chart);
            ReadFrameGeometry(gf.Transform, chart);
            // Chart model (type/series/data) is resolved from the chart part in a later step.
            return chart;
        }

        // Unknown graphic type — represent as a generic autoshape so counts stay consistent.
        var stub = new AutoShape { ShapeType = AutoShapeType.Rectangle };
        ReadCommon(gf.NonVisualGraphicFrameProperties?.NonVisualDrawingProperties, stub);
        ReadFrameGeometry(gf.Transform, stub);
        return stub;
    }

    private static GroupShape ReadGroup(P.GroupShape grp)
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

        ReadShapeTree(grp, shape.Children);
        return shape;
    }

    private static ConnectorShape ReadConnector(P.ConnectionShape cxn)
    {
        var shape = new ConnectorShape();
        ReadCommon(cxn.NonVisualConnectionShapeProperties?.NonVisualDrawingProperties, shape);
        ReadGeometry(cxn.ShapeProperties?.Transform2D, shape);

        var prst = cxn.ShapeProperties?.GetFirstChild<D.PresetGeometry>()?.Preset?.InnerText;
        shape.ConnectorType = prst switch
        {
            { } p when p.StartsWith("bentConnector", StringComparison.Ordinal) => ConnectorType.Bent,
            { } p when p.StartsWith("curvedConnector", StringComparison.Ordinal) => ConnectorType.Curved,
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

    private static void ReadGeometry(D.Transform2D? xfrm, Shape shape)
    {
        if (xfrm?.Offset is { } off)
        {
            shape.X = new Emu(off.X ?? 0);
            shape.Y = new Emu(off.Y ?? 0);
        }
        if (xfrm?.Extents is { } ext)
        {
            shape.Width = new Emu(ext.Cx ?? 0);
            shape.Height = new Emu(ext.Cy ?? 0);
        }
    }

    private static void ReadFrameGeometry(P.Transform? xfrm, Shape shape)
    {
        if (xfrm?.Offset is { } off)
        {
            shape.X = new Emu(off.X ?? 0);
            shape.Y = new Emu(off.Y ?? 0);
        }
        if (xfrm?.Extents is { } ext)
        {
            shape.Width = new Emu(ext.Cx ?? 0);
            shape.Height = new Emu(ext.Cy ?? 0);
        }
    }

    private static void ReadTable(D.Table? table, TableShape shape)
    {
        if (table is null) return;

        var columnWidths = table.TableGrid?.Elements<D.GridColumn>()
            .Select(static c => new Emu(c.Width?.Value ?? 0)).ToArray() ?? [];
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

    private static void ReadTextBody(D.TextBody body, TextFrame frame)
    {
        foreach (var para in body.Elements<D.Paragraph>())
        {
            var p = frame.Paragraphs.Add();
            foreach (var run in para.Elements<D.Run>())
            {
                var text = run.Text?.Text;
                if (!string.IsNullOrEmpty(text))
                    p.Runs.Add(text);
            }
        }
    }

    private static void ReadTextBody(P.TextBody body, TextFrame frame)
    {
        foreach (var para in body.Elements<D.Paragraph>())
        {
            var p = frame.Paragraphs.Add();
            foreach (var run in para.Elements<D.Run>())
            {
                var text = run.Text?.Text;
                if (!string.IsNullOrEmpty(text))
                    p.Runs.Add(text);
            }
        }
    }
}
