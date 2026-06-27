namespace Unchained.Xlsx.Models;

/// <summary>The severity style shown when data validation fails.</summary>
public enum DataValidationErrorStyle
{
    /// <summary>Block the invalid entry.</summary>
    Stop,

    /// <summary>Warn but allow the entry.</summary>
    Warning,

    /// <summary>Inform only.</summary>
    Information
}

/// <summary>The comparison operator for a data validation or conditional-format rule.</summary>
public enum DataValidationOperator
{
    /// <summary>Value is between two bounds (inclusive).</summary>
    Between,

    /// <summary>Value is not between two bounds.</summary>
    NotBetween,

    /// <summary>Value equals the comparand.</summary>
    Equal,

    /// <summary>Value does not equal the comparand.</summary>
    NotEqual,

    /// <summary>Value is greater than the comparand.</summary>
    GreaterThan,

    /// <summary>Value is less than the comparand.</summary>
    LessThan,

    /// <summary>Value is greater than or equal to the comparand.</summary>
    GreaterThanOrEqual,

    /// <summary>Value is less than or equal to the comparand.</summary>
    LessThanOrEqual
}

/// <summary>The kind of data validation constraint applied to a cell range.</summary>
public enum DataValidationType
{
    /// <summary>No validation.</summary>
    None,

    /// <summary>Whole-number constraint.</summary>
    Whole,

    /// <summary>Decimal-number constraint.</summary>
    Decimal,

    /// <summary>A drop-down list of allowed values.</summary>
    List,

    /// <summary>A date constraint.</summary>
    Date,

    /// <summary>A time constraint.</summary>
    Time,

    /// <summary>A text-length constraint.</summary>
    TextLength,

    /// <summary>A custom formula constraint.</summary>
    Custom
}
