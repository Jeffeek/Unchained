using System.Diagnostics.CodeAnalysis;

namespace Unchained.Xlsx.Models.Cell;

/// <summary>
///     An immutable rectangular range of cells, expressed in A1 notation (e.g. <c>"A1:C3"</c>).
///     Normalised so that <see cref="TopLeft" /> is always the top-left corner.
/// </summary>
public readonly struct CellRange : IEquatable<CellRange>
{
    /// <summary>Creates a range from two corner references; the corners are normalised.</summary>
    public CellRange(CellReference a, CellReference b)
    {
        var topRow = Math.Min(a.Row, b.Row);
        var bottomRow = Math.Max(a.Row, b.Row);
        var leftCol = Math.Min(a.Column, b.Column);
        var rightCol = Math.Max(a.Column, b.Column);

        TopLeft = new CellReference(topRow, leftCol);
        BottomRight = new CellReference(bottomRow, rightCol);
    }

    /// <summary>The top-left corner of the range.</summary>
    public CellReference TopLeft { get; }

    /// <summary>The bottom-right corner of the range.</summary>
    public CellReference BottomRight { get; }

    /// <summary>The number of rows the range spans.</summary>
    public int RowCount => BottomRight.Row - TopLeft.Row + 1;

    /// <summary>The number of columns the range spans.</summary>
    public int ColumnCount => BottomRight.Column - TopLeft.Column + 1;

    /// <summary>The total number of cells in the range.</summary>
    public int CellCount => RowCount * ColumnCount;

    /// <summary>A range covering the entire sheet (A1:XFD1048576).</summary>
    public static readonly CellRange EntireSheet =
        new(new CellReference(1, 1), new CellReference(CellReference.MaxRow, CellReference.MaxColumn));

    // ── Factories ──────────────────────────────────────────────────────────────

    /// <summary>Parses an A1 range such as <c>"A1:C3"</c>, or a single cell such as <c>"B2"</c>.</summary>
    /// <exception cref="FormatException">Thrown when <paramref name="a1Range" /> is malformed.</exception>
    public static CellRange FromA1(string a1Range)
    {
        ArgumentException.ThrowIfNullOrEmpty(a1Range);

        var colon = a1Range.IndexOf(':');
        if (colon < 0)
        {
            var single = CellReference.FromA1(a1Range);
            return new CellRange(single, single);
        }

        var from = CellReference.FromA1(a1Range[..colon]);
        var to = CellReference.FromA1(a1Range[(colon + 1)..]);
        return new CellRange(from, to);
    }

    /// <summary>Parses two A1 cell references into a range.</summary>
    public static CellRange FromA1(string a1From, string a1To) =>
        new(CellReference.FromA1(a1From), CellReference.FromA1(a1To));

    /// <summary>Attempts to parse an A1 range (or single cell); returns <see langword="false" /> when malformed.</summary>
    public static bool TryFromA1(string? a1Range, out CellRange result)
    {
        result = default;
        if (string.IsNullOrEmpty(a1Range))
            return false;

        var colon = a1Range.IndexOf(':');
        if (colon < 0)
        {
            if (!CellReference.TryFromA1(a1Range, out var single))
                return false;

            result = new CellRange(single, single);
            return true;
        }

        if (!CellReference.TryFromA1(a1Range[..colon], out var from) ||
            !CellReference.TryFromA1(a1Range[(colon + 1)..], out var to))
            return false;

        result = new CellRange(from, to);
        return true;
    }

    /// <summary>Creates a range from two corner references.</summary>
    public static CellRange FromCorners(CellReference topLeft, CellReference bottomRight) =>
        new(topLeft, bottomRight);

    /// <summary>Creates a range from explicit 1-based bounds.</summary>
    // ReSharper disable once BadListLineBreaks
    public static CellRange FromBounds(int topRow, int leftColumn, int bottomRow, int rightColumn) =>
        new(new CellReference(topRow, leftColumn), new CellReference(bottomRow, rightColumn));

    // ── A1 rendering ─────────────────────────────────────────────────────────

    /// <summary>Returns the A1 range notation (e.g. "A1:C3").</summary>
    public string ToA1() => $"{TopLeft.ToA1()}:{BottomRight.ToA1()}";

    /// <summary>Returns the absolute A1 range notation (e.g. "$A$1:$C$3").</summary>
    public string ToAbsoluteA1() => $"{TopLeft.ToAbsoluteA1()}:{BottomRight.ToAbsoluteA1()}";

    /// <summary>Returns a sheet-qualified absolute A1 range (e.g. "Sheet1!$A$1:$C$3").</summary>
    public string ToSheetQualifiedA1(string sheetName)
    {
        var quoted = sheetName.AsSpan().IndexOfAny(" '!".AsSpan()) >= 0
            ? $"'{sheetName.Replace("'", "''")}'"
            : sheetName;
        return $"{quoted}!{ToAbsoluteA1()}";
    }

    /// <inheritdoc />
    public override string ToString() => ToA1();

    // ── Geometry ───────────────────────────────────────────────────────────────

    /// <summary>Returns <see langword="true" /> when <paramref name="cell" /> lies within the range.</summary>
    public bool Contains(CellReference cell) =>
        cell.Row >= TopLeft.Row && cell.Row <= BottomRight.Row &&
        cell.Column >= TopLeft.Column && cell.Column <= BottomRight.Column;

    /// <summary>Returns <see langword="true" /> when <paramref name="other" /> is fully contained.</summary>
    public bool Contains(CellRange other) =>
        Contains(other.TopLeft) && Contains(other.BottomRight);

    /// <summary>Returns <see langword="true" /> when the two ranges share at least one cell.</summary>
    public bool Overlaps(CellRange other) =>
        TopLeft.Row <= other.BottomRight.Row && BottomRight.Row >= other.TopLeft.Row &&
        TopLeft.Column <= other.BottomRight.Column && BottomRight.Column >= other.TopLeft.Column;

    /// <summary>Returns the smallest range that contains both this range and <paramref name="other" />.</summary>
    public CellRange Union(CellRange other) =>
        new(
            new CellReference(Math.Min(TopLeft.Row, other.TopLeft.Row), Math.Min(TopLeft.Column, other.TopLeft.Column)),
            new CellReference(Math.Max(BottomRight.Row, other.BottomRight.Row), Math.Max(BottomRight.Column, other.BottomRight.Column))
        );

    /// <summary>Enumerates every cell in the range in row-major order.</summary>
    public IEnumerable<CellReference> Cells()
    {
        for (var row = TopLeft.Row; row <= BottomRight.Row; row++)
        for (var col = TopLeft.Column; col <= BottomRight.Column; col++)
            yield return new CellReference(row, col);
    }

    // ── Equality ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public bool Equals(CellRange other) => TopLeft == other.TopLeft && BottomRight == other.BottomRight;

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is CellRange other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(TopLeft, BottomRight);

    /// <summary>Returns <see langword="true" /> when both ranges are identical.</summary>
    public static bool operator ==(CellRange left, CellRange right) => left.Equals(right);

    /// <summary>Returns <see langword="true" /> when the ranges differ.</summary>
    public static bool operator !=(CellRange left, CellRange right) => !left.Equals(right);
}
