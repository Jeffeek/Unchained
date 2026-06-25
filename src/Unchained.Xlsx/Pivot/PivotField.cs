using Unchained.Xlsx.Models.Pivot;

namespace Unchained.Xlsx.Pivot;

/// <summary>A field (source column) available to a pivot table.</summary>
public sealed class PivotField
{
    internal PivotField(string name, int sourceIndex)
    {
        Name = name;
        SourceIndex = sourceIndex;
    }

    /// <summary>The field's name (the source column header).</summary>
    public string Name { get; set; }

    /// <summary>The 0-based index of this field within the source range columns.</summary>
    public int SourceIndex { get; }

    /// <summary>The axis this field is currently placed on.</summary>
    public PivotAxis Axis { get; set; } = PivotAxis.None;

    /// <summary>The distinct shared items observed for this field (cache item strings).</summary>
    public IList<string> Items { get; } = [];
}

/// <summary>A data (values) field in a pivot table, with its aggregate function.</summary>
public sealed class PivotDataField
{
    internal PivotDataField(string name, int sourceIndex, PivotDataFunction function)
    {
        Name = name;
        SourceIndex = sourceIndex;
        Function = function;
    }

    /// <summary>The display name (e.g. "Sum of Amount").</summary>
    public string Name { get; set; }

    /// <summary>The 0-based source-column index this data field aggregates.</summary>
    public int SourceIndex { get; }

    /// <summary>The aggregate function.</summary>
    public PivotDataFunction Function { get; set; }
}
