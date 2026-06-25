using System.Text;

namespace Unchained.Xlsx.Models;

/// <summary>Settings controlling CSV import.</summary>
public sealed class CsvLoadOptions
{
    /// <summary>The field delimiter. Defaults to comma.</summary>
    public char Delimiter { get; init; } = ',';

    /// <summary>The text encoding, or <see langword="null" /> to auto-detect from a BOM.</summary>
    public Encoding? Encoding { get; init; }

    /// <summary>Whether the first row holds headers (still imported as data).</summary>
    public bool HasHeaders { get; init; }

    /// <summary>Whether to infer numeric, boolean, and date types from text.</summary>
    public bool TypeInference { get; init; } = true;

    /// <summary>The date format to attempt during inference; empty disables date inference.</summary>
    public string DateFormat { get; init; } = string.Empty;

    /// <summary>The name of the sheet created to hold the imported data.</summary>
    public string SheetName { get; init; } = "Sheet1";

    /// <summary>The default options.</summary>
    public static readonly CsvLoadOptions Default = new();
}
