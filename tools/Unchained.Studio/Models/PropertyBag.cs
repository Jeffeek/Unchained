namespace Unchained.Studio.Models;

public enum PropertyValueKind
{
    Text, Number, Boolean, Hex, Bytes, Json, ObjectRef, Date, Color
}

public sealed class PropertyEntry
{
    public string Key { get; init; } = string.Empty;
    public string DisplayValue { get; init; } = string.Empty;
    public PropertyValueKind Kind { get; init; } = PropertyValueKind.Text;
    public string? LinkedNodeId { get; init; }
    public string? CopyValue { get; init; }
}

public sealed class PropertyGroup
{
    public string? Header { get; init; }
    public List<PropertyEntry> Entries { get; init; } = [];
}

public sealed class PropertyBag
{
    public string Title { get; init; } = string.Empty;
    public string? Subtitle { get; init; }
    public List<PropertyGroup> Groups { get; init; } = [];
    public string? RawText { get; init; }
    public string? RawTextLabel { get; init; }

    public static PropertyBag Empty(string title) => new() { Title = title };
}
