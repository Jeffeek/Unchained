namespace Unchained.Xlsx.Models.Styles;

/// <summary>The underline style applied to cell font.</summary>
public enum FontUnderline
{
    /// <summary>No underline.</summary>
    None,

    /// <summary>A single underline.</summary>
    Single,

    /// <summary>A double underline.</summary>
    Double,

    /// <summary>A single accounting-style underline (spans the cell width).</summary>
    SingleAccounting,

    /// <summary>A double accounting-style underline.</summary>
    DoubleAccounting
}
