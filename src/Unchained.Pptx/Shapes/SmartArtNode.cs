namespace Unchained.Pptx.Shapes;

/// <summary>
///     A node in a SmartArt diagram — carries the text and a model identifier, with child nodes
///     forming the diagram hierarchy. Editing the text is reflected back on save; structural layout
///     is governed by the diagram's layout part (preserved verbatim).
/// </summary>
public sealed class SmartArtNode
{
    /// <summary>The model identifier of this node (<c>dgm:pt/@modelId</c>).</summary>
    public string ModelId { get; internal set; } = string.Empty;

    /// <summary>The text shown in this node.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>The child nodes beneath this node.</summary>
    public List<SmartArtNode> Children { get; } = [];

    /// <summary>Adds a child node with the given text and returns it.</summary>
    public SmartArtNode AddChild(string text)
    {
        var child = new SmartArtNode { Text = text };
        Children.Add(child);
        return child;
    }
}
