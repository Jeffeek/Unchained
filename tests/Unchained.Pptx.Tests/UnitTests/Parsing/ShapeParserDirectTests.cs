using System.Xml.Linq;
using Shouldly;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Parsing;
using Unchained.Pptx.Shapes;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Parsing;

/// <summary>
///     Branch coverage for <see cref="ShapeParser" /> driven directly with hand-built shape trees:
///     every preset-geometry token, custom geometry, placeholder-type tokens, connector-type tokens,
///     alt-text / decorative / hyperlink present-vs-absent, table cell borders, and unknown graphic
///     frames.
/// </summary>
public sealed class ShapeParserDirectTests
{
    private static readonly XNamespace P = PmlNames.Pml;
    private static readonly XNamespace A = DmlNames.Dml;
    private static readonly XNamespace R = PmlNames.Relationships;

    private static ShapeCollection ParseTree(params XElement[] children)
    {
        var spTree = new XElement(PmlNames.ShapeTree, children.Cast<object>().ToArray());
        var collection = new ShapeCollection();
        new ShapeParser().ParseTree(spTree, collection);
        return collection;
    }

    private static XElement AutoShape(XElement spPr, XElement? nvSpPr = null, XElement? txBody = null)
    {
        var sp = new XElement(PmlNames.Shape);
        sp.Add(nvSpPr ?? new XElement(PmlNames.NonVisualShapeProperties));
        sp.Add(spPr);
        if (txBody != null) sp.Add(txBody);
        return sp;
    }

    private static XElement SpPrWithPreset(string prst) =>
        new(PmlNames.ShapeProperties, new XElement(DmlNames.PresetGeometry, new XAttribute("prst", prst)));

    // ── Preset geometry map ────────────────────────────────────────────────────

    [
        Theory,
        InlineData("rect", AutoShapeType.Rectangle),
        InlineData("roundRect", AutoShapeType.RoundedRectangle),
        InlineData("ellipse", AutoShapeType.Ellipse),
        InlineData("triangle", AutoShapeType.IsoscelesTriangle),
        InlineData("rtTriangle", AutoShapeType.RightTriangle),
        InlineData("diamond", AutoShapeType.Diamond),
        InlineData("parallelogram", AutoShapeType.Parallelogram),
        InlineData("trapezoid", AutoShapeType.Trapezoid),
        InlineData("pentagon", AutoShapeType.Pentagon),
        InlineData("hexagon", AutoShapeType.Hexagon),
        InlineData("heptagon", AutoShapeType.Heptagon),
        InlineData("octagon", AutoShapeType.Octagon),
        InlineData("decagon", AutoShapeType.Decagon),
        InlineData("dodecagon", AutoShapeType.Dodecagon),
        InlineData("star4", AutoShapeType.Star4),
        InlineData("star5", AutoShapeType.Star5),
        InlineData("star6", AutoShapeType.Star6),
        InlineData("star7", AutoShapeType.Star7),
        InlineData("star8", AutoShapeType.Star8),
        InlineData("star10", AutoShapeType.Star10),
        InlineData("star12", AutoShapeType.Star12),
        InlineData("star16", AutoShapeType.Star16),
        InlineData("star24", AutoShapeType.Star24),
        InlineData("star32", AutoShapeType.Star32),
        InlineData("rightArrow", AutoShapeType.RightArrow),
        InlineData("leftArrow", AutoShapeType.LeftArrow),
        InlineData("upArrow", AutoShapeType.UpArrow),
        InlineData("downArrow", AutoShapeType.DownArrow),
        InlineData("leftRightArrow", AutoShapeType.LeftRightArrow),
        InlineData("upDownArrow", AutoShapeType.UpDownArrow),
        InlineData("plus", AutoShapeType.Plus),
        InlineData("donut", AutoShapeType.Donut),
        InlineData("noSmoking", AutoShapeType.NoSymbol),
        InlineData("cube", AutoShapeType.Cube),
        InlineData("can", AutoShapeType.Can),
        InlineData("bevel", AutoShapeType.Bevel),
        InlineData("foldedCorner", AutoShapeType.FoldedCorner),
        InlineData("smileyFace", AutoShapeType.SmileyFace),
        InlineData("heart", AutoShapeType.Heart),
        InlineData("lightningBolt", AutoShapeType.LightningBolt),
        InlineData("sun", AutoShapeType.Sun),
        InlineData("moon", AutoShapeType.Moon),
        InlineData("cloud", AutoShapeType.Cloud),
        InlineData("arc", AutoShapeType.Arc),
        InlineData("wave", AutoShapeType.Wave),
        InlineData("doubleWave", AutoShapeType.DoubleWave),
        InlineData("callout1", AutoShapeType.RectangularCallout),
        InlineData("roundedRectCallout", AutoShapeType.RoundedRectangularCallout),
        InlineData("ellipseCallout", AutoShapeType.OvalCallout),
        InlineData("cloudCallout", AutoShapeType.CloudCallout),
        InlineData("flowChartProcess", AutoShapeType.FlowChartProcess),
        InlineData("flowChartDecision", AutoShapeType.FlowChartDecision),
        InlineData("flowChartInputOutput", AutoShapeType.FlowChartInputOutput),
        InlineData("flowChartTerminator", AutoShapeType.FlowChartTerminator),
        InlineData("flowChartDocument", AutoShapeType.FlowChartDocument),
        InlineData("flowChartConnector", AutoShapeType.FlowChartConnector),
        InlineData("mathPlus", AutoShapeType.MathPlus),
        InlineData("mathMinus", AutoShapeType.MathMinus),
        InlineData("mathMultiply", AutoShapeType.MathMultiply),
        InlineData("mathDivide", AutoShapeType.MathDivide),
        InlineData("mathEqual", AutoShapeType.MathEqual),
        InlineData("mathNotEqual", AutoShapeType.MathNotEqual),
        InlineData("line", AutoShapeType.Line),
        InlineData("straightConnector1", AutoShapeType.StraightConnector),
        InlineData("bentConnector2", AutoShapeType.BentConnector),
        InlineData("bentConnector3", AutoShapeType.BentConnector),
        InlineData("bentConnector4", AutoShapeType.BentConnector),
        InlineData("curvedConnector2", AutoShapeType.CurvedConnector),
        InlineData("curvedConnector3", AutoShapeType.CurvedConnector),
        InlineData("curvedConnector4", AutoShapeType.CurvedConnector),
        InlineData("totallyUnknownPreset", AutoShapeType.Rectangle)
    ]
    public void ParseAutoShape_MapsPresetGeometry(string prst, AutoShapeType expected)
    {
        var shapes = ParseTree(AutoShape(SpPrWithPreset(prst)));
        shapes.OfType<AutoShape>().Single().ShapeType.ShouldBe(expected);
    }

