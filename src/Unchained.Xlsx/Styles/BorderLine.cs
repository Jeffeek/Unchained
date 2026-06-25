using Unchained.Ooxml.Drawing;
using Unchained.Xlsx.Models.Styles;

namespace Unchained.Xlsx.Styles;

/// <summary>A single border edge: its line style and colour.</summary>
public sealed class BorderLine : IEquatable<BorderLine>
{
    /// <summary>The line style of this edge.</summary>
    public BorderStyle Style { get; set; } = BorderStyle.None;

    /// <summary>The line colour, or <see langword="null" /> for the automatic default.</summary>
    public ColorSpec? Color { get; set; }

    /// <summary>An edge with no line.</summary>
    public static BorderLine None => new();

    /// <summary>Returns a shallow copy of this border line.</summary>
    public BorderLine Clone() => (BorderLine)MemberwiseClone();

    /// <inheritdoc />
    public bool Equals(BorderLine? other) =>
        other != null && Style == other.Style && Nullable.Equals(Color, other.Color);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as BorderLine);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Style, Color);
}
