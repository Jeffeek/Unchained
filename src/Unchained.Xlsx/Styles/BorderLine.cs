using Unchained.Ooxml.Drawing;
using Unchained.Xlsx.Models;

namespace Unchained.Xlsx.Styles;

/// <summary>A single border edge: its line style and colour.</summary>
public sealed class BorderLine : IEquatable<BorderLine>
{
    /// <summary>The line style of this edge.</summary>
    public BorderStyle Style { get; init; } = BorderStyle.None;

    /// <summary>The line colour, or <see langword="null" /> for the automatic default.</summary>
    public ColorSpec? Color { get; init; }

    /// <summary>An edge with no line.</summary>
    public static BorderLine None => new();

    /// <inheritdoc />
    public bool Equals(BorderLine? other) =>
        other != null && Style == other.Style && Nullable.Equals(Color, other.Color);

    /// <summary>Returns a shallow copy of this border line.</summary>
    public BorderLine Clone() => (BorderLine)MemberwiseClone();

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as BorderLine);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Style, Color);
}
