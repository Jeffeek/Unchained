using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Unchained.Xlsx.Models.Cell;

/// <summary>
///     An immutable address of a single cell, using 1-based row and column indices and supporting
///     A1 notation (e.g. <c>"B3"</c> ↔ row 3, column 2). Cells are ordered row-major.
/// </summary>
public readonly struct CellReference : IEquatable<CellReference>, IComparable<CellReference>
{
    /// <summary>The maximum column supported by the XLSX format (XFD = 16384).</summary>
    public const int MaxColumn = 16384;

    /// <summary>The maximum row supported by the XLSX format (1048576).</summary>
    public const int MaxRow = 1_048_576;

    /// <summary>Creates a reference from a 1-based row and column.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when row or column is out of the XLSX range.</exception>
    public CellReference(int row, int column)
    {
        if (row is < 1 or > MaxRow)
            throw new ArgumentOutOfRangeException(nameof(row), row, $"Row must be between 1 and {MaxRow}.");
        if (column is < 1 or > MaxColumn)
            throw new ArgumentOutOfRangeException(nameof(column), column, $"Column must be between 1 and {MaxColumn}.");

        Row = row;
        Column = column;
    }

    /// <summary>The 1-based row index.</summary>
    public int Row { get; }

    /// <summary>The 1-based column index (A = 1).</summary>
    public int Column { get; }

    /// <summary>The column letters in A1 notation (e.g. "A", "Z", "AA").</summary>
    public string ColumnLetter => ColumnNumberToLetters(Column);

    // ── Factories ──────────────────────────────────────────────────────────────

    /// <summary>Creates a reference from a 1-based row and column. Equivalent to the constructor.</summary>
    public static CellReference FromRowColumn(int row, int column) => new(row, column);

    /// <summary>Parses an A1-notation reference such as <c>"B3"</c> or <c>"$AA$100"</c>.</summary>
    /// <exception cref="FormatException">Thrown when <paramref name="a1" /> is not a valid cell reference.</exception>
    public static CellReference FromA1(string a1) =>
        TryFromA1(a1, out var result)
            ? result
            : throw new FormatException($"'{a1}' is not a valid A1 cell reference.");

    /// <summary>Attempts to parse an A1-notation reference. Dollar signs (absolute markers) are ignored.</summary>
    public static bool TryFromA1(string? a1, out CellReference result)
    {
        result = default;
        if (string.IsNullOrEmpty(a1))
            return false;

        var i = 0;
        var column = 0;
        if (a1[i] == '$') i++;

        var letterStart = i;
        while (i < a1.Length && IsAsciiLetter(a1[i]))
        {
            column = (column * 26) + (char.ToUpperInvariant(a1[i]) - 'A') + 1;
            i++;
        }

        if (i == letterStart || column is < 1 or > MaxColumn)
            return false;

        if (i < a1.Length && a1[i] == '$') i++;

        var digitStart = i;
        var row = 0;
        while (i < a1.Length && char.IsAsciiDigit(a1[i]))
        {
            row = (row * 10) + (a1[i] - '0');
            i++;
        }

        if (i != a1.Length || i == digitStart || row is < 1 or > MaxRow)
            return false;

        result = new CellReference(row, column);
        return true;
    }

    // ── A1 rendering ─────────────────────────────────────────────────────────

    /// <summary>Returns the A1 notation of this reference (e.g. "B3").</summary>
    public string ToA1() => ColumnLetter + Row.ToString(CultureInfo.InvariantCulture);

    /// <summary>Returns the absolute A1 notation of this reference (e.g. "$B$3").</summary>
    public string ToAbsoluteA1() => $"${ColumnLetter}${Row}";

    /// <inheritdoc />
    public override string ToString() => ToA1();

    // ── Navigation ─────────────────────────────────────────────────────────────

    /// <summary>Returns a new reference offset by the given row and column deltas.</summary>
    public CellReference Offset(int rowDelta, int columnDelta) =>
        new(Row + rowDelta, Column + columnDelta);

    /// <summary>Returns a copy with the row replaced.</summary>
    public CellReference WithRow(int row) => new(row, Column);

    /// <summary>Returns a copy with the column replaced.</summary>
    public CellReference WithColumn(int column) => new(Row, column);

    // ── Column ↔ letters ─────────────────────────────────────────────────────

    /// <summary>Converts a 1-based column number to its A1 letters (1 → "A", 27 → "AA").</summary>
    public static string ColumnNumberToLetters(int column)
    {
        if (column is < 1 or > MaxColumn)
            throw new ArgumentOutOfRangeException(nameof(column), column, $"Column must be between 1 and {MaxColumn}.");

        var builder = new StringBuilder(3);
        while (column > 0)
        {
            var remainder = (column - 1) % 26;
            builder.Insert(0, (char)('A' + remainder));
            column = (column - 1) / 26;
        }

        return builder.ToString();
    }

    /// <summary>Converts A1 column letters to a 1-based column number ("A" → 1, "AA" → 27).</summary>
    /// <exception cref="FormatException">Thrown when <paramref name="letters" /> contains non-letters or is empty.</exception>
    public static int ColumnLettersToNumber(string letters)
    {
        if (string.IsNullOrEmpty(letters))
            throw new FormatException("Column letters cannot be empty.");

        var column = 0;
        foreach (var c in letters)
        {
            if (!IsAsciiLetter(c))
                throw new FormatException($"'{letters}' is not a valid column reference.");

            column = (column * 26) + (char.ToUpperInvariant(c) - 'A') + 1;
        }

        return column;
    }

    private static bool IsAsciiLetter(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    // ── Equality & ordering ──────────────────────────────────────────────────

    /// <inheritdoc />
    public bool Equals(CellReference other) => Row == other.Row && Column == other.Column;

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is CellReference other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Row, Column);

    /// <summary>Compares two references in row-major order (row first, then column).</summary>
    public int CompareTo(CellReference other)
    {
        var byRow = Row.CompareTo(other.Row);
        return byRow != 0 ? byRow : Column.CompareTo(other.Column);
    }

    /// <summary>Returns <see langword="true" /> when both references address the same cell.</summary>
    public static bool operator ==(CellReference left, CellReference right) => left.Equals(right);

    /// <summary>Returns <see langword="true" /> when the references differ.</summary>
    public static bool operator !=(CellReference left, CellReference right) => !left.Equals(right);

    /// <summary>Row-major less-than comparison.</summary>
    public static bool operator <(CellReference left, CellReference right) => left.CompareTo(right) < 0;

    /// <summary>Row-major greater-than comparison.</summary>
    public static bool operator >(CellReference left, CellReference right) => left.CompareTo(right) > 0;

    /// <summary>Row-major less-than-or-equal comparison.</summary>
    public static bool operator <=(CellReference left, CellReference right) => left.CompareTo(right) <= 0;

    /// <summary>Row-major greater-than-or-equal comparison.</summary>
    public static bool operator >=(CellReference left, CellReference right) => left.CompareTo(right) >= 0;
}
