using Unchained.Pptx.Core.Xml;
using Unchained.Ooxml.Drawing;
using System.Xml.Linq;
using Unchained.Ooxml;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Shapes;

namespace Unchained.Pptx.Writing;

/// <summary>
/// Serializes <see cref="Shape"/> objects to PresentationML XML elements.
/// </summary>
internal static class ShapeWriter
{
    /// <summary>
    /// Returns the PresentationML element(s) for <paramref name="shape"/>.
    /// Unknown types return <see langword="null"/> and are skipped by the caller.
    /// </summary>
    public static XElement? Write(Shape shape) => shape switch
    {
        AutoShape auto => WriteAutoShape(auto),
        PictureShape pic => WritePicture(pic),
        TableShape table => WriteTable(table),
        ConnectorShape connector => WriteConnector(connector),
        GroupShape group => WriteGroup(group),
        ChartShape chart => WriteChart(chart),
        _ => shape.RawElement // preserve unknown shape types verbatim
    };

    // ── AutoShape / TextBox ───────────────────────────────────────────────────

    private static XElement WriteAutoShape(AutoShape shape)
    {
        var spEl = new XElement(PmlNames.Shape);

        // Non-visual properties
        var nvSpPr = new XElement(PmlNames.NonVisualShapeProperties);
        nvSpPr.Add(WriteCommonNonVisualProperties(shape));
        var cNvSpPr = new XElement(PmlNames.Pml + "cNvSpPr");
        if (shape.IsTextBox)
            cNvSpPr.Add(new XAttribute("txBox", "1"));
        nvSpPr.Add(cNvSpPr);
        nvSpPr.Add(new XElement(PmlNames.ApplicationNonVisualProperties));
        spEl.Add(nvSpPr);

        // Shape properties
        spEl.Add(WriteShapeProperties(shape));

        // Text body
        spEl.Add(TextWriter.WriteAsShape(shape.TextFrame));

        return spEl;
    }

    // ── Picture ───────────────────────────────────────────────────────────────

    private static XElement WritePicture(PictureShape shape)
    {
        var picEl = new XElement(PmlNames.Picture);

        var nvPicPr = new XElement(PmlNames.NonVisualPictureProperties);
        nvPicPr.Add(WriteCommonNonVisualProperties(shape));
        nvPicPr.Add(new XElement(PmlNames.Pml + "cNvPicPr",
            new XElement(DmlNames.Dml + "picLocks",
                new XAttribute("noChangeAspect", "1"))));
        nvPicPr.Add(new XElement(PmlNames.ApplicationNonVisualProperties));
        picEl.Add(nvPicPr);

        // Blip fill
        var blipFill = new XElement(PmlNames.BlipFill);
        if (shape.Image != null && !string.IsNullOrEmpty(shape.Image.RelationshipId))
        {
            var blip = new XElement(DmlNames.Blip,
                new XAttribute(PmlNames.RelationshipId, shape.Image.RelationshipId));
            blipFill.Add(blip);
        }
        blipFill.Add(new XElement(DmlNames.Stretch,
            new XElement(DmlNames.FillRect)));
        picEl.Add(blipFill);

        // Shape properties (no fill/line for pictures)
        var spPr = new XElement(PmlNames.ShapeProperties);
        WriteTransformToElement(spPr, shape);
        spPr.Add(new XElement(DmlNames.PresetGeometry,
            new XAttribute(DmlNames.AttributePreset, "rect"),
            new XElement(DmlNames.AdjustValueList)));
        picEl.Add(spPr);

        return picEl;
    }

    // ── Table ─────────────────────────────────────────────────────────────────

    private static XElement WriteTable(TableShape shape)
    {
        var frameEl = new XElement(PmlNames.GraphicFrame);

        var nvGraphicFramePr = new XElement(PmlNames.NonVisualGraphicFrameProperties);
        nvGraphicFramePr.Add(WriteCommonNonVisualProperties(shape));
        nvGraphicFramePr.Add(new XElement(PmlNames.Pml + "cNvGraphicFramePr",
            new XElement(DmlNames.Dml + "graphicFrameLocks",
                new XAttribute("noGrp", "1"))));
        nvGraphicFramePr.Add(new XElement(PmlNames.ApplicationNonVisualProperties));
        frameEl.Add(nvGraphicFramePr);

        // Frame transform uses p:xfrm
        var xfrm = new XElement(PmlNames.Pml + "xfrm");
        xfrm.Add(new XElement(DmlNames.Offset,
            new XAttribute(DmlNames.AttributeX, shape.X.Value),
            new XAttribute(DmlNames.AttributeY, shape.Y.Value)));
        xfrm.Add(new XElement(DmlNames.Extents,
            new XAttribute(DmlNames.AttributeWidth, shape.Width.Value),
            new XAttribute(DmlNames.AttributeHeight, shape.Height.Value)));
        frameEl.Add(xfrm);

        // Graphic data
        var graphic = new XElement(DmlNames.Graphic);
        var graphicData = new XElement(DmlNames.GraphicData,
            new XAttribute(DmlNames.AttributeUri, DmlNames.GraphicDataTableUri));

        graphicData.Add(WriteTableElement(shape));
        graphic.Add(graphicData);
        frameEl.Add(graphic);

        return frameEl;
    }

