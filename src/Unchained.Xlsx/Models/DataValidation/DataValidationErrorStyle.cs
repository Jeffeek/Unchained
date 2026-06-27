namespace Unchained.Xlsx.Models.DataValidation;

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
