using Unchained.Ooxml.Drawing;
using Unchained.Xlsx.Models;

namespace Unchained.Xlsx.Styles;

/// <summary>The borders of a cell, mapping to a <c>&lt;border&gt;</c> entry in the styles table.</summary>
public sealed class CellBorder : IEquatable<CellBorder>
{
    /// <summary>The left edge.</summary>
    public BorderLine Left { get; set; } = BorderLine.None;

    /// <summary>The right edge.</summary>
    public BorderLine Right { get; set; } = BorderLine.None;

    /// <summary>The top edge.</summary>
    public BorderLine Top { get; set; } = BorderLine.None;

    /// <summary>The bottom edge.</summary>
    public BorderLine Bottom { get; set; } = BorderLine.None;

    /// <summary>The diagonal line style, shared by both diagonals.</summary>
    public BorderLine Diagonal { get; set; } = BorderLine.None;

    /// <summary>Whether a diagonal line runs from lower-left to upper-right.</summary>
    public bool DiagonalUp { get; set; }

    /// <summary>Whether a diagonal line runs from upper-left to lower-right.</summary>
    public bool DiagonalDown { get; set; }

    /// <inheritdoc />
    public bool Equals(CellBorder? other) =>
        other != null &&
        Left.Equals(other.Left) &&
        Right.Equals(other.Right) &&
        Top.Equals(other.Top) &&
        Bottom.Equals(other.Bottom) &&
        Diagonal.Equals(other.Diagonal) &&
        DiagonalUp == other.DiagonalUp &&
        DiagonalDown == other.DiagonalDown;

    /// <summary>Sets all four edges to the same style and colour and returns this border.</summary>
    public CellBorder SetAllEdges(BorderStyle style, ColorSpec? color = null)
    {
        Left = new BorderLine { Style = style, Color = color };
        Right = Left.Clone();
        Top = Left.Clone();
        Bottom = Left.Clone();
        return this;
    }

    /// <summary>Returns a deep copy of this border.</summary>
    public CellBorder Clone() =>
        new()
        {
            Left = Left.Clone(),
            Right = Right.Clone(),
            Top = Top.Clone(),
            Bottom = Bottom.Clone(),
            Diagonal = Diagonal.Clone(),
            DiagonalUp = DiagonalUp,
            DiagonalDown = DiagonalDown
        };

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as CellBorder);

    /// <inheritdoc />
    public override int GetHashCode() =>
        // ReSharper disable NonReadonlyMemberInGetHashCode
        // ReSharper disable BadListLineBreaks
        HashCode.Combine(
            Left,
            Right,
            Top,
            Bottom,
            Diagonal,
            DiagonalUp,
            DiagonalDown
        );
    // ReSharper restore BadListLineBreaks
    // ReSharper restore NonReadonlyMemberInGetHashCode
}
