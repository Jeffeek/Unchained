using System.Text;
using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.Models;

/// <summary>Settings controlling CSV export of a worksheet.</summary>
public sealed class CsvSaveOptions
{
    /// <summary>The default options.</summary>
    public static readonly CsvSaveOptions Default = new();

    /// <summary>The field delimiter. Defaults to comma.</summary>
    public char Delimiter { get; init; } = ',';

    /// <summary>The text encoding. Defaults to UTF-8.</summary>
    public Encoding Encoding { get; init; } = Encoding.UTF8;

    /// <summary>Whether to write a byte-order mark.</summary>
    public bool WriteBom { get; init; }

    /// <summary>Whether to quote every field regardless of content.</summary>
    public bool QuoteAllFields { get; init; }

    /// <summary>Whether to quote fields that contain the delimiter, quotes, or line breaks.</summary>
    public bool QuoteFieldsWithDelimiter { get; init; } = true;

    /// <summary>The .NET format string used for date values.</summary>
    public string DateFormat { get; init; } = "yyyy-MM-dd";

    /// <summary>The .NET format string used for date/time values.</summary>
    public string DateTimeFormat { get; init; } = "yyyy-MM-dd HH:mm:ss";

    /// <summary>The numeric format string. "G" by default.</summary>
    public string NumberFormat { get; init; } = "G";

    /// <summary>The literal written for boolean true.</summary>
    public string TrueValue { get; init; } = "TRUE";

    /// <summary>The literal written for boolean false.</summary>
    public string FalseValue { get; init; } = "FALSE";

    /// <summary>The range to export, or <see langword="null" /> to export the used range.</summary>
    public CellRange? Range { get; init; }
}
