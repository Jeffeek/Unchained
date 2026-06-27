namespace Unchained.Xlsx.Styles;

/// <summary>A named cell style (e.g. "Normal", "Good", "Bad"), referencing a <c>cellStyleXfs</c> entry.</summary>
public sealed class NamedCellStyle
{
    /// <summary>Initialises a named style.</summary>
    public NamedCellStyle(string name, int xfId, int builtInId = -1)
    {
        Name = name;
        XfId = xfId;
        BuiltInId = builtInId;
    }

    /// <summary>The user-visible style name.</summary>
    public string Name { get; set; }

    /// <summary>Index into the <c>cellStyleXfs</c> table.</summary>
    public int XfId { get; set; }

    /// <summary>The built-in style id, or -1 when this is a custom style.</summary>
    public int BuiltInId { get; set; }

    /// <summary><see langword="true" /> when this maps to a built-in Excel style.</summary>
    public bool IsBuiltIn => BuiltInId >= 0;
}
