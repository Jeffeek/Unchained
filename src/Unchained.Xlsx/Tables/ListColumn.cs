using Unchained.Xlsx.Models.Tables;

namespace Unchained.Xlsx.Tables;

/// <summary>A single column of a table (<see cref="ListObject" />).</summary>
public sealed class ListColumn
{
    internal ListColumn(int id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>The stable column id within the table.</summary>
    public int Id { get; internal set; }

    /// <summary>The column header name. Must be unique within the table.</summary>
    public string Name { get; set; }

    /// <summary>The aggregate function shown in the totals row.</summary>
    public TotalsRowFunction TotalsFunction { get; set; } = TotalsRowFunction.None;

    /// <summary>The label shown in the totals row (e.g. "Total").</summary>
    public string? TotalsLabel { get; set; }

    /// <summary>A custom totals-row formula, when <see cref="TotalsFunction" /> is <see cref="TotalsRowFunction.Custom" />.</summary>
    public string? TotalsFormula { get; set; }

    /// <summary>A calculated-column formula applied to every data cell of the column.</summary>
    public string? ColumnFormula { get; set; }
}
