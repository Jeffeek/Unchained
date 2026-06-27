using System.Xml.Linq;
using Unchained.Ooxml;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Media;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Shapes;

namespace Unchained.Pptx.Parsing;

/// <summary>
///     Parses a PresentationML shape tree (<c>&lt;p:spTree&gt;</c>) into
///     <see cref="ShapeCollection" /> objects.
/// </summary>
internal sealed class ShapeParser
{
    /// <summary>
    ///     Reads all shapes from a <c>&lt;p:spTree&gt;</c> element and adds them to
    ///     <paramref name="collection" />.
    /// </summary>
    public void ParseTree(XElement spTree, ShapeCollection collection)
    {
        ArgumentNullException.ThrowIfNull(spTree);
        ArgumentNullException.ThrowIfNull(collection);

        foreach (var shape in spTree.Elements().Select(TryParseShape).OfType<Shape>())
            collection.AddParsed(shape);
    }

    // ── Shape dispatch ────────────────────────────────────────────────────────

    private Shape? TryParseShape(XElement element) =>
        element.Name == PmlNames.Shape
            ? ParseAutoShape(element)
            : element.Name == PmlNames.Picture
                ? ParsePicture(element)
                : element.Name == PmlNames.GraphicFrame
                    ? ParseGraphicFrame(element)
                    : element.Name == PmlNames.GroupShape
                        ? ParseGroup(element)
                        : element.Name == PmlNames.Connector
                            ? ParseConnector(element)
                            : null; // skip unknown elements (e.g. p:nvGrpSpPr, p:grpSpPr)

    // ── AutoShape / TextBox ───────────────────────────────────────────────────

    private static AutoShape ParseAutoShape(XElement spEl)
    {
        var shape = new AutoShape();
        ReadNonVisualProperties(spEl.Element(PmlNames.NonVisualShapeProperties), shape);
        ReadTransform(spEl.Element(PmlNames.ShapeProperties), shape);
        ReadGeometry(spEl.Element(PmlNames.ShapeProperties), shape);
        ReadFillAndLine(spEl.Element(PmlNames.ShapeProperties), shape);

        // Detect text box (cNvSpPr/@txBox="1")
        var cNvSpPr = spEl.Element(PmlNames.NonVisualShapeProperties)
            ?.Elements()
            .FirstOrDefault(static e => e.Name.LocalName == "cNvSpPr");
        shape.IsTextBox = cNvSpPr?.GetAttrBool("txBox") == true;

        // Text body
        var txBody = spEl.Element(PmlNames.TextBody);
        if (txBody != null)
        {
            var parsed = TextParser.ParseTextBody(txBody);
            shape.TextFrame.AbsorbFrom(parsed);
        }

        ReadStyleFill(spEl, shape);
        shape.RawElement = spEl;
        return shape;
    }

    // ── Picture ───────────────────────────────────────────────────────────────

    private static PictureShape ParsePicture(XElement picEl)
    {
        var shape = new PictureShape();
        ReadNonVisualProperties(picEl.Element(PmlNames.NonVisualPictureProperties), shape);
        ReadTransform(picEl.Element(PmlNames.ShapeProperties), shape);
        ReadFillAndLine(picEl.Element(PmlNames.ShapeProperties), shape);

        // Resolve image via relationship
        var blipFill = picEl.Element(PmlNames.BlipFill);
        var blip = blipFill?.Element(DmlNames.Blip);
        if (blip != null)
        {
            var rId = (string?)blip.Attribute(PmlNames.RelationshipId);
            if (rId != null)
                shape.Image = ResolveImage(rId, picEl);
        }

        shape.RawElement = picEl;
        return shape;
    }

    // ── Graphic frame (table, chart, SmartArt) ────────────────────────────────