    [Fact]
    public void ParseAutoShape_CustomGeometry_SetsCustomType()
    {
        var spPr = new XElement(PmlNames.ShapeProperties, new XElement(DmlNames.CustomGeometry));
        var shapes = ParseTree(AutoShape(spPr));
        shapes.OfType<AutoShape>().Single().ShapeType.ShouldBe(AutoShapeType.Custom);
    }

    [Fact]
    public void ParseAutoShape_NoShapeProperties_ParsesWithoutGeometry()
    {
        var sp = new XElement(PmlNames.Shape, new XElement(PmlNames.NonVisualShapeProperties));
        var shapes = ParseTree(sp);
        shapes.OfType<AutoShape>().ShouldHaveSingleItem();
    }

    // ── Placeholder types ────────────────────────────────────────────────────────

    private static XElement NvSpPrWithPlaceholder(string? type, int? idx)
    {
        var ph = new XElement(PmlNames.Placeholder);
        if (type != null) ph.Add(new XAttribute("type", type));
        if (idx.HasValue) ph.Add(new XAttribute("idx", idx.Value));
        return new XElement(
            PmlNames.NonVisualShapeProperties,
            new XElement(PmlNames.CommonNonVisualProperties, new XAttribute("id", "5"), new XAttribute("name", "PH")),
            new XElement(PmlNames.ApplicationNonVisualProperties, ph)
        );
    }

