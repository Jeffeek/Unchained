namespace Unchained.Xlsx.Models.ConditionalFormatting;

/// <summary>The kind of conditional-formatting rule.</summary>
public enum ConditionalFormatRuleType
{
    /// <summary>Compare the cell value against a constant or range using an operator.</summary>
    CellValue,

    /// <summary>Apply formatting when a custom formula evaluates to true.</summary>
    Expression,

    /// <summary>A colour scale (round-trip preserved only in v0.1.0).</summary>
    ColorScale,

    /// <summary>A data bar (round-trip preserved only in v0.1.0).</summary>
    DataBar,

    /// <summary>An icon set (round-trip preserved only in v0.1.0).</summary>
    IconSet,

    /// <summary>The top or bottom N values.</summary>
    Top10,

    /// <summary>Cells with unique values.</summary>
    UniqueValues,

    /// <summary>Cells with duplicate values.</summary>
    DuplicateValues,

    /// <summary>Text begins with a string.</summary>
    BeginsWith,

    /// <summary>Text ends with a string.</summary>
    EndsWith,

    /// <summary>Text contains a string.</summary>
    ContainsText,

    /// <summary>Any other rule type, preserved verbatim.</summary>
    Other
}
