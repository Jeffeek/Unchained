using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models;

namespace Unchained.Xlsx.Abstractions;

/// <summary>
///     The public contract for the Unchained.Xlsx entry point: loading, creating, and saving
///     workbooks. Implemented by <see cref="SpreadsheetProcessor" />.
/// </summary>
public interface ISpreadsheetProcessor : IDisposable
{
    /// <summary>Loads a workbook from a file path.</summary>
    Task<SpreadsheetDocument> LoadAsync(
        string path,
        OpenOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>Loads a workbook from a stream.</summary>
    Task<SpreadsheetDocument> LoadAsync(
        Stream stream,
        OpenOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>Loads a workbook from a byte array.</summary>
    Task<SpreadsheetDocument> LoadAsync(
        ReadOnlyMemory<byte> data,
        OpenOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>Loads a CSV file into a fresh single-sheet workbook.</summary>
    Task<SpreadsheetDocument> LoadFromCsvAsync(
        string path,
        CsvLoadOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>Loads CSV data from a stream into a fresh single-sheet workbook.</summary>
    Task<SpreadsheetDocument> LoadFromCsvAsync(
        Stream stream,
        CsvLoadOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>Creates a new blank workbook with a single default sheet.</summary>
    SpreadsheetDocument CreateBlank();

    /// <summary>Creates a new blank workbook with a single sheet of the given name.</summary>
    SpreadsheetDocument CreateBlank(string firstSheetName);

    /// <summary>Saves the workbook to a file.</summary>
    Task SaveAsync(
        SpreadsheetDocument document,
        string path,
        XlsxSaveOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>Saves the workbook to a stream.</summary>
    Task SaveAsync(
        SpreadsheetDocument document,
        Stream stream,
        XlsxSaveOptions? options = null,
        CancellationToken cancellationToken = default
    );
}