    private static Shape ParseGraphicFrame(XElement frameEl)
    {
        var graphic = frameEl.Element(DmlNames.Graphic);
        var graphicData = graphic?.Element(DmlNames.GraphicData);
        var uri = graphicData?.GetAttr(DmlNames.AttributeUri);

        switch (uri)
        {
            case DmlNames.GraphicDataTableUri:
                return ParseTable(frameEl, graphicData!);
            case DmlNames.GraphicDataChartUri:
                return ParseChart(frameEl, graphicData!);
            case DmlNames.GraphicDataDiagramUri:
                return ParseSmartArt(frameEl, graphicData!);
        }

        // Unknown graphic type — return generic AutoShape stub
        var stub = new AutoShape
        {
            ShapeType = AutoShapeType.Rectangle,
            RawElement = frameEl
        };
        ReadNonVisualProperties(frameEl.Element(PmlNames.NonVisualGraphicFrameProperties), stub);
        ReadFrameTransform(frameEl, stub);
        return stub;
    }

    private static TableShape ParseTable(XElement frameEl, XContainer graphicData)
    {
        var shape = new TableShape();
        ReadNonVisualProperties(frameEl.Element(PmlNames.NonVisualGraphicFrameProperties), shape);
        ReadFrameTransform(frameEl, shape);

        var tbl = graphicData.Element(DmlNames.Table);
        if (tbl != null)
            ParseTableContent(tbl, shape);

        shape.RawElement = frameEl;
        return shape;
    }

    private static void ParseTableContent(XContainer tbl, TableShape shape)
    {
        var tblPr = tbl.Element(DmlNames.TableProperties);
        if (tblPr != null)
        {
            shape.HasHeaderRow = tblPr.GetAttrBool("firstRow") ?? false;
            shape.HasTotalRow = tblPr.GetAttrBool("lastRow") ?? false;
            shape.HasBandedRows = tblPr.GetAttrBool("bandRow") ?? false;
            shape.HasBandedColumns = tblPr.GetAttrBool("bandCol") ?? false;
            shape.HasFirstColumn = tblPr.GetAttrBool("firstCol") ?? false;
            shape.HasLastColumn = tblPr.GetAttrBool("lastCol") ?? false;
        }

        var grid = tbl.Element(DmlNames.TableGrid);
        foreach (var w in from col in grid?.Elements(DmlNames.GridColumn) ?? [] select col.GetAttrLong(DmlNames.AttributeWidth, 0))
            shape.Grid.AddColumnWidth(new Emu(w));

        foreach (var tr in tbl.Elements(DmlNames.TableRow))
        {
            var h = tr.GetAttrLong(DmlNames.AttributeRowHeight, 0);
            var cells = tr.Elements(DmlNames.TableCell).Select(ParseTableCell).ToList();
            shape.Grid.AddRowWithCells(new Emu(h), cells);
        }
    }

    private static TableCell ParseTableCell(XElement tcEl)
    {
        var cell = new TableCell
        {
            ColumnSpan = tcEl.GetAttrInt("gridSpan", 1),
            RowSpan = tcEl.GetAttrInt("rowSpan", 1),
            IsHorizontalMergeContinuation = tcEl.GetAttrBool("hMerge") ?? false,
            IsVerticalMergeContinuation = tcEl.GetAttrBool("vMerge") ?? false
        };

        var txBody = tcEl.Element(DmlNames.TextBody);
        if (txBody != null)
        {
            var parsedCell = TextParser.ParseTextBody(txBody);
            foreach (var para in parsedCell.Paragraphs)
                cell.TextFrame.Paragraphs.Add(para);
        }

        var tcPr = tcEl.Element(DmlNames.TableCellProperties);

        if (tcPr == null) return cell;

        FillParser.Parse(tcPr, cell.Fill);
        // Cell border lines: lnL, lnR, lnT, lnB
        var lnL = tcPr.Element(DmlNames.Dml + "lnL");
        if (lnL != null) LineParser.ParseElement(lnL, cell.LeftBorder);
        var lnR = tcPr.Element(DmlNames.Dml + "lnR");
        if (lnR != null) LineParser.ParseElement(lnR, cell.RightBorder);
        var lnT = tcPr.Element(DmlNames.Dml + "lnT");
        if (lnT != null) LineParser.ParseElement(lnT, cell.TopBorder);
        var lnB = tcPr.Element(DmlNames.Dml + "lnB");
        if (lnB != null) LineParser.ParseElement(lnB, cell.BottomBorder);

        return cell;
    }

