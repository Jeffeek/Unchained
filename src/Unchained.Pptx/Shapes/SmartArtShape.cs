using System.Xml.Linq;

namespace Unchained.Pptx.Shapes;

/// <summary>
/// A shape that contains a SmartArt diagram. The node text is surfaced through
/// <see cref="Nodes"/> for reading and simple editing; the diagram's layout, colour, and style
/// definitions are preserved verbatim for lossless round-trips.
/// </summary>
public sealed class SmartArtShape : Shape
{
    /// <summary>
    /// The top-level diagram nodes. Each may have <see cref="SmartArtNode.Children"/> forming
    /// the diagram hierarchy. Empty when the diagram has no text nodes.
    /// </summary>
    public List<SmartArtNode> Nodes { get; } = [];

    /// <summary>
    /// All text across the diagram, one node per line (depth-first). Convenience reader.
    /// </summary>
    public string GetAllText()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var n in Nodes) Walk(n, sb);
        return sb.ToString().TrimEnd();

        static void Walk(SmartArtNode node, System.Text.StringBuilder sb)
        {
            if (!string.IsNullOrEmpty(node.Text)) sb.AppendLine(node.Text);
            foreach (var child in node.Children) Walk(child, sb);
        }
    }

    // ── Round-trip part data ────────────────────────────────────────────────
    // SmartArt is backed by up to five OPC parts referenced from the slide. Their bytes are
    // preserved so round-trips do not alter the diagram; node-text edits are applied back onto
    // the data part on save (see PresentationWriter).

    /// <summary>The parsed data-model document (<c>data*.xml</c>), used to apply text edits on save.</summary>
    internal XDocument? DiagramDataDocument { get; set; }

    /// <summary>Raw bytes of the diagram data part (<c>r:dm</c>). Internal.</summary>
    internal byte[]? DataPartData { get; set; }

    /// <summary>Raw bytes of the diagram layout-definition part (<c>r:lo</c>). Internal.</summary>
    internal byte[]? LayoutPartData { get; set; }

    /// <summary>Raw bytes of the diagram quick-style part (<c>r:qs</c>). Internal.</summary>
    internal byte[]? QuickStylePartData { get; set; }

    /// <summary>Raw bytes of the diagram colors part (<c>r:cs</c>). Internal.</summary>
    internal byte[]? ColorsPartData { get; set; }

    /// <summary>Raw bytes of the pre-rendered drawing part (MS extension, <c>r:dm</c> ext). Internal.</summary>
    internal byte[]? DrawingPartData { get; set; }

    /// <summary>Relationship ID of the diagram data part (<c>r:dm</c>). Internal.</summary>
    internal string DataRelationshipId { get; set; } = string.Empty;

    /// <summary>Relationship ID of the diagram layout part (<c>r:lo</c>). Internal.</summary>
    internal string LayoutRelationshipId { get; set; } = string.Empty;

    /// <summary>Relationship ID of the diagram quick-style part (<c>r:qs</c>). Internal.</summary>
    internal string QuickStyleRelationshipId { get; set; } = string.Empty;

    /// <summary>Relationship ID of the diagram colors part (<c>r:cs</c>). Internal.</summary>
    internal string ColorsRelationshipId { get; set; } = string.Empty;

    /// <summary>Relationship ID of the pre-rendered drawing part (<c>dsp:dataModelExt/@relId</c>). Internal.</summary>
    internal string DrawingRelationshipId { get; set; } = string.Empty;

    /// <summary>Absolute OPC part URI of the data part. Internal.</summary>
    internal string DataPartUri { get; set; } = string.Empty;

    /// <summary>Absolute OPC part URI of the layout part. Internal.</summary>
    internal string LayoutPartUri { get; set; } = string.Empty;

    /// <summary>Absolute OPC part URI of the quick-style part. Internal.</summary>
    internal string QuickStylePartUri { get; set; } = string.Empty;

    /// <summary>Absolute OPC part URI of the colors part. Internal.</summary>
    internal string ColorsPartUri { get; set; } = string.Empty;

    /// <summary>Absolute OPC part URI of the drawing part. Internal.</summary>
    internal string DrawingPartUri { get; set; } = string.Empty;
}
