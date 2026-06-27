namespace Unchained.Xlsx.Models.Tables;

/// <summary>The aggregate function shown in a table's totals row.</summary>
public enum TotalsRowFunction
{
    /// <summary>No totals function.</summary>
    None,

    /// <summary>Sum of the column.</summary>
    Sum,

    /// <summary>Count of values.</summary>
    Count,

    /// <summary>Average of the column.</summary>
    Average,

    /// <summary>Maximum value.</summary>
    Max,

    /// <summary>Minimum value.</summary>
    Min,

    /// <summary>Standard deviation.</summary>
    StdDev,

    /// <summary>Variance.</summary>
    Var,

    /// <summary>Count of numeric values.</summary>
    CountNumbers,

    /// <summary>A custom totals formula.</summary>
    Custom
}