    private static XElement WriteTableElement(TableShape shape)
    {
        var tbl = new XElement(DmlNames.Table);
        var tblPr = new XElement(DmlNames.TableProperties,
            new XAttribute("firstRow", shape.HasHeaderRow ? "1" : "0"),
            new XAttribute("lastRow", shape.HasTotalRow ? "1" : "0"),
            new XAttribute("bandRow", shape.HasBandedRows ? "1" : "0"),
            new XAttribute("bandCol", shape.HasBandedColumns ? "1" : "0"),
            new XAttribute("firstCol", shape.HasFirstColumn ? "1" : "0"),
            new XAttribute("lastCol", shape.HasLastColumn ? "1" : "0"));
        tbl.Add(tblPr);

        // Column grid
        var tblGrid = new XElement(DmlNames.TableGrid);
        foreach (var width in shape.Grid.ColumnWidths)
            tblGrid.Add(new XElement(DmlNames.GridColumn,
                new XAttribute(DmlNames.AttributeWidth, width.Value)));
        tbl.Add(tblGrid);

        // Rows
        for (var r = 0; r < shape.Grid.RowCount; r++)
        {
            var tr = new XElement(DmlNames.TableRow,
                new XAttribute(DmlNames.AttributeRowHeight, shape.Grid.RowHeights[r].Value));

            for (var c = 0; c < shape.Grid.ColumnCount; c++)
            {
                var cell = shape.Grid[c, r];
                var tc = new XElement(DmlNames.TableCell);

                if (cell.ColumnSpan > 1)
                    tc.Add(new XAttribute("gridSpan", cell.ColumnSpan));
                if (cell.RowSpan > 1)
                    tc.Add(new XAttribute("rowSpan", cell.RowSpan));
                if (cell.IsHorizontalMergeContinuation)
                    tc.Add(new XAttribute("hMerge", "1"));
                if (cell.IsVerticalMergeContinuation)
                    tc.Add(new XAttribute("vMerge", "1"));

                tc.Add(TextWriter.WriteAsDml(cell.TextFrame));

                var tcPr = new XElement(DmlNames.TableCellProperties);
                FillWriter.Write(tcPr, cell.Fill);
                tc.Add(tcPr);

                tr.Add(tc);
            }

            tbl.Add(tr);
        }

        return tbl;
    }

    // ── Connector ─────────────────────────────────────────────────────────────

    private static XElement WriteConnector(ConnectorShape shape)
    {
        // If we have raw XML (parsed from file), use it verbatim
        if (shape.RawElement != null)
            return shape.RawElement;

        var cxnEl = new XElement(PmlNames.Connector);

        var nvCxnSpPr = new XElement(PmlNames.NonVisualConnectorProperties);
        nvCxnSpPr.Add(WriteCommonNonVisualProperties(shape));
        nvCxnSpPr.Add(new XElement(PmlNames.Pml + "cNvCxnSpPr"));
        nvCxnSpPr.Add(new XElement(PmlNames.ApplicationNonVisualProperties));
        cxnEl.Add(nvCxnSpPr);

        var spPr = new XElement(PmlNames.ShapeProperties);
        WriteTransformToElement(spPr, shape);

        var prst = shape.ConnectorType switch
        {
            Models.Shapes.ConnectorType.Bent => "bentConnector3",
            Models.Shapes.ConnectorType.Curved => "curvedConnector3",
            _ => "straightConnector1"
        };
        spPr.Add(new XElement(DmlNames.PresetGeometry,
            new XAttribute(DmlNames.AttributePreset, prst),
            new XElement(DmlNames.AdjustValueList)));

        LineWriter.Write(spPr, shape.Line);
        cxnEl.Add(spPr);

        return cxnEl;
    }

    // ── Group ─────────────────────────────────────────────────────────────────

    private static XElement WriteGroup(GroupShape shape)
    {
        var grpEl = new XElement(PmlNames.GroupShape);

        var nvGrpSpPr = new XElement(PmlNames.NonVisualGroupShapeProperties);
        nvGrpSpPr.Add(WriteCommonNonVisualProperties(shape));
        nvGrpSpPr.Add(new XElement(PmlNames.Pml + "cNvGrpSpPr"));
        nvGrpSpPr.Add(new XElement(PmlNames.ApplicationNonVisualProperties));
        grpEl.Add(nvGrpSpPr);

        var grpSpPr = new XElement(PmlNames.GroupShapeProperties);
        WriteTransformToElement(grpSpPr, shape);
        grpEl.Add(grpSpPr);

        foreach (var child in shape.Children)
        {
            var childEl = Write(child);
            if (childEl != null)
                grpEl.Add(childEl);
        }

        return grpEl;
    }

    // ── Chart (round-trip) ────────────────────────────────────────────────────

    private static XElement WriteChart(ChartShape shape) =>
        shape.RawElement ?? new XElement(PmlNames.GraphicFrame);

    // ── Shared helpers ─────────────────────────────────────────────────────────