    private static ChartShape ParseChart(XElement frameEl, XContainer graphicData)
    {
        var shape = new ChartShape();
        ReadNonVisualProperties(frameEl.Element(PmlNames.NonVisualGraphicFrameProperties), shape);
        ReadFrameTransform(frameEl, shape);

        // Find the chart relationship and preserve the part data for round-trip
        var chartRef = graphicData.Element(DmlNames.Chart + "chart")
                       ?? graphicData.Elements().FirstOrDefault(static e => e.Name.LocalName == "chart");
        var rId = (string?)chartRef?.Attribute(PmlNames.RelationshipId);
        if (rId != null)
        {
            shape.RelationshipId = rId;
            // Chart part resolution is done via the slide's OpcPart context;
            // stored as raw bytes for lossless round-trip.
        }

        shape.RawElement = frameEl;
        return shape;
    }

    // ── SmartArt (diagram) ──────────────────────────────────────────────────────

    private static SmartArtShape ParseSmartArt(XElement frameEl, XContainer graphicData)
    {
        var shape = new SmartArtShape();
        ReadNonVisualProperties(frameEl.Element(PmlNames.NonVisualGraphicFrameProperties), shape);
        ReadFrameTransform(frameEl, shape);

        // <dgm:relIds r:dm=".." r:lo=".." r:qs=".." r:cs=".."/> references the four diagram parts.
        var relIds = graphicData.Element(DmlNames.DiagramRelIds)
                     ?? graphicData.Elements().FirstOrDefault(static e => e.Name.LocalName == "relIds");
        if (relIds != null)
        {
            var r = PmlNames.Relationships;
            shape.DataRelationshipId = (string?)relIds.Attribute(r + "dm") ?? string.Empty;
            shape.LayoutRelationshipId = (string?)relIds.Attribute(r + "lo") ?? string.Empty;
            shape.QuickStyleRelationshipId = (string?)relIds.Attribute(r + "qs") ?? string.Empty;
            shape.ColorsRelationshipId = (string?)relIds.Attribute(r + "cs") ?? string.Empty;
        }

        // Part bytes + node model are resolved in a second pass (SlideParser) where the
        // slide's relationships and the package are available.
        shape.RawElement = frameEl;
        return shape;
    }

    // ── Group ─────────────────────────────────────────────────────────────────

    private GroupShape ParseGroup(XElement grpEl)
    {
        var shape = new GroupShape();
        ReadNonVisualProperties(grpEl.Element(PmlNames.NonVisualGroupShapeProperties), shape);

        var grpSpPr = grpEl.Element(PmlNames.GroupShapeProperties);
        if (grpSpPr != null)
            ReadGroupTransform(grpSpPr, shape);

        foreach (var child2 in from child in grpEl.Elements()
                               where child.Name != PmlNames.NonVisualGroupShapeProperties
                               where child.Name != PmlNames.GroupShapeProperties
                               select TryParseShape(child))
            shape.Children.AddParsed(child2);

        shape.RawElement = grpEl;
        return shape;
    }

    // ── Connector ─────────────────────────────────────────────────────────────

    private static ConnectorShape ParseConnector(XElement cxnEl)
    {
        var shape = new ConnectorShape();
        ReadNonVisualProperties(cxnEl.Element(PmlNames.NonVisualConnectorProperties), shape);
        ReadTransform(cxnEl.Element(PmlNames.ShapeProperties), shape);
        ReadFillAndLine(cxnEl.Element(PmlNames.ShapeProperties), shape);

        var prst = cxnEl.Element(PmlNames.ShapeProperties)
            ?.Element(DmlNames.PresetGeometry)
            ?.GetAttr(DmlNames.AttributePreset, string.Empty);

        shape.ConnectorType = prst switch
        {
            "bentConnector2" or "bentConnector3" or "bentConnector4" or "bentConnector5"
                => ConnectorType.Bent,
            "curvedConnector2" or "curvedConnector3" or "curvedConnector4" or "curvedConnector5"
                => ConnectorType.Curved,
            _ => ConnectorType.Straight
        };

        shape.RawElement = cxnEl;
        return shape;
    }

    // ── Shared read helpers ───────────────────────────────────────────────────