    [
        Theory,
        InlineData("title", PlaceholderType.Title),
        InlineData("ctrTitle", PlaceholderType.CenteredTitle),
        InlineData("subTitle", PlaceholderType.Subtitle),
        InlineData("body", PlaceholderType.Body),
        InlineData("obj", PlaceholderType.Object),
        InlineData("dt", PlaceholderType.Date),
        InlineData("ftr", PlaceholderType.Footer),
        InlineData("sldNum", PlaceholderType.SlideNumber),
        InlineData("hdr", PlaceholderType.Header),
        InlineData("chart", PlaceholderType.Media),
        InlineData("tbl", PlaceholderType.Media),
        InlineData("pic", PlaceholderType.Media),
        InlineData("media", PlaceholderType.Media),
        InlineData("clipArt", PlaceholderType.Media),
        InlineData("dgm", PlaceholderType.Media),
        InlineData("weird", PlaceholderType.Content)
    ]
    public void ParseAutoShape_MapsPlaceholderType(string type, PlaceholderType expected)
    {
        var shapes = ParseTree(AutoShape(SpPrWithPreset("rect"), NvSpPrWithPlaceholder(type, 3)));
        var shape = shapes.OfType<AutoShape>().Single();
        shape.PlaceholderType.ShouldBe(expected);
        shape.PlaceholderIndex.ShouldBe(3);
        shape.ShapeId.ShouldBe(5u);
        shape.Name.ShouldBe("PH");
    }

    [Fact]
    public void ParseAutoShape_PlaceholderNoTypeNoIdx_DefaultsToContent()
    {
        var shapes = ParseTree(AutoShape(SpPrWithPreset("rect"), NvSpPrWithPlaceholder(null, null)));
        shapes.OfType<AutoShape>().Single().PlaceholderType.ShouldBe(PlaceholderType.Content);
    }

    [Fact]
    public void ParseAutoShape_NoCommonNonVisualProperties_LeavesDefaults()
    {
        // nvSpPr present but missing cNvPr → ReadNonVisualProperties early-returns.
        var nvSpPr = new XElement(PmlNames.NonVisualShapeProperties);
        var shapes = ParseTree(AutoShape(SpPrWithPreset("rect"), nvSpPr));
        shapes.OfType<AutoShape>().Single().ShapeId.ShouldBe(0u);
    }

    // ── Alt text, decorative, hyperlink ──────────────────────────────────────────

    [Fact]
    public void ParseAutoShape_AltTextDecorativeAndHyperlink_AreCaptured()
    {
        var cNvPr = new XElement(
            PmlNames.CommonNonVisualProperties,
            new XAttribute("id", "9"),
            new XAttribute("name", "Hot"),
            new XAttribute("descr", "alt text"),
            new XAttribute("title", "the title"),
            new XElement(
                A + "extLst",
                new XElement(A + "ext", new XElement(A + "decorative", new XAttribute(DmlNames.AttributeValue, "1")))
            ),
            new XElement(DmlNames.HyperlinkClick, new XAttribute(R + "id", "rId3"), new XAttribute("tooltip", "go"))
        );
        var nvSpPr = new XElement(PmlNames.NonVisualShapeProperties, cNvPr);

        var shape = ParseTree(AutoShape(SpPrWithPreset("rect"), nvSpPr)).OfType<AutoShape>().Single();

        shape.AltText.ShouldBe("alt text");
        shape.AltTextTitle.ShouldBe("the title");
        shape.IsDecorative.ShouldBeTrue();
        shape.ClickAction.ShouldNotBeNull();
        shape.ClickAction!.RelationshipId.ShouldBe("rId3");
        shape.ClickAction.Tooltip.ShouldBe("go");
    }

    [Fact]
    public void ParseAutoShape_TextBox_DetectedFromTxBoxAttribute()
    {
        var nvSpPr = new XElement(
            PmlNames.NonVisualShapeProperties,
            new XElement(PmlNames.CommonNonVisualProperties, new XAttribute("id", "2"), new XAttribute("name", "TB")),
            new XElement(P + "cNvSpPr", new XAttribute("txBox", "1"))
        );
        var txBody = new XElement(
            PmlNames.TextBody,
            new XElement(A + "p", new XElement(A + "r", new XElement(A + "t", "hi")))
        );

        var shape = ParseTree(AutoShape(SpPrWithPreset("rect"), nvSpPr, txBody)).OfType<AutoShape>().Single();
        shape.IsTextBox.ShouldBeTrue();
    }

    // ── Transform reading ────────────────────────────────────────────────────────

    [Fact]
    public void ParseAutoShape_FullTransform_ReadsOffsetExtentRotationFlips()
    {
        var xfrm = new XElement(
            DmlNames.Transform,
            new XAttribute("rot", 5_400_000),
            new XAttribute("flipH", "1"),
            new XAttribute("flipV", "1"),
            new XElement(DmlNames.Offset, new XAttribute("x", 914400), new XAttribute("y", 457200)),
            new XElement(DmlNames.Extents, new XAttribute("cx", 1828800), new XAttribute("cy", 914400))
        );
        var spPr = new XElement(
            PmlNames.ShapeProperties,
            xfrm,
            new XElement(DmlNames.PresetGeometry, new XAttribute("prst", "rect"))
        );

        var shape = ParseTree(AutoShape(spPr)).OfType<AutoShape>().Single();
        shape.X.Value.ShouldBe(914400);
        shape.Width.Value.ShouldBe(1828800);
        shape.RotationDegrees.ShouldBe(90, 0.01);
        shape.FlipHorizontal.ShouldBeTrue();
        shape.FlipVertical.ShouldBeTrue();
    }

