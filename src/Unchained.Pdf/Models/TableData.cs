namespace Unchained.Pdf.Models;

/// <summary>
/// Data-only representation of a table to be rendered into a PDF document
/// visual presentation is controlled separately by <see cref="TableStyle"/>.
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
public sealed class TableData
{
    /// <summary>
    /// Column header labels, in left-to-right order.
    /// The number of headers determines the number of columns.
    /// </summary>
    public required IReadOnlyList<string> Headers { get; init; }

    /// <summary>
    /// Data rows, each containing one string value per column.
    /// Each inner list must have the same length as <see cref="Headers"/>.
    /// </summary>
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }

    /// <summary>
    /// An optional title rendered above the table. Pass <see langword="null"/> to omit.
    /// </summary>
    public string? Title { get; init; }
}