    private static void ReadNonVisualProperties(XContainer? nvPrContainer, Shape shape)
    {
        var cNvPr = nvPrContainer?.Element(PmlNames.CommonNonVisualProperties);
        if (cNvPr == null) return;

        var id = cNvPr.GetAttrInt(PmlNames.AttributeId);
        if (id.HasValue) shape.ShapeId = (uint)id.Value;

        shape.Name = cNvPr.GetAttr(PmlNames.AttributeName, string.Empty);
        shape.AltText = cNvPr.GetAttr(DmlNames.AttributeDescription);
        shape.AltTextTitle = cNvPr.GetAttr("title");

        // IsDecorative: Microsoft extension stored in a16:creationId ext list.
        // The standard attribute is on the cNvPr/@decor extension or extLst.
        // Check for <a:extLst><a:ext><a16:decorative val="1"/></a:ext></a:extLst>.
        var extLst = cNvPr.Element(DmlNames.Dml + "extLst");
        if (extLst is not null)
        {
            foreach (var decorative in extLst.Elements(DmlNames.Dml + "ext")
                         .Select(static ext => ext.Elements().FirstOrDefault(static e => e.Name.LocalName == "decorative"))
                         .OfType<XElement>())
            {
                shape.IsDecorative = decorative.GetAttrBool(DmlNames.AttributeValue) ?? false;
                break;
            }
        }

        // Click hyperlink (<a:hlinkClick>) — capture the relationship id + tooltip; the target is
        // resolved against the slide's relationships in a second pass (SlideParser).
        var hlink = cNvPr.Element(DmlNames.HyperlinkClick);
        if (hlink != null)
            shape.ClickAction = ReadHyperlink(hlink);

        // Placeholder reference (<p:nvPr>/<p:ph>) — captures the role + index so the slide
        // parser can inherit geometry/formatting from the matching layout placeholder.
        var ph = nvPrContainer!.Element(PmlNames.ApplicationNonVisualProperties)?.Element(PmlNames.Placeholder);
        if (ph == null) return;

        shape.PlaceholderType = ParsePlaceholderType(ph.GetAttr(PmlNames.AttributeType));
        var idx = ph.GetAttrInt(CmlNames.AttributeIndex);
        if (idx.HasValue) shape.PlaceholderIndex = idx.Value;
    }

    /// <summary>Maps a <c>p:ph/@type</c> value to <see cref="PlaceholderType" />. Absent = Content.</summary>
    private static PlaceholderType ParsePlaceholderType(string? type) => type switch
    {
        null or "" => PlaceholderType.Content,
        "title" => PlaceholderType.Title,
        "ctrTitle" => PlaceholderType.CenteredTitle,
        "subTitle" => PlaceholderType.Subtitle,
        "body" => PlaceholderType.Body,
        "obj" => PlaceholderType.Object,
        "dt" => PlaceholderType.Date,
        "ftr" => PlaceholderType.Footer,
        "sldNum" => PlaceholderType.SlideNumber,
        "hdr" => PlaceholderType.Header,
        "chart" or "tbl" or "pic" or "media" or "clipArt" or "dgm" => PlaceholderType.Media,
        _ => PlaceholderType.Content
    };

    /// <summary>
    ///     Reads a <c>&lt;a:hlinkClick&gt;</c> (or hover) element into a <see cref="HyperlinkAction" />
    ///     with its relationship id and tooltip captured. URL/slide resolution happens later.
    /// </summary>
    private static HyperlinkAction ReadHyperlink(XElement hlinkEl)
    {
        var action = new HyperlinkAction
        {
            RelationshipId = (string?)hlinkEl.Attribute(PmlNames.Relationships + "id") ?? string.Empty,
            Tooltip = (string?)hlinkEl.Attribute("tooltip")
        };
        return action;
    }

    private static void ReadTransform(XContainer? spPr, Shape shape)
    {
        var xfrm = spPr?.Element(DmlNames.Transform);
        if (xfrm == null) return;

        ReadTransformFromXfrm(xfrm, shape);
    }

    private static void ReadFrameTransform(XContainer frameEl, Shape shape)
    {
        var xfrm = frameEl.Element(DmlNames.Transform)
                   ?? frameEl.Element(PmlNames.Pml + "xfrm");
        if (xfrm != null)
            ReadTransformFromXfrm(xfrm, shape);
    }

