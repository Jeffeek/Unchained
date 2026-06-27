using Unchained.Xlsx.Models;
using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.DataValidation;

/// <summary>A data-validation constraint applied to one or more cell ranges.</summary>
public sealed class DataValidation
{
    /// <summary>The constraint type.</summary>
    public DataValidationType Type { get; set; } = DataValidationType.None;

    /// <summary>The comparison operator (for numeric, date, time, and text-length types).</summary>
    public DataValidationOperator Operator { get; set; } = DataValidationOperator.Between;

    /// <summary>The ranges this validation applies to.</summary>
    public IList<CellRange> Ranges { get; } = [];

    /// <summary>The first formula/operand (e.g. the lower bound, or the list source).</summary>
    public string? Formula1 { get; set; }

    /// <summary>The second formula/operand (e.g. the upper bound for a between constraint).</summary>
    public string? Formula2 { get; set; }

    /// <summary>Whether blank entries are permitted.</summary>
    public bool AllowBlank { get; set; } = true;

    /// <summary>Whether to show the input prompt message.</summary>
    public bool ShowInputMessage { get; set; } = true;

    /// <summary>Whether to show the error alert on invalid input.</summary>
    public bool ShowErrorAlert { get; set; } = true;

    /// <summary>Whether to show the in-cell drop-down for list validations.</summary>
    public bool ShowDropDown { get; set; } = true;

    /// <summary>The severity of the error alert.</summary>
    public DataValidationErrorStyle ErrorStyle { get; set; } = DataValidationErrorStyle.Stop;

    /// <summary>The error alert title.</summary>
    public string? ErrorTitle { get; set; }

    /// <summary>The error alert message.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>The input prompt title.</summary>
    public string? PromptTitle { get; set; }

    /// <summary>The input prompt message.</summary>
    public string? Prompt { get; set; }
}
