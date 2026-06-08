using Unchained.Pptx.Core.Xml;
using System.Xml.Linq;
using Unchained.Ooxml;
using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Media;
using Unchained.Ooxml.Media;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Shapes;

namespace Unchained.Pptx.Parsing;

/// <summary>
/// Parses a PresentationML shape tree (<c>&lt;p:spTree&gt;</c>) into
/// <see cref="ShapeCollection"/> objects.
/// </summary>
internal sealed class ShapeParser
{
    private readonly OpcPackage _package;
    private readonly MediaStore _mediaStore;

    public ShapeParser(OpcPackage package, MediaStore mediaStore)
    {
        _package = package;
        _mediaStore = mediaStore;
    }

    /// <summary>
    /// Reads all shapes from a <c>&lt;p:spTree&gt;</c> element and adds them to
    /// <paramref name="collection"/>.
    /// </summary>
    public void ParseTree(XElement spTree, ShapeCollection collection)
    {
        ArgumentNullException.ThrowIfNull(spTree);
        ArgumentNullException.ThrowIfNull(collection);

        foreach (var child in spTree.Elements())
        {
            var shape = TryParseShape(child);
            if (shape != null)
                collection.AddParsed(shape);
        }
    }

    // ── Shape dispatch ────────────────────────────────────────────────────────

    private Shape? TryParseShape(XElement element)
    {
        if (element.Name == PmlNames.Shape)       return ParseAutoShape(element);
        if (element.Name == PmlNames.Picture)     return ParsePicture(element);
        if (element.Name == PmlNames.GraphicFrame) return ParseGraphicFrame(element);
        if (element.Name == PmlNames.GroupShape)  return ParseGroup(element);
        if (element.Name == PmlNames.Connector)   return ParseConnector(element);
        return null; // skip unknown elements (e.g. p:nvGrpSpPr, p:grpSpPr)
    }

    // ── AutoShape / TextBox ───────────────────────────────────────────────────

