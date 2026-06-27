using Unchained.Xlsx.Models;

namespace Unchained.Xlsx.Styles;

/// <summary>The alignment and text-flow settings of a cell.</summary>
public sealed class CellAlignment : IEquatable<CellAlignment>
{
    /// <summary>Horizontal alignment.</summary>
    public HorizontalAlignment Horizontal { get; set; } = HorizontalAlignment.General;

    /// <summary>Vertical alignment.</summary>
    public VerticalAlignment Vertical { get; set; } = VerticalAlignment.Bottom;

    /// <summary>Whether text wraps within the cell.</summary>
    public bool WrapText { get; set; }

    /// <summary>Whether text shrinks to fit the cell width.</summary>
    public bool ShrinkToFit { get; set; }

    /// <summary>
    ///     Text rotation as encoded by SpreadsheetML: 0 = horizontal; 1–90 = degrees counter-clockwise;
    ///     91–180 = degrees clockwise (91 = 1° CW … 180 = 90° CW); 255 = vertically stacked text.
    /// </summary>
    public int TextRotation { get; set; }

    /// <summary>The indent level (in indent units).</summary>
    public int Indent { get; set; }

    /// <summary>The reading order (text direction).</summary>
    public ReadingOrder ReadingOrder { get; set; } = ReadingOrder.ContextDependent;

    /// <summary>Whether the last line is justified (for justified horizontal alignment).</summary>
    public bool JustifyLastLine { get; set; }

    /// <summary>Whether this alignment differs from the default and therefore needs serializing.</summary>
    internal bool IsDefault =>
        Horizontal == HorizontalAlignment.General &&
        Vertical == VerticalAlignment.Bottom &&
        !WrapText && !ShrinkToFit && TextRotation == 0 && Indent == 0 &&
        ReadingOrder == ReadingOrder.ContextDependent && !JustifyLastLine;

    /// <inheritdoc />
    public bool Equals(CellAlignment? other) =>
        other != null &&
        Horizontal == other.Horizontal &&
        Vertical == other.Vertical &&
        WrapText == other.WrapText &&
        ShrinkToFit == other.ShrinkToFit &&
        TextRotation == other.TextRotation &&
        Indent == other.Indent &&
        ReadingOrder == other.ReadingOrder &&
        JustifyLastLine == other.JustifyLastLine;

    /// <summary>Returns a shallow copy of this alignment.</summary>
    public CellAlignment Clone() => (CellAlignment)MemberwiseClone();

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as CellAlignment);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(
            // ReSharper disable NonReadonlyMemberInGetHashCode
            Horizontal,
            Vertical,
            WrapText,
            ShrinkToFit,
            TextRotation,
            Indent,
            ReadingOrder,
            JustifyLastLine
            // ReSharper restore NonReadonlyMemberInGetHashCode
        );
}
