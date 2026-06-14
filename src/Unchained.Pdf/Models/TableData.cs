namespace Unchained.Pdf.Models;

/// <summary>
///     Data-only representation of a table to be rendered into a PDF document
///     visual presentation is controlled separately by <see cref="TableStyle" />.
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
public sealed class TableData
{
    /// <summary>
    ///     Column header labels, in left-to-right order.
    ///     The number of headers determines the number of columns.
    /// </summary>
    public required IReadOnlyList<string> Headers { get; init; }

    /// <summary>
    ///     Data rows, each containing one string value per column.
    ///     Each inner list must have the same length as <see cref="Headers" />.
    /// </summary>
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }

    /// <summary>
    ///     An optional title rendered above the table. Pass <see langword="null" /> to omit.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    ///     When <see langword="true" />, the generated PDF includes a tagged structure tree
    ///     (<c>/MarkInfo /Marked true</c>, <c>/StructTreeRoot</c>) with <c>/Table</c>,
    ///     <c>/TR</c>, <c>/TH</c>, and <c>/TD</c> structure elements so that assistive
    ///     technologies can navigate the table content.
    /// </summary>
    public bool Tagged { get; init; }

    /// <summary>
    ///     BCP 47 language tag written to the document catalog's <c>/Lang</c> entry
    ///     (e.g. <c>"en-US"</c>). Required for PDF/UA conformance when
    ///     <see cref="Tagged" /> is <see langword="true" />.
    /// </summary>
    public string? Language { get; init; }
}
