using Unchained.Xlsx.Worksheets;

namespace Unchained.Xlsx.Cell;

/// <summary>
///     A column definition within a <see cref="Worksheet" />. A single definition may span a range
///     of columns (<see cref="Min" />..<see cref="Max" />) that share the same width and properties.
/// </summary>
public sealed class Column
{
    internal Column(int min, int max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>The first 1-based column number this definition covers.</summary>
    public int Min { get; internal set; }

    /// <summary>The last 1-based column number this definition covers.</summary>
    public int Max { get; internal set; }

    /// <summary>The column width in character units, or <see langword="null" /> for the sheet default.</summary>
    public double? Width { get; set; }

    /// <summary><see langword="true" /> when the column has an explicit (non-default) width.</summary>
    public bool IsCustomWidth { get; set; }

    /// <summary><see langword="true" /> when the column is hidden.</summary>
    public bool IsHidden { get; set; }

    /// <summary><see langword="true" /> when the column's outline group is collapsed.</summary>
    public bool IsCollapsed { get; set; }

    /// <summary>The outline (grouping) level, 0 when not grouped.</summary>
    public int OutlineLevel { get; set; }

    /// <summary>The column-level default style index, or <see langword="null" /> when unset.</summary>
    public int? StyleIndex { get; set; }
}
