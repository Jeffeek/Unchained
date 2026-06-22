using System.Xml.Linq;
using Shouldly;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Parsing;
using Unchained.Pptx.Shapes;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Parsing;

public sealed class SmartArtParserTests
{
    private static readonly XNamespace A = DmlNames.Dml;

    private static XElement Point(string modelId, string? type = null, string? text = null)
    {
        var pt = new XElement(DmlNames.DiagramPoint, new XAttribute("modelId", modelId));
        if (type != null) pt.Add(new XAttribute("type", type));
        if (text != null)
        {
            pt.Add(
                new XElement(
                    DmlNames.DiagramText,
                    new XElement(A + "p", new XElement(A + "r", new XElement(A + "t", text)))
                )
            );
        }

        return pt;
    }

    // ReSharper disable once BadListLineBreaks
    private static XElement Connection(string srcId, string destId, int order, string? type = null)
    {
        var cxn = new XElement(
            DmlNames.DiagramConnection,
            new XAttribute("srcId", srcId),
            new XAttribute("destId", destId),
            new XAttribute("srcOrd", order)
        );
        if (type != null) cxn.Add(new XAttribute("type", type));
        return cxn;
    }

    private static XElement Model(IEnumerable<XElement> points, IEnumerable<XElement> connections) =>
        new(
            DmlNames.DiagramDataModel,
            new XElement(DmlNames.DiagramPointList, points),
            new XElement(DmlNames.DiagramConnectionList, connections)
        );

