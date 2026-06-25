namespace Unchained.Xlsx.Models.DataValidation;

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
