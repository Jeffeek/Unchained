using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Shapes;

namespace Unchained.Pptx.Parsing;

/// <summary>
///     Parses a SmartArt diagram data model (<c>dgm:dataModel</c> from <c>data*.xml</c>) into the
///     hierarchical <see cref="SmartArtNode" /> tree carried by a <see cref="SmartArtShape" />.
/// </summary>
/// <remarks>
///     The data model stores a flat list of points (<c>dgm:pt</c>) plus a list of connections
///     (<c>dgm:cxn</c>). The visible node hierarchy is reconstructed from the connections whose
///     type is the (default) <c>parOf</c> kind — those link a parent point to a child point with a
///     sibling order (<c>srcOrd</c>). Structural points (doc / transitions / presentation) are skipped.
/// </remarks>
internal static class SmartArtParser
{
    public static void Parse(XElement dataModel, SmartArtShape shape)
    {
        var ptLst = dataModel.Element(DmlNames.DiagramPointList);
        if (ptLst == null) return;

        // Index every point by its model identifier.
        var points = new Dictionary<string, XElement>(StringComparer.Ordinal);
        XElement? docPoint = null;
        foreach (var pt in ptLst.Elements(DmlNames.DiagramPoint))
        {
            var modelId = (string?)pt.Attribute("modelId");
            if (modelId == null) continue;

            points[modelId] = pt;
            if ((string?)pt.Attribute("type") == "doc")
                docPoint = pt;
        }

        // Build parent → ordered children from the hierarchy connections (those with no explicit
        // type, i.e. the default "parOf" kind). Transition/presentation connections are ignored.
        var childLinks = new Dictionary<string, List<(int Order, string DestId)>>(StringComparer.Ordinal);
        var cxnLst = dataModel.Element(DmlNames.DiagramConnectionList);
        foreach (var cxn in cxnLst?.Elements(DmlNames.DiagramConnection) ?? [])
        {
            if ((string?)cxn.Attribute("type") != null) continue; // skip presOf/presParOf/etc.

            var srcId = (string?)cxn.Attribute("srcId");
            var destId = (string?)cxn.Attribute("destId");
            if (srcId == null || destId == null) continue;

            var order = (int?)cxn.Attribute("srcOrd") ?? 0;

            if (!childLinks.TryGetValue(srcId, out var list))
                childLinks[srcId] = list = [];
            list.Add((order, destId));
        }

        var rootId = (string?)docPoint?.Attribute("modelId");
        if (rootId == null) return;

        foreach (var node in BuildChildren(rootId, points, childLinks))
            shape.Nodes.Add(node);
    }

    private static List<SmartArtNode> BuildChildren(
        string parentId,
        IReadOnlyDictionary<string, XElement> points,
        IReadOnlyDictionary<string, List<(int Order, string DestId)>> childLinks
    )
    {
        var result = new List<SmartArtNode>();
        if (!childLinks.TryGetValue(parentId, out var children)) return result;

        foreach (var (_, destId) in children.OrderBy(static c => c.Order))
        {
            if (!points.TryGetValue(destId, out var pt)) continue;

            var node = new SmartArtNode
            {
                ModelId = destId,
                Text = ReadNodeText(pt)
            };
            foreach (var child in BuildChildren(destId, points, childLinks))
                node.Children.Add(child);

            result.Add(node);
        }

        return result;
    }

    /// <summary>Reads the visible text of a point from its <c>dgm:t</c> body (paragraphs joined by newline).</summary>
    private static string ReadNodeText(XContainer pt)
    {
        var t = pt.Element(DmlNames.DiagramText);
        if (t == null) return string.Empty;

        var paragraphs = t.Elements(DmlNames.Dml + "p")
            .Select(static p => string.Concat(
                p.Elements(DmlNames.Dml + "r").Select(static r => (string?)r.Element(DmlNames.Dml + "t") ?? string.Empty)));

        return string.Join("\n", paragraphs).TrimEnd('\n');
    }

    /// <summary>
    ///     Applies the current <see cref="SmartArtNode.Text" /> of each node back onto its matching
    ///     <c>dgm:pt</c> in <paramref name="dataModel" />, so edits are reflected on save. Only the text
    ///     of the first run of the first paragraph is updated; nodes without a model id are skipped.
    /// </summary>
    public static void ApplyTextEdits(XElement dataModel, IEnumerable<SmartArtNode> nodes)
    {
        var ptLst = dataModel.Element(DmlNames.DiagramPointList);
        if (ptLst == null) return;

        var byId = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var pt in ptLst.Elements(DmlNames.DiagramPoint))
        {
            var modelId = (string?)pt.Attribute("modelId");
            if (modelId != null) byId[modelId] = pt;
        }

        foreach (var node in Flatten(nodes).Where(static node => !string.IsNullOrEmpty(node.ModelId)))
        {
            if (!byId.TryGetValue(node.ModelId, out var pt)) continue;

            SetNodeText(pt, node.Text);
        }
    }

    private static IEnumerable<SmartArtNode> Flatten(IEnumerable<SmartArtNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;

            foreach (var child in Flatten(node.Children))
                yield return child;
        }
    }

    private static void SetNodeText(XContainer pt, string text)
    {
        var a = DmlNames.Dml;
        var t = pt.Element(DmlNames.DiagramText);
        if (t == null)
        {
            t = new XElement(DmlNames.DiagramText,
                new XElement(a + "bodyPr"),
                new XElement(a + "lstStyle"));
            pt.Add(t);
        }

        // Replace all paragraphs with one paragraph per text line.
        t.Elements(a + "p").Remove();
        var lines = text.Length == 0 ? [string.Empty] : text.Split('\n');
        foreach (var line in lines)
        {
            t.Add(new XElement(a + "p",
                new XElement(a + "r",
                    new XElement(a + "rPr", new XAttribute("lang", "en-US")),
                    new XElement(a + "t", line))));
        }
    }
}