    [Fact]
    public void Parse_NoPointList_LeavesNodesEmpty()
    {
        var shape = new SmartArtShape();
        SmartArtParser.Parse(new XElement(DmlNames.DiagramDataModel), shape);
        shape.Nodes.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_NoDocPoint_LeavesNodesEmpty()
    {
        var shape = new SmartArtShape();
        var model = Model([Point("a", text: "orphan")], []);
        SmartArtParser.Parse(model, shape);
        shape.Nodes.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_BuildsNestedHierarchy()
    {
        var points = new[]
        {
            Point("root", "doc"),
            Point("a", text: "Parent"),
            Point("b", text: "Child"),
            Point("c", text: "Grandchild")
        };
        var connections = new[]
        {
            Connection("root", "a", 0),
            Connection("a", "b", 0),
            Connection("b", "c", 0)
        };

        var shape = new SmartArtShape();
        SmartArtParser.Parse(Model(points, connections), shape);

        shape.Nodes.Count.ShouldBe(1);
        shape.Nodes[0].Text.ShouldBe("Parent");
        shape.Nodes[0].Children.Count.ShouldBe(1);
        shape.Nodes[0].Children[0].Text.ShouldBe("Child");
        shape.Nodes[0].Children[0].Children[0].Text.ShouldBe("Grandchild");
    }

    [Fact]
    public void Parse_OrdersChildrenBySrcOrd()
    {
        var points = new[]
        {
            Point("root", "doc"),
            Point("a", text: "First"),
            Point("b", text: "Second")
        };
        var connections = new[]
        {
            Connection("root", "b", 1),
            Connection("root", "a", 0)
        };

        var shape = new SmartArtShape();
        SmartArtParser.Parse(Model(points, connections), shape);

        shape.Nodes.Select(static n => n.Text).ShouldBe(["First", "Second"]);
    }

    [Fact]
    public void Parse_SkipsTypedConnections()
    {
        var points = new[] { Point("root", "doc"), Point("a", text: "Node") };
        var connections = new[] { Connection("root", "a", 0, "presOf") };

        var shape = new SmartArtShape();
        SmartArtParser.Parse(Model(points, connections), shape);

        shape.Nodes.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_MultiParagraphText_JoinsWithNewline()
    {
        var pt = new XElement(
            DmlNames.DiagramPoint,
            new XAttribute("modelId", "a"),
            new XElement(
                DmlNames.DiagramText,
                new XElement(A + "p", new XElement(A + "r", new XElement(A + "t", "Line1"))),
                new XElement(A + "p", new XElement(A + "r", new XElement(A + "t", "Line2")))
            )
        );
        var model = Model([Point("root", "doc"), pt], [Connection("root", "a", 0)]);

        var shape = new SmartArtShape();
        SmartArtParser.Parse(model, shape);

        shape.Nodes[0].Text.ShouldBe("Line1\nLine2");
    }

    [Fact]
    public void ApplyTextEdits_NoPointList_IsNoOp()
    {
        var node = new SmartArtNode { Text = "x" };
        // ReSharper disable once AccessToModifiedClosure
        Should.NotThrow(() => SmartArtParser.ApplyTextEdits(new XElement(DmlNames.DiagramDataModel), [node]));
    }

    [Fact]
    public void ApplyTextEdits_ExistingText_ReplacesParagraphs()
    {
        var pt = Point("a", text: "Original");
        var model = Model([pt], []);

        var shape = new SmartArtShape();
        SmartArtParser.Parse(Model([Point("root", "doc"), Point("a", text: "Original")], [Connection("root", "a", 0)]), shape);
        var node = shape.Nodes[0];
        node.Text = "Updated";

        SmartArtParser.ApplyTextEdits(model, [node]);

        var t = pt.Element(DmlNames.DiagramText);
        t.ShouldNotBeNull();
        t.Elements(A + "p").Count().ShouldBe(1);
        t.Descendants(A + "t").First().Value.ShouldBe("Updated");
    }

    [Fact]
    public void ApplyTextEdits_MissingTextElement_CreatesOne()
    {
        var pt = new XElement(DmlNames.DiagramPoint, new XAttribute("modelId", "a"));
        var model = Model([pt], []);
        var node = new SmartArtNode { ModelId = "a", Text = "Brand New" };

        SmartArtParser.ApplyTextEdits(model, [node]);

        var t = pt.Element(DmlNames.DiagramText);
        t.ShouldNotBeNull();
        t.Element(A + "bodyPr").ShouldNotBeNull();
        t.Descendants(A + "t").First().Value.ShouldBe("Brand New");
    }

    [Fact]
    public void ApplyTextEdits_MultilineText_CreatesParagraphPerLine()
    {
        var pt = new XElement(DmlNames.DiagramPoint, new XAttribute("modelId", "a"));
        var model = Model([pt], []);
        var node = new SmartArtNode { ModelId = "a", Text = "A\nB\nC" };

        SmartArtParser.ApplyTextEdits(model, [node]);

        pt.Element(DmlNames.DiagramText)!.Elements(A + "p").Count().ShouldBe(3);
    }

    [Fact]
    public void ApplyTextEdits_FlattensNestedNodes()
    {
        var ptParent = new XElement(DmlNames.DiagramPoint, new XAttribute("modelId", "p"));
        var ptChild = new XElement(DmlNames.DiagramPoint, new XAttribute("modelId", "c"));
        var model = Model([ptParent, ptChild], []);

        var parent = new SmartArtNode { ModelId = "p", Text = "PV" };
        var child = new SmartArtNode { ModelId = "c", Text = "CV" };
        parent.Children.Add(child);

        SmartArtParser.ApplyTextEdits(model, [parent]);

        ptParent.Descendants(A + "t").First().Value.ShouldBe("PV");
        ptChild.Descendants(A + "t").First().Value.ShouldBe("CV");
    }

    [Fact]
    public void ApplyTextEdits_NodeWithoutModelId_IsSkipped()
    {
        var pt = new XElement(DmlNames.DiagramPoint, new XAttribute("modelId", "a"));
        var model = Model([pt], []);
        var node = new SmartArtNode { Text = "ignored" };

        SmartArtParser.ApplyTextEdits(model, [node]);

        pt.Element(DmlNames.DiagramText).ShouldBeNull();
    }
}