    private static void ReadGroupTransform(XContainer grpSpPr, Shape shape)
    {
        var xfrm = grpSpPr.Element(DmlNames.Transform);
        if (xfrm == null) return;

        ReadTransformFromXfrm(xfrm, shape);

        if (shape is not GroupShape group) return;

        var chOff = xfrm.Element(DmlNames.ChildOffset);
        if (chOff != null)
        {
            group.ChildOffsetX = new Emu(chOff.GetAttrLong(DmlNames.AttributeX, 0));
            group.ChildOffsetY = new Emu(chOff.GetAttrLong(DmlNames.AttributeY, 0));
        }

        var chExt = xfrm.Element(DmlNames.ChildExtent);
        if (chExt == null) return;

        group.ChildExtentWidth = new Emu(chExt.GetAttrLong(DmlNames.AttributeWidth, 0));
        group.ChildExtentHeight = new Emu(chExt.GetAttrLong(DmlNames.AttributeHeight, 0));
    }

    private static void ReadTransformFromXfrm(XElement xfrm, Shape shape)
    {
        var off = xfrm.Element(DmlNames.Offset);
        if (off != null)
        {
            shape.X = new Emu(off.GetAttrLong(DmlNames.AttributeX, 0));
            shape.Y = new Emu(off.GetAttrLong(DmlNames.AttributeY, 0));
        }

        var ext = xfrm.Element(DmlNames.Extents);
        if (ext != null)
        {
            shape.Width = new Emu(ext.GetAttrLong(DmlNames.AttributeWidth, 0));
            shape.Height = new Emu(ext.GetAttrLong(DmlNames.AttributeHeight, 0));
        }

        var rot = xfrm.GetAttrInt(DmlNames.AttributeRotation);
        if (rot.HasValue)
            shape.RotationDegrees = OoXmlHelper.OoxmlRotationToDegrees(rot.Value);

        shape.FlipHorizontal = xfrm.GetAttrBool(DmlNames.AttributeFlipHorizontal) ?? false;
        shape.FlipVertical = xfrm.GetAttrBool(DmlNames.AttributeFlipVertical) ?? false;
    }

    private static void ReadGeometry(XContainer? spPr, AutoShape shape)
    {
        if (spPr == null) return;

        var prstGeom = spPr.Element(DmlNames.PresetGeometry);
        if (prstGeom != null)
        {
            shape.ShapeType = MapPresetGeometry(prstGeom.GetAttr(DmlNames.AttributePreset, "rect"));
            return;
        }

        if (spPr.Element(DmlNames.CustomGeometry) != null)
            shape.ShapeType = AutoShapeType.Custom;
    }

    private static void ReadFillAndLine(XElement? spPr, Shape shape)
    {
        if (spPr == null) return;

        FillParser.Parse(spPr, shape.Fill);
        LineParser.Parse(spPr, shape.Line);
        EffectParser.Parse(spPr, shape.Effects);
        Shape3DParser.Parse(spPr, shape.ThreeD);
    }

    // Reads p:style/a:fillRef and a:fontRef to get theme-driven style fill and text colors.
    // These apply when spPr has no explicit fill (FillType.None) or runs have no explicit color.
    private static void ReadStyleFill(XContainer shapeEl, Shape shape)
    {
        var styleEl = shapeEl.Element(PmlNames.Pml + "style");
        if (styleEl is null) return;

        var fillRef = styleEl.Element(DmlNames.Dml + "fillRef");
        if (fillRef is not null)
            shape.StyleFillColor = ColorParser.Parse(fillRef);

        var fontRef = styleEl.Element(DmlNames.Dml + "fontRef");
        if (fontRef is not null)
            shape.StyleTextColor = ColorParser.Parse(fontRef);
    }

    // ReSharper disable UnusedParameter.Local
    private static EmbeddedImage? ResolveImage(string relationshipId, XElement shapeElement) =>
        // ReSharper restore UnusedParameter.Local
        // Image resolution uses the slide's OPC part — stored in RawElement context.
        // The SlideParser will call PostProcessImages after initial parse.
        // For now return null; images are resolved in a second pass.
        null;

