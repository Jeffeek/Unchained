namespace Unchained.Xlsx.Models.DataValidation;

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