    private static XElement WriteCommonNonVisualProperties(Shape shape)
    {
        var cNvPr = new XElement(PmlNames.CommonNonVisualProperties,
            new XAttribute(PmlNames.AttributeId, shape.ShapeId),
            new XAttribute(PmlNames.AttributeName, shape.Name));

        if (!string.IsNullOrEmpty(shape.AltText))
            cNvPr.Add(new XAttribute(DmlNames.AttributeDescription, shape.AltText));

        return cNvPr;
    }

    private static XElement WriteShapeProperties(Shape shape)
    {
        var spPr = new XElement(PmlNames.ShapeProperties);
        WriteTransformToElement(spPr, shape);

        if (shape is AutoShape auto)
        {
            var prstName = PresetGeometryToString(auto.ShapeType);
            spPr.Add(new XElement(DmlNames.PresetGeometry,
                new XAttribute(DmlNames.AttributePreset, prstName),
                new XElement(DmlNames.AdjustValueList)));
        }

        FillWriter.Write(spPr, shape.Fill);
        LineWriter.Write(spPr, shape.Line);
        return spPr;
    }

    private static void WriteTransformToElement(XElement parent, Shape shape)
    {
        var xfrm = new XElement(DmlNames.Transform);

        if (shape.RotationDegrees != 0)
            xfrm.Add(new XAttribute(DmlNames.AttributeRotation,
                OoXmlHelper.DegreesToOoxmlRotation(shape.RotationDegrees)));

        if (shape.FlipHorizontal)
            xfrm.Add(new XAttribute(DmlNames.AttributeFlipHorizontal, "1"));

        if (shape.FlipVertical)
            xfrm.Add(new XAttribute(DmlNames.AttributeFlipVertical, "1"));

        xfrm.Add(new XElement(DmlNames.Offset,
            new XAttribute(DmlNames.AttributeX, shape.X.Value),
            new XAttribute(DmlNames.AttributeY, shape.Y.Value)));

        xfrm.Add(new XElement(DmlNames.Extents,
            new XAttribute(DmlNames.AttributeWidth, shape.Width.Value),
            new XAttribute(DmlNames.AttributeHeight, shape.Height.Value)));

        parent.Add(xfrm);
    }

    private static string PresetGeometryToString(Models.Shapes.AutoShapeType type) => type switch
    {
        Models.Shapes.AutoShapeType.RoundedRectangle => "roundRect",
        Models.Shapes.AutoShapeType.Ellipse => "ellipse",
        Models.Shapes.AutoShapeType.IsoscelesTriangle => "triangle",
        Models.Shapes.AutoShapeType.RightTriangle => "rtTriangle",
        Models.Shapes.AutoShapeType.Diamond => "diamond",
        Models.Shapes.AutoShapeType.Parallelogram => "parallelogram",
        Models.Shapes.AutoShapeType.Trapezoid => "trapezoid",
        Models.Shapes.AutoShapeType.Pentagon => "pentagon",
        Models.Shapes.AutoShapeType.Hexagon => "hexagon",
        Models.Shapes.AutoShapeType.Heptagon => "heptagon",
        Models.Shapes.AutoShapeType.Octagon => "octagon",
        Models.Shapes.AutoShapeType.Star4 => "star4",
        Models.Shapes.AutoShapeType.Star5 => "star5",
        Models.Shapes.AutoShapeType.Star6 => "star6",
        Models.Shapes.AutoShapeType.Star8 => "star8",
        Models.Shapes.AutoShapeType.RightArrow => "rightArrow",
        Models.Shapes.AutoShapeType.LeftArrow => "leftArrow",
        Models.Shapes.AutoShapeType.UpArrow => "upArrow",
        Models.Shapes.AutoShapeType.DownArrow => "downArrow",
        Models.Shapes.AutoShapeType.Plus => "plus",
        Models.Shapes.AutoShapeType.Donut => "donut",
        Models.Shapes.AutoShapeType.Heart => "heart",
        Models.Shapes.AutoShapeType.LightningBolt => "lightningBolt",
        Models.Shapes.AutoShapeType.Sun => "sun",
        Models.Shapes.AutoShapeType.Moon => "moon",
        Models.Shapes.AutoShapeType.Cloud => "cloud",
        Models.Shapes.AutoShapeType.Arc => "arc",
        Models.Shapes.AutoShapeType.Wave => "wave",
        Models.Shapes.AutoShapeType.FlowChartProcess => "flowChartProcess",
        Models.Shapes.AutoShapeType.FlowChartDecision => "flowChartDecision",
        Models.Shapes.AutoShapeType.FlowChartTerminator => "flowChartTerminator",
        Models.Shapes.AutoShapeType.MathPlus => "mathPlus",
        Models.Shapes.AutoShapeType.MathMinus => "mathMinus",
        Models.Shapes.AutoShapeType.MathMultiply => "mathMultiply",
        Models.Shapes.AutoShapeType.MathDivide => "mathDivide",
        Models.Shapes.AutoShapeType.MathEqual => "mathEqual",
        Models.Shapes.AutoShapeType.MathNotEqual => "mathNotEqual",
        _ => "rect"
    };
}