    private AutoShape ParseAutoShape(XElement spEl)
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
            shape.TextFrame.Format.VerticalAnchor = parsed.Format.VerticalAnchor;
            shape.TextFrame.Format.WrapText = parsed.Format.WrapText;
            shape.TextFrame.Format.Direction = parsed.Format.Direction;
            shape.TextFrame.Format.Autofit = parsed.Format.Autofit;
            foreach (var para in parsed.Paragraphs)
                shape.TextFrame.Paragraphs.Add(para);
        }

        shape.RawElement = spEl;
        return shape;
    }

    // ── Picture ───────────────────────────────────────────────────────────────

    private PictureShape ParsePicture(XElement picEl)
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

    private Shape ParseGraphicFrame(XElement frameEl)
    {
        var graphic = frameEl.Element(DmlNames.Graphic);
        var graphicData = graphic?.Element(DmlNames.GraphicData);
        var uri = graphicData?.GetAttr(DmlNames.AttributeUri);

        if (uri == DmlNames.GraphicDataTableUri)
            return ParseTable(frameEl, graphicData!);

        if (uri == DmlNames.GraphicDataChartUri)
            return ParseChart(frameEl, graphicData!);

        // Unknown graphic type — return generic AutoShape stub
        var stub = new AutoShape { ShapeType = AutoShapeType.Rectangle };
        stub.RawElement = frameEl;
        ReadNonVisualProperties(frameEl.Element(PmlNames.NonVisualGraphicFrameProperties), stub);
        ReadFrameTransform(frameEl, stub);
        return stub;
    }

    private TableShape ParseTable(XElement frameEl, XElement graphicData)
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

    private void ParseTableContent(XElement tbl, TableShape shape)
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
        foreach (var col in grid?.Elements(DmlNames.GridColumn) ?? [])
        {
            var w = col.GetAttrLong(DmlNames.AttributeWidth, 0);
            shape.Grid.AddColumnWidth(new Emu(w));
        }

        foreach (var tr in tbl.Elements(DmlNames.TableRow))
        {
            var h = tr.GetAttrLong(DmlNames.AttributeRowHeight, 0);
            var cells = tr.Elements(DmlNames.TableCell).Select(ParseTableCell).ToList();
            shape.Grid.AddRowWithCells(new Emu(h), cells);
        }
    }

    private static TableCell ParseTableCell(XElement tcEl)
    {
        var cell = new TableCell();
        cell.ColumnSpan = tcEl.GetAttrInt("gridSpan", 1);
        cell.RowSpan = tcEl.GetAttrInt("rowSpan", 1);
        cell.IsHorizontalMergeContinuation = tcEl.GetAttrBool("hMerge") ?? false;
        cell.IsVerticalMergeContinuation = tcEl.GetAttrBool("vMerge") ?? false;

        var txBody = tcEl.Element(DmlNames.TextBody);
        if (txBody != null)
        {
            var parsedCell = TextParser.ParseTextBody(txBody);
            foreach (var para in parsedCell.Paragraphs)
                cell.TextFrame.Paragraphs.Add(para);
        }

        var tcPr = tcEl.Element(DmlNames.TableCellProperties);
        if (tcPr != null)
            FillParser.Parse(tcPr, cell.Fill);

        return cell;
    }

    private ChartShape ParseChart(XElement frameEl, XElement graphicData)
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

    // ── Group ─────────────────────────────────────────────────────────────────

    private GroupShape ParseGroup(XElement grpEl)
    {
        var shape = new GroupShape();
        ReadNonVisualProperties(grpEl.Element(PmlNames.NonVisualGroupShapeProperties), shape);

        var grpSpPr = grpEl.Element(PmlNames.GroupShapeProperties);
        if (grpSpPr != null)
            ReadGroupTransform(grpSpPr, shape);

        var innerTree = grpEl; // group's children are direct children
        foreach (var child in innerTree.Elements())
        {
            if (child.Name == PmlNames.NonVisualGroupShapeProperties) continue;
            if (child.Name == PmlNames.GroupShapeProperties) continue;
            var child2 = TryParseShape(child);
            if (child2 != null)
                shape.Children.AddParsed(child2);
        }

        shape.RawElement = grpEl;
        return shape;
    }

    // ── Connector ─────────────────────────────────────────────────────────────

    private ConnectorShape ParseConnector(XElement cxnEl)
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
                => Models.Shapes.ConnectorType.Bent,
            "curvedConnector2" or "curvedConnector3" or "curvedConnector4" or "curvedConnector5"
                => Models.Shapes.ConnectorType.Curved,
            _ => Models.Shapes.ConnectorType.Straight
        };

        shape.RawElement = cxnEl;
        return shape;
    }

    // ── Shared read helpers ───────────────────────────────────────────────────

    private static void ReadNonVisualProperties(XElement? nvPrContainer, Shape shape)
    {
        if (nvPrContainer == null) return;
        var cNvPr = nvPrContainer.Element(PmlNames.CommonNonVisualProperties);
        if (cNvPr == null) return;

        var id = cNvPr.GetAttrInt(PmlNames.AttributeId);
        if (id.HasValue) shape.ShapeId = (uint)id.Value;

        shape.Name = cNvPr.GetAttr(PmlNames.AttributeName, string.Empty);
        shape.AltText = cNvPr.GetAttr(DmlNames.AttributeDescription);
    }

    private static void ReadTransform(XElement? spPr, Shape shape)
    {
        if (spPr == null) return;
        var xfrm = spPr.Element(DmlNames.Transform);
        if (xfrm == null) return;
        ReadTransformFromXfrm(xfrm, shape);
    }

    private static void ReadFrameTransform(XElement frameEl, Shape shape)
    {
        var xfrm = frameEl.Element(DmlNames.Transform)
                   ?? frameEl.Element(PmlNames.Pml + "xfrm");
        if (xfrm != null)
            ReadTransformFromXfrm(xfrm, shape);
    }

    private static void ReadGroupTransform(XElement grpSpPr, Shape shape)
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
        if (chExt != null)
        {
            group.ChildExtentWidth = new Emu(chExt.GetAttrLong(DmlNames.AttributeWidth, 0));
            group.ChildExtentHeight = new Emu(chExt.GetAttrLong(DmlNames.AttributeHeight, 0));
        }
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

    private static void ReadGeometry(XElement? spPr, AutoShape shape)
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
    }

    private EmbeddedImage? ResolveImage(string relationshipId, XElement shapeElement)
    {
        // Image resolution uses the slide's OPC part — stored in RawElement context.
        // The SlideParser will call PostProcessImages after initial parse.
        // For now return null; images are resolved in a second pass.
        return null;
    }

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
