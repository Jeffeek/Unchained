namespace Unchained.Xlsx.Models.Cell;

/// <summary>The kind of value a cell holds.</summary>
public enum CellType
{
    /// <summary>The cell has no value.</summary>
    Empty,

    /// <summary>A numeric value (also used for dates, which are stored as serial numbers).</summary>
    Number,

    /// <summary>A text value (shared string or inline string).</summary>
    String,

    /// <summary>A boolean value.</summary>
    Boolean,

    /// <summary>An error value (see <see cref="CellError" />).</summary>
    Error,

    /// <summary>A formula cell. The cached result type is exposed through the value getters.</summary>
    Formula
}