    // ── Connector types ──────────────────────────────────────────────────────────

    private static XElement Connector(string prst)
    {
        var spPr = new XElement(
            PmlNames.ShapeProperties,
            new XElement(DmlNames.PresetGeometry, new XAttribute("prst", prst))
        );
        return new XElement(
            PmlNames.Connector,
            new XElement(PmlNames.NonVisualConnectorProperties),
            spPr
        );
    }

    [
        Theory,
        InlineData("bentConnector3", ConnectorType.Bent),
        InlineData("curvedConnector3", ConnectorType.Curved),
        InlineData("straightConnector1", ConnectorType.Straight),
        InlineData("line", ConnectorType.Straight)
    ]
    public void ParseConnector_MapsConnectorType(string prst, ConnectorType expected)
    {
        var shapes = ParseTree(Connector(prst));
        shapes.OfType<ConnectorShape>().Single().ConnectorType.ShouldBe(expected);
    }

    // ── Group ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseGroup_WithTransformAndChildren()
    {
        var grpSpPr = new XElement(
            PmlNames.GroupShapeProperties,
            new XElement(
                DmlNames.Transform,
                new XElement(DmlNames.Offset, new XAttribute("x", 0), new XAttribute("y", 0)),
                new XElement(DmlNames.Extents, new XAttribute("cx", 1000), new XAttribute("cy", 1000)),
                new XElement(DmlNames.ChildOffset, new XAttribute("x", 5), new XAttribute("y", 6)),
                new XElement(DmlNames.ChildExtent, new XAttribute("cx", 2000), new XAttribute("cy", 3000))
            )
        );
        var grp = new XElement(
            PmlNames.GroupShape,
            new XElement(PmlNames.NonVisualGroupShapeProperties),
            grpSpPr,
            AutoShape(SpPrWithPreset("ellipse"))
        );

        var group = ParseTree(grp).OfType<GroupShape>().Single();
        group.Children.Count.ShouldBe(1);
        group.ChildOffsetX.Value.ShouldBe(5);
        group.ChildExtentWidth.Value.ShouldBe(2000);
    }

    [Fact]
    public void ParseGroup_NoTransform_StillParsesChildren()
    {
        var grp = new XElement(
            PmlNames.GroupShape,
            new XElement(PmlNames.NonVisualGroupShapeProperties),
            AutoShape(SpPrWithPreset("rect"))
        );
        ParseTree(grp).OfType<GroupShape>().Single().Children.Count.ShouldBe(1);
    }

    // ── Graphic frame: table / chart / smartart / unknown ────────────────────────

    private static XElement GraphicFrame(string uri, params object[] dataChildren)
    {
        var graphicData = new XElement(DmlNames.GraphicData, new XAttribute("uri", uri), dataChildren);
        return new XElement(
            PmlNames.GraphicFrame,
            new XElement(PmlNames.NonVisualGraphicFrameProperties),
            new XElement(DmlNames.Graphic, graphicData)
        );
    }

    [Fact]
    public void ParseGraphicFrame_UnknownUri_ReturnsRectangleStub()
    {
        var shapes = ParseTree(GraphicFrame("http://example.com/unknown"));
        shapes.OfType<AutoShape>().Single().ShapeType.ShouldBe(AutoShapeType.Rectangle);
    }

    [Fact]
    public void ParseGraphicFrame_Chart_CapturesRelationshipId()
    {
        var chartRef = new XElement(DmlNames.Chart + "chart", new XAttribute(R + "id", "rId8"));
        var shapes = ParseTree(GraphicFrame(DmlNames.GraphicDataChartUri, chartRef));
        shapes.OfType<ChartShape>().Single().RelationshipId.ShouldBe("rId8");
    }