    // ── Preset geometry map ───────────────────────────────────────────────────

    private static AutoShapeType MapPresetGeometry(string prst) => prst switch
    {
        "rect" => AutoShapeType.Rectangle,
        "roundRect" => AutoShapeType.RoundedRectangle,
        "ellipse" => AutoShapeType.Ellipse,
        "triangle" => AutoShapeType.IsoscelesTriangle,
        "rtTriangle" => AutoShapeType.RightTriangle,
        "diamond" => AutoShapeType.Diamond,
        "parallelogram" => AutoShapeType.Parallelogram,
        "trapezoid" => AutoShapeType.Trapezoid,
        "pentagon" => AutoShapeType.Pentagon,
        "hexagon" => AutoShapeType.Hexagon,
        "heptagon" => AutoShapeType.Heptagon,
        "octagon" => AutoShapeType.Octagon,
        "decagon" => AutoShapeType.Decagon,
        "dodecagon" => AutoShapeType.Dodecagon,
        "star4" => AutoShapeType.Star4,
        "star5" => AutoShapeType.Star5,
        "star6" => AutoShapeType.Star6,
        "star7" => AutoShapeType.Star7,
        "star8" => AutoShapeType.Star8,
        "star10" => AutoShapeType.Star10,
        "star12" => AutoShapeType.Star12,
        "star16" => AutoShapeType.Star16,
        "star24" => AutoShapeType.Star24,
        "star32" => AutoShapeType.Star32,
        "rightArrow" => AutoShapeType.RightArrow,
        "leftArrow" => AutoShapeType.LeftArrow,
        "upArrow" => AutoShapeType.UpArrow,
        "downArrow" => AutoShapeType.DownArrow,
        "leftRightArrow" => AutoShapeType.LeftRightArrow,
        "upDownArrow" => AutoShapeType.UpDownArrow,
        "plus" => AutoShapeType.Plus,
        "donut" => AutoShapeType.Donut,
        "noSmoking" => AutoShapeType.NoSymbol,
        "cube" => AutoShapeType.Cube,
        "can" => AutoShapeType.Can,
        "bevel" => AutoShapeType.Bevel,
        "foldedCorner" => AutoShapeType.FoldedCorner,
        "smileyFace" => AutoShapeType.SmileyFace,
        "heart" => AutoShapeType.Heart,
        "lightningBolt" => AutoShapeType.LightningBolt,
        "sun" => AutoShapeType.Sun,
        "moon" => AutoShapeType.Moon,
        "cloud" => AutoShapeType.Cloud,
        "arc" => AutoShapeType.Arc,
        "wave" => AutoShapeType.Wave,
        "doubleWave" => AutoShapeType.DoubleWave,
        "callout1" => AutoShapeType.RectangularCallout,
        "roundedRectCallout" => AutoShapeType.RoundedRectangularCallout,
        "ellipseCallout" => AutoShapeType.OvalCallout,
        "cloudCallout" => AutoShapeType.CloudCallout,
        "flowChartProcess" => AutoShapeType.FlowChartProcess,
        "flowChartDecision" => AutoShapeType.FlowChartDecision,
        "flowChartInputOutput" => AutoShapeType.FlowChartInputOutput,
        "flowChartTerminator" => AutoShapeType.FlowChartTerminator,
        "flowChartDocument" => AutoShapeType.FlowChartDocument,
        "flowChartConnector" => AutoShapeType.FlowChartConnector,
        "mathPlus" => AutoShapeType.MathPlus,
        "mathMinus" => AutoShapeType.MathMinus,
        "mathMultiply" => AutoShapeType.MathMultiply,
        "mathDivide" => AutoShapeType.MathDivide,
        "mathEqual" => AutoShapeType.MathEqual,
        "mathNotEqual" => AutoShapeType.MathNotEqual,
        "line" => AutoShapeType.Line,
        "straightConnector1" => AutoShapeType.StraightConnector,
        "bentConnector2" or "bentConnector3" or "bentConnector4" => AutoShapeType.BentConnector,
        "curvedConnector2" or "curvedConnector3" or "curvedConnector4" => AutoShapeType.CurvedConnector,
        _ => AutoShapeType.Rectangle
    };
}
