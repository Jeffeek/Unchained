namespace Unchained.Xlsx.Models.Pivot;

/// <summary>The aggregate function applied to a pivot table data field.</summary>
public enum PivotDataFunction
{
    /// <summary>Sum of values.</summary>
    Sum,

    /// <summary>Count of values (non-empty).</summary>
    Count,

    /// <summary>Average of values.</summary>
    Average,

    /// <summary>Maximum value.</summary>
    Max,

    /// <summary>Minimum value.</summary>
    Min,

    /// <summary>Product of values.</summary>
    Product,

    /// <summary>Count of numeric values.</summary>
    CountNumbers,

    /// <summary>Sample standard deviation.</summary>
    StdDev,

    /// <summary>Population standard deviation.</summary>
    StdDevP,

    /// <summary>Sample variance.</summary>
    Var,

    /// <summary>Population variance.</summary>
    VarP
}

/// <summary>The axis a pivot field is placed on.</summary>
public enum PivotAxis
{
    /// <summary>Not placed on any axis.</summary>
    None,

    /// <summary>Row labels.</summary>
    Row,

    /// <summary>Column labels.</summary>
    Column,

    /// <summary>Report filter (page).</summary>
    Page,

    /// <summary>Values (data) area.</summary>
    Data
}
