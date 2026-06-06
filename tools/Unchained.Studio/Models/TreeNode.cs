namespace Unchained.Studio.Models;

public enum TreeNodeType
{
    Document, Metadata, XmpMetadata,
    Pages, Page, ContentStream, Operator,
    Font, Image, XObject,
    Annotation, Bookmark, BookmarkGroup,
    FormField, FormFieldGroup,
    NamedDestination, NamedDestinationGroup,
    ViewerPreferences, PageLabels,
    Encryption, Signature,
    Slide, Shape, Master, Layout, Theme,
    Sheet, Cell,
    Generic
}

public sealed class TreeNode
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Label { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public TreeNodeType NodeType { get; init; } = TreeNodeType.Generic;
    public object? Payload { get; init; }
    public bool IsExpanded { get; set; }
    public bool IsSelected { get; set; }

    // Eagerly-provided children (for nodes whose children are cheap to enumerate)
    public List<TreeNode> Children { get; init; } = [];

    // True when this node may have children that haven't been loaded yet
    public bool HasLazyChildren { get; init; }
    public bool LazyChildrenLoaded { get; set; }
    public Func<Task<List<TreeNode>>>? LoadChildrenAsync { get; init; }

    public bool HasChildren => Children.Count > 0 || HasLazyChildren;
}
