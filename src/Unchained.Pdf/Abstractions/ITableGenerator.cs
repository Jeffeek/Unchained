using Unchained.Pdf.Models;

namespace Unchained.Pdf.Abstractions;

/// <summary>
///     Generates PDF documents or pages containing tabular data.
///     <para>
///         Table generation is a CPU-intensive operation when row counts are large.
///         Implementations must batch all cell data before emitting any PDF content stream
///         operators to avoid the allocation cost of incremental layout recalculation.
///     </para>
/// </summary>
public interface ITableGenerator
{
    /// <summary>
    ///     Creates a new single-page PDF document containing <paramref name="data" /> rendered
    ///     according to <paramref name="style" />. The returned document is caller-owned.
    /// </summary>
    /// <param name="data">The table headers, rows, and optional title.</param>
    /// <param name="style">Visual styling: fonts, padding, borders, and colours.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    /// <returns>A new <see cref="IPdfDocument" /> whose first page contains the table.</returns>
    Task<IPdfDocument> GenerateAsync(TableData data, TableStyle style, CancellationToken ct = default);

    /// <summary>
    ///     Appends a table to the last page of <paramref name="document" />, or adds a new
    ///     page if the existing content does not leave enough room.
    /// </summary>
    /// <param name="document">The document to append to. Must not be disposed.</param>
    /// <param name="data">The table headers, rows, and optional title.</param>
    /// <param name="style">Visual styling applied to the appended table.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task AppendTableAsync(
        IPdfDocument document,
        TableData data,
        TableStyle style,
        CancellationToken ct = default
    );
}
