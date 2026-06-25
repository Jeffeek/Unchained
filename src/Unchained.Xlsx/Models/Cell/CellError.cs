namespace Unchained.Xlsx.Models.Cell;

/// <summary>The set of error values a cell may contain.</summary>
public enum CellError
{
    /// <summary><c>#NULL!</c> — intersection of two ranges that do not intersect.</summary>
    Null,

    /// <summary><c>#DIV/0!</c> — division by zero.</summary>
    DivisionByZero,

    /// <summary><c>#VALUE!</c> — wrong type of argument or operand.</summary>
    Value,

    /// <summary><c>#REF!</c> — invalid cell reference.</summary>
    Reference,

    /// <summary><c>#NAME?</c> — unrecognised name in a formula.</summary>
    Name,

    /// <summary><c>#NUM!</c> — invalid numeric value.</summary>
    Number,

    /// <summary><c>#N/A</c> — value not available.</summary>
    NotAvailable
}

/// <summary>Converts between <see cref="CellError" /> values and their SpreadsheetML literals.</summary>
internal static class CellErrorExtensions
{
    public static string ToLiteral(this CellError error) => error switch
    {
        CellError.Null => "#NULL!",
        CellError.DivisionByZero => "#DIV/0!",
        CellError.Value => "#VALUE!",
        CellError.Reference => "#REF!",
        CellError.Name => "#NAME?",
        CellError.Number => "#NUM!",
        CellError.NotAvailable => "#N/A",
        _ => "#VALUE!"
    };

    public static CellError? FromLiteral(string? literal) => literal switch
    {
        "#NULL!" => CellError.Null,
        "#DIV/0!" => CellError.DivisionByZero,
        "#VALUE!" => CellError.Value,
        "#REF!" => CellError.Reference,
        "#NAME?" => CellError.Name,
        "#NUM!" => CellError.Number,
        "#N/A" => CellError.NotAvailable,
        _ => null
    };
}