    [Fact]
    public void ParseGraphicFrame_SmartArt_CapturesRelIds()
    {
        var relIds = new XElement(
            DmlNames.DiagramRelIds,
            new XAttribute(R + "dm", "rIdDm"),
            new XAttribute(R + "lo", "rIdLo"),
            new XAttribute(R + "qs", "rIdQs"),
            new XAttribute(R + "cs", "rIdCs")
        );
        var shape = ParseTree(GraphicFrame(DmlNames.GraphicDataDiagramUri, relIds)).OfType<SmartArtShape>().Single();
        shape.DataRelationshipId.ShouldBe("rIdDm");
        shape.LayoutRelationshipId.ShouldBe("rIdLo");
        shape.QuickStyleRelationshipId.ShouldBe("rIdQs");
        shape.ColorsRelationshipId.ShouldBe("rIdCs");
    }

    [Fact]
    public void ParseGraphicFrame_Table_WithPropertiesGridAndCellBorders()
    {
        var tblPr = new XElement(
            DmlNames.TableProperties,
            new XAttribute("firstRow", "1"),
            new XAttribute("lastRow", "1"),
            new XAttribute("bandRow", "1"),
            new XAttribute("bandCol", "1"),
            new XAttribute("firstCol", "1"),
            new XAttribute("lastCol", "1")
        );
        var grid = new XElement(
            DmlNames.TableGrid,
            new XElement(DmlNames.GridColumn, new XAttribute("w", 100)),
            new XElement(DmlNames.GridColumn, new XAttribute("w", 200))
        );
        var tcPr = new XElement(
            DmlNames.TableCellProperties,
            new XElement(A + "lnL", new XElement(A + "solidFill", new XElement(A + "srgbClr", new XAttribute(DmlNames.AttributeValue, "000000")))),
            new XElement(A + "lnR", new XElement(A + "solidFill", new XElement(A + "srgbClr", new XAttribute(DmlNames.AttributeValue, "111111")))),
            new XElement(A + "lnT", new XElement(A + "solidFill", new XElement(A + "srgbClr", new XAttribute(DmlNames.AttributeValue, "222222")))),
            new XElement(A + "lnB", new XElement(A + "solidFill", new XElement(A + "srgbClr", new XAttribute(DmlNames.AttributeValue, "333333"))))
        );
        var cell = new XElement(
            DmlNames.TableCell,
            new XAttribute("gridSpan", 2),
            tcPr,
            new XElement(DmlNames.TextBody, new XElement(A + "p", new XElement(A + "r", new XElement(A + "t", "x"))))
        );
        var bareCell = new XElement(DmlNames.TableCell, new XAttribute("hMerge", "1"));
        var row = new XElement(DmlNames.TableRow, new XAttribute("h", 50), cell, bareCell);
        var tbl = new XElement(DmlNames.Table, tblPr, grid, row);

        var shape = ParseTree(GraphicFrame(DmlNames.GraphicDataTableUri, tbl)).OfType<TableShape>().Single();
        shape.HasHeaderRow.ShouldBeTrue();
        shape.HasBandedColumns.ShouldBeTrue();
        shape.Grid.ColumnCount.ShouldBe(2);
        shape.Grid.RowCount.ShouldBe(1);
    }

    [Fact]
    public void ParseGraphicFrame_Table_NoTableElement_LeavesEmptyGrid()
    {
        var shape = ParseTree(GraphicFrame(DmlNames.GraphicDataTableUri)).OfType<TableShape>().Single();
        shape.Grid.RowCount.ShouldBe(0);
    }

    // ── Picture ───────────────────────────────────────────────────────────────────

    [Fact]
    public void ParsePicture_WithBlip_ParsesShape()
    {
        var pic = new XElement(
            PmlNames.Picture,
            new XElement(PmlNames.NonVisualPictureProperties),
            new XElement(PmlNames.ShapeProperties),
            new XElement(PmlNames.BlipFill, new XElement(DmlNames.Blip, new XAttribute(R + "embed", "rId4")))
        );
        ParseTree(pic).OfType<PictureShape>().ShouldHaveSingleItem();
    }

    [Fact]
    public void ParsePicture_NoBlip_StillParses()
    {
        var pic = new XElement(
            PmlNames.Picture,
            new XElement(PmlNames.NonVisualPictureProperties),
            new XElement(PmlNames.ShapeProperties)
        );
        ParseTree(pic).OfType<PictureShape>().ShouldHaveSingleItem();
    }

    [Fact]
    public void ParseTree_UnknownElement_IsSkipped()
    {
        var shapes = ParseTree(new XElement(PmlNames.NonVisualGroupShapeProperties));
        shapes.Count.ShouldBe(0);
    }
}
