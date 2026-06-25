using System.Globalization;
using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.Pivot;

/// <summary>The kind of a cached pivot value, mapping to a cache record element.</summary>
internal enum PivotCacheValueKind { Number, Text, Boolean, Error, Blank }

/// <summary>A single value in the pivot cache records — a type-tagged snapshot of a source cell.</summary>
internal readonly struct PivotCacheValue
{
    // ReSharper disable BadListLineBreaks
    private PivotCacheValue(PivotCacheValueKind kind, double number, string? text, bool boolean, CellError error)
        // ReSharper restore BadListLineBreaks
    {
        Kind = kind;
        Number = number;
        Text = text;
        Boolean = boolean;
        Error = error;
    }

    public PivotCacheValueKind Kind { get; }
    public double Number { get; }
    public string? Text { get; }
    public bool Boolean { get; }
    public CellError Error { get; }

    public static readonly PivotCacheValue Blank = new(PivotCacheValueKind.Blank, 0, null, false, default);

    public static PivotCacheValue FromCell(Cell.Cell? cell) =>
        cell is null
            ? Blank
            : cell.CellType switch
            {
                CellType.Number => new PivotCacheValue(PivotCacheValueKind.Number, cell.GetDouble() ?? 0, null, false, default),
                CellType.Boolean => new PivotCacheValue(PivotCacheValueKind.Boolean, 0, null, cell.GetBoolean() ?? false, default),
                CellType.Error => new PivotCacheValue(PivotCacheValueKind.Error, 0, null, false, cell.GetError() ?? CellError.Value),
                CellType.String => new PivotCacheValue(PivotCacheValueKind.Text, 0, cell.GetString() ?? string.Empty, false, default),
                CellType.Formula => FromFormula(cell),
                _ => Blank
            };

    private static PivotCacheValue FromFormula(Cell.Cell cell)
    {
        if (cell.GetDouble() is { } d)
            return new PivotCacheValue(PivotCacheValueKind.Number, d, null, false, default);
        if (cell.GetString() is { } s)
            return new PivotCacheValue(PivotCacheValueKind.Text, 0, s, false, default);

        return Blank;
    }

    public override string ToString() => Kind switch
    {
        PivotCacheValueKind.Number => Number.ToString("G15", CultureInfo.InvariantCulture),
        PivotCacheValueKind.Text => Text ?? string.Empty,
        PivotCacheValueKind.Boolean => Boolean ? "1" : "0",
        PivotCacheValueKind.Error => Error.ToLiteral(),
        _ => string.Empty
    };
}
